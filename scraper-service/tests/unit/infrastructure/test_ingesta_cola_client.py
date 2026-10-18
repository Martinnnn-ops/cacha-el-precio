"""Pruebas del cliente de la ingesta por la cola (T04), sin red: httpx.MockTransport."""

import json

import httpx
import pytest

from scraper.config.settings import Settings
from scraper.infrastructure.http.ingesta_cola_client import (
    ErrorIngesta,
    IngestaColaClient,
    TokenCognito,
    sobre,
)

URL_TOKEN = "https://cep.auth.us-east-1.amazoncognito.com/oauth2/token"
URL_INGESTA = "https://api.example/prod/ingesta"
PAYLOAD = {"externalId": "SKU-1", "store": "paris", "name": "Polera", "price": 9990}


class Servidor:
    """Cognito + API Gateway falsos: responde según la URL y guarda lo que recibe."""

    def __init__(self, respuestas_ingesta=None, token_status=200):
        self.respuestas = list(respuestas_ingesta or [200])
        self.token_status = token_status
        self.tokens_pedidos = 0
        self.ingestas = []

    def __call__(self, request: httpx.Request) -> httpx.Response:
        if str(request.url) == URL_TOKEN:
            self.tokens_pedidos += 1
            if self.token_status != 200:
                return httpx.Response(self.token_status)
            return httpx.Response(
                200, json={"access_token": f"tok-{self.tokens_pedidos}", "expires_in": 3600}
            )
        self.ingestas.append(request)
        respuesta = self.respuestas.pop(0) if len(self.respuestas) > 1 else self.respuestas[0]
        if isinstance(respuesta, Exception):
            raise respuesta
        return httpx.Response(respuesta, text="<SendMessageResponse/>")


def cliente(servidor, reintentos=3, reloj=None):
    http = httpx.Client(transport=httpx.MockTransport(servidor))
    token = TokenCognito(URL_TOKEN, "id", "secreto", "https://api.cachaelprecio.cl/ingesta", http,
                         **({"reloj": reloj} if reloj else {}))
    return IngestaColaClient(URL_INGESTA, token, http, reintentos=reintentos, dormir=lambda s: None)


# ── El sobre ─────────────────────────────────────────────────────────────

def test_el_sobre_es_oferta_capturada_v1_con_el_payload_intacto():
    s = sobre(PAYLOAD)
    assert s["type"] == "oferta.capturada"
    assert s["version"] == 1
    assert s["source"] == "scraper-service"
    assert s["occurredAt"].endswith("Z")
    assert s["data"] == PAYLOAD
    assert len(s["eventId"]) == 36


def test_cada_prenda_lleva_un_event_id_distinto():
    assert sobre(PAYLOAD)["eventId"] != sobre(PAYLOAD)["eventId"]


# ── Camino feliz ─────────────────────────────────────────────────────────

def test_publica_con_el_token_del_scraper():
    servidor = Servidor()
    resultado = cliente(servidor).sincronizar_producto(PAYLOAD)

    assert resultado["encolado"] is True
    peticion = servidor.ingestas[0]
    assert peticion.headers["Authorization"] == "Bearer tok-1"
    cuerpo = json.loads(peticion.content)
    assert cuerpo["type"] == "oferta.capturada" and cuerpo["data"] == PAYLOAD


def test_reutiliza_el_token_mientras_no_vence():
    servidor = Servidor()
    c = cliente(servidor)
    for _ in range(5):
        c.sincronizar_producto(PAYLOAD)
    assert servidor.tokens_pedidos == 1


def test_renueva_el_token_cuando_esta_por_vencer():
    ahora = [0.0]
    servidor = Servidor()
    c = cliente(servidor, reloj=lambda: ahora[0])
    c.sincronizar_producto(PAYLOAD)
    ahora[0] = 3600 - 30          # dentro del margen de 60 s antes de vencer
    c.sincronizar_producto(PAYLOAD)
    assert servidor.tokens_pedidos == 2


# ── Errores ──────────────────────────────────────────────────────────────

