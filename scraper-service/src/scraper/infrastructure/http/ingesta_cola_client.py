# src/scraper/infrastructure/http/ingesta_cola_client.py
"""
Cliente de la ingesta asincrona (EP3, flujo 1).

En vez de entregarle cada prenda a Product Service por HTTP, la deja en la
cola SQS cep-ofertas-crudas a traves de POST /ingesta del API Gateway. Si
Product Service esta caido, la prenda espera en la cola y no se pierde.

Tiene el mismo metodo que ProductServiceClient (sincronizar_producto), asi que
ProductServiceSync y el barrido no saben donde se publica: lo decide
MODO_PUBLICACION en main.py.

Contrato del sobre en docs/MENSAJERIA.md (oferta.capturada v1): data es
exactamente el mismo payload que recibe POST /api/products.
"""

from __future__ import annotations

import logging
import time
import uuid
from datetime import UTC, datetime

import httpx

log = logging.getLogger(__name__)

TIPO = "oferta.capturada"
VERSION = 1
FUENTE = "scraper-service"

# El token se renueva un poco antes de vencer, para no mandar uno que caduca
# en el camino.
_MARGEN_TOKEN_S = 60


class ErrorIngesta(Exception):
    """La prenda no se pudo dejar en la cola. El barrido la cuenta como fallo de sync."""


class TokenCognito:
    """
    Pide el access_token del client del scraper a Cognito (client_credentials,
    scope ingesta) y lo reutiliza hasta que esta por vencer.
    """

    def __init__(
        self,
        token_url: str,
        client_id: str,
        client_secret: str,
        scope: str,
        http: httpx.Client,
        reloj=time.monotonic,
    ) -> None:
        self._url = token_url
        self._auth = (client_id, client_secret)
        self._scope = scope
        self._http = http
        self._reloj = reloj
        self._token: str | None = None
        self._vence = 0.0

    def obtener(self, forzar: bool = False) -> str:
        if forzar or self._token is None or self._reloj() >= self._vence:
            r = self._http.post(
                self._url,
                auth=self._auth,
                data={"grant_type": "client_credentials", "scope": self._scope},
            )
            if r.status_code != 200:
                raise ErrorIngesta(
                    f"Cognito no entrego el token del scraper (HTTP {r.status_code})"
                )
            cuerpo = r.json()
            self._token = cuerpo["access_token"]
            self._vence = self._reloj() + int(cuerpo.get("expires_in", 3600)) - _MARGEN_TOKEN_S
            log.info("Token del scraper renovado (vence en %s s)", cuerpo.get("expires_in"))
        assert self._token is not None
        return self._token


def sobre(payload: dict, ahora: datetime | None = None) -> dict:
    """Envuelve una prenda en el sobre oferta.capturada v1, con un eventId nuevo."""
    return {
        "eventId": str(uuid.uuid4()),
        "type": TIPO,
        "version": VERSION,
        "source": FUENTE,
        "occurredAt": (ahora or datetime.now(UTC)).isoformat().replace("+00:00", "Z"),
        "data": payload,
    }


class IngestaColaClient:
    """
    Publica cada prenda en POST /ingesta.

    - 200: la prenda quedo encolada.
    - 401: el token vencio o lo revocaron: se pide uno nuevo UNA vez y se reintenta.
    - otro 4xx: la prenda esta mal o no hay permiso; falla solo esa prenda.
    - 5xx o timeout: se reintenta hasta `reintentos` veces con espera creciente.
    """

    def __init__(
        self,
        url_ingesta: str,
        token: TokenCognito,
        http: httpx.Client,
        reintentos: int = 3,
        espera_base: float = 1.0,
        dormir=time.sleep,
    ) -> None:
        self._url = url_ingesta
        self._token = token
        self._http = http
        self._reintentos = reintentos
        self._espera = espera_base
        self._dormir = dormir

    def sincronizar_producto(self, payload: dict) -> dict:
        mensaje = sobre(payload)
        renovado = False
        ultimo_error = ""

        for intento in range(1, self._reintentos + 1):
            try:
                r = self._http.post(
                    self._url,
                    json=mensaje,
                    headers={"Authorization": f"Bearer {self._token.obtener(forzar=renovado)}"},
                )
            except httpx.TimeoutException:
                ultimo_error = "timeout"
            except httpx.TransportError as exc:
                ultimo_error = f"error de red: {exc.__class__.__name__}"
            else:
                if r.status_code == 200:
                    log.debug("Encolada %s/%s eventId=%s", payload.get("store"),
                              payload.get("externalId"), mensaje["eventId"])
                    return {"eventId": mensaje["eventId"], "encolado": True}
                if r.status_code == 401 and not renovado:
                    renovado = True
                    log.info("La ingesta respondio 401; se pide un token nuevo y se reintenta")
                    continue
                if 400 <= r.status_code < 500:
                    raise ErrorIngesta(
                        f"/ingesta rechazo {payload.get('store')}/{payload.get('externalId')} "
                        f"con HTTP {r.status_code}"
                    )
                ultimo_error = f"HTTP {r.status_code}"

            if intento < self._reintentos:
                log.warning("Ingesta fallo (%s), intento %s de %s",
                            ultimo_error, intento, self._reintentos)
                self._dormir(self._espera * intento)

        raise ErrorIngesta(
            f"/ingesta no respondio para {payload.get('store')}/{payload.get('externalId')} "
            f"tras {self._reintentos} intentos ({ultimo_error})"
        )

    def close(self) -> None:
        self._http.close()

    def __enter__(self) -> IngestaColaClient:
        return self

    def __exit__(self, exc_type, exc_value, traceback) -> None:
        self.close()