def test_ante_401_pide_un_token_nuevo_una_sola_vez():
    servidor = Servidor([401, 200])
    cliente(servidor).sincronizar_producto(PAYLOAD)
    assert servidor.tokens_pedidos == 2
    assert servidor.ingestas[1].headers["Authorization"] == "Bearer tok-2"


def test_401_que_se_repite_falla_esa_prenda():
    servidor = Servidor([401, 401])
    with pytest.raises(ErrorIngesta, match="HTTP 401"):
        cliente(servidor).sincronizar_producto(PAYLOAD)
    assert servidor.tokens_pedidos == 2


@pytest.mark.parametrize("estado", [400, 403, 413])
def test_otro_4xx_falla_solo_esa_prenda_sin_reintentar(estado):
    servidor = Servidor([estado])
    with pytest.raises(ErrorIngesta, match=f"HTTP {estado}"):
        cliente(servidor).sincronizar_producto(PAYLOAD)
    assert len(servidor.ingestas) == 1


def test_5xx_se_reintenta_y_si_se_recupera_queda_encolada():
    servidor = Servidor([503, 500, 200])
    assert cliente(servidor).sincronizar_producto(PAYLOAD)["encolado"] is True
    assert len(servidor.ingestas) == 3


def test_5xx_que_no_se_recupera_falla_tras_3_intentos():
    servidor = Servidor([502])
    with pytest.raises(ErrorIngesta, match="tras 3 intentos"):
        cliente(servidor).sincronizar_producto(PAYLOAD)
    assert len(servidor.ingestas) == 3


def test_timeout_se_reintenta():
    servidor = Servidor([httpx.ReadTimeout("lento"), 200])
    assert cliente(servidor).sincronizar_producto(PAYLOAD)["encolado"] is True


def test_si_cognito_no_entrega_token_falla_con_mensaje_claro():
    with pytest.raises(ErrorIngesta, match="Cognito no entrego el token"):
        cliente(Servidor(token_status=400)).sincronizar_producto(PAYLOAD)


# ── Configuración ────────────────────────────────────────────────────────

def test_por_defecto_publica_por_http():
    assert Settings(_env_file=None).modo_publicacion == "http"


def test_modo_cola_sin_variables_dice_cuales_faltan():
    cfg = Settings(_env_file=None, modo_publicacion="cola")
    faltan = cfg.faltantes_para_cola()
    assert "API_GATEWAY_URL_INGESTA" in faltan
    assert "COGNITO_SCRAPER_CLIENT_SECRET" in faltan


def test_la_url_del_token_sale_del_dominio_de_cognito():
    cfg = Settings(
        _env_file=None, cognito_dominio="cacha-el-precio-123", cognito_region="us-east-1"
    )
    assert cfg.url_token_cognito() == (
        "https://cacha-el-precio-123.auth.us-east-1.amazoncognito.com/oauth2/token"
    )


# ── main.construir_sync elige el cliente según MODO_PUBLICACION ──────────

def test_construir_sync_en_modo_cola_usa_la_ingesta():
    from scraper.main import construir_sync

    cfg = Settings(
        _env_file=None,
        modo_publicacion="cola",
        api_gateway_url_ingesta=URL_INGESTA,
        cognito_dominio="cep",
        cognito_scraper_client_id="id",
        cognito_scraper_client_secret="secreto",
        cognito_scraper_scope="https://api.cachaelprecio.cl/ingesta",
    )
    assert isinstance(construir_sync(cfg)._cliente, IngestaColaClient)


def test_construir_sync_en_modo_cola_incompleto_no_arranca():
    from scraper.main import construir_sync

    with pytest.raises(SystemExit, match="API_GATEWAY_URL_INGESTA"):
        construir_sync(Settings(_env_file=None, modo_publicacion="cola"))


def test_construir_sync_en_modo_http_usa_product_service():
    from scraper.infrastructure.http.product_service_client import ProductServiceClient
    from scraper.main import construir_sync

    cfg = Settings(_env_file=None, product_service_url="http://product-service:8081")
    assert isinstance(construir_sync(cfg)._cliente, ProductServiceClient)
