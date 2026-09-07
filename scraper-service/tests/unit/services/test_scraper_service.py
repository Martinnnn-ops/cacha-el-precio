# tests/unit/services/test_scraper_service.py
"""
El servicio se prueba entero sin red y sin base de datos, usando el
repositorio en memoria y scrapers falsos.
"""

import pytest

from scraper.domain.product import Product
from scraper.infrastructure.database.repositories.product_repository import (
    RepositorioEnMemoria,
)
from scraper.services.scraper_service import ScraperService


def prod(external_id="A1", precio=79990, store="converse"):
    return Product(external_id=external_id, store=store, name="Zapatilla",
                   brand="Converse", price=precio, product_url=f"https://x/{external_id}")


class ScraperFalso:
    def __init__(self, por_url=None, revienta_en=None):
        self.por_url = por_url or {}
        self.revienta_en = revienta_en or set()

    def scrape(self, url):
        if url in self.revienta_en:
            raise RuntimeError("caida simulada")
        return self.por_url.get(url, [])


def test_barrido_normal():
    repo = RepositorioEnMemoria()
    s = ScraperService({"converse": ScraperFalso({"u1": [prod("A1")], "u2": [prod("A2")]})}, repo)
    r = s.ejecutar("converse", ["u1", "u2"])
    assert r.productos_extraidos == 2
    assert r.guardados == 2
    assert r.cambios_de_precio == 2      # primera vez, todo es cambio
    assert r.urls_fallidas == []
    assert len(repo) == 2


def test_una_url_rota_no_aborta_el_barrido():
    """Lo importante en un barrido nocturno de cientos de productos."""
    repo = RepositorioEnMemoria()
    sc = ScraperFalso({"ok1": [prod("A1")], "ok2": [prod("A2")]}, revienta_en={"malo"})
    s = ScraperService({"converse": sc}, repo)
    r = s.ejecutar("converse", ["ok1", "malo", "ok2"])
    assert r.guardados == 2
    assert r.urls_fallidas == ["malo"]
    assert r.urls_ok == 2


def test_url_sin_producto_no_es_un_fallo_de_descarga():
    """
    Descatalogado o sin precio publicado no es lo mismo que "no pude
    descargar". Mezclarlos oculta que la tienda cambio el maquetado
    detras de un monton de productos retirados.
    """
    repo = RepositorioEnMemoria()
    s = ScraperService({"converse": ScraperFalso({"vacia": []})}, repo)
    r = s.ejecutar("converse", ["vacia"])
    assert r.sin_producto == ["vacia"]
    assert r.urls_fallidas == []
    assert r.urls_ok == 1              # se descargo bien
    assert r.guardados == 0


def test_separa_los_dos_tipos_de_problema():
    repo = RepositorioEnMemoria()
    sc = ScraperFalso({"ok": [prod("A1")], "vacia": []}, revienta_en={"caida"})
    s = ScraperService({"converse": sc}, repo)
    r = s.ejecutar("converse", ["ok", "vacia", "caida"])
    assert r.guardados == 1
    assert r.sin_producto == ["vacia"]
    assert r.urls_fallidas == ["caida"]
    assert "1 sin producto" in r.resumen()
    assert "1 fallos de descarga" in r.resumen()


def test_segundo_barrido_sin_cambios_no_cuenta_cambios_de_precio():
    repo = RepositorioEnMemoria()
    sc = ScraperFalso({"u1": [prod("A1", 79990)]})
    s = ScraperService({"converse": sc}, repo)
    s.ejecutar("converse", ["u1"])
    r = s.ejecutar("converse", ["u1"])
    assert r.guardados == 1
    assert r.cambios_de_precio == 0
    assert len(repo.historial("converse", "A1")) == 1


def test_descarta_productos_que_no_son_vestimenta():
    repo = RepositorioEnMemoria()
    control = prod("PS5").model_copy(update={"name": "Control PS5 DualSense"})
    s = ScraperService({"converse": ScraperFalso({"u1": [control]})}, repo)

    r = s.ejecutar("converse", ["u1"])

    assert r.productos_extraidos == 1
    assert r.productos_descartados == 1
    assert r.guardados == 0
    assert len(repo) == 0


def test_reporta_fallos_de_guardado():
    class RepoRoto:
        def guardar(self, producto):
            raise RuntimeError("base no disponible")

    s = ScraperService({"converse": ScraperFalso({"u1": [prod("A1")]})}, RepoRoto())
    r = s.ejecutar("converse", ["u1"])

    assert r.guardados == 0
    assert r.errores_guardado == ["converse/A1"]
    assert "1 fallos de guardado" in r.resumen()


def test_sincroniza_lo_guardado():
    class SyncFalso:
        def __init__(self):
            self.vistos = []

        def sincronizar(self, producto):
            self.vistos.append(producto.external_id)
            return True

    sync = SyncFalso()
    s = ScraperService({"converse": ScraperFalso({"u1": [prod("A1")]})},
                       RepositorioEnMemoria(), sync_service=sync)
    r = s.ejecutar("converse", ["u1"])

    assert r.sincronizados == 1
    assert r.errores_sync == []
    assert sync.vistos == ["A1"]
    assert "1 sincronizados" in r.resumen()


def test_fallo_de_sync_no_pierde_el_producto():
    class SyncRoto:
        def sincronizar(self, producto):
            raise RuntimeError("Product Service no disponible")

    repo = RepositorioEnMemoria()
    s = ScraperService({"converse": ScraperFalso({"u1": [prod("A1")]})},
                       repo, sync_service=SyncRoto())
    r = s.ejecutar("converse", ["u1"])

    assert r.guardados == 1
    assert r.sincronizados == 0
    assert r.errores_sync == ["converse/A1"]
    assert repo.obtener("converse", "A1") is not None
    assert "1 fallos de sync" in r.resumen()


def test_respeta_el_retardo_entre_urls(monkeypatch):
    pausas = []
    monkeypatch.setattr("scraper.services.scraper_service.time.sleep", pausas.append)
    sc = ScraperFalso({"u1": [prod("A1")], "u2": [prod("A2")]})

    ScraperService({"converse": sc}, RepositorioEnMemoria(), delay=1.5).ejecutar(
        "converse", ["u1", "u2"]
    )

    assert pausas == [1.5]


def test_fallo_de_imagen_no_pierde_el_producto():
    class ImagenRota:
        def process(self, product, previous=None):
            raise RuntimeError("S3 no disponible")

    repo = RepositorioEnMemoria()
    producto = prod("A1").model_copy(update={
        "source_image_url": "https://cdn.example/image.jpg",
    })
    service = ScraperService(
        {"converse": ScraperFalso({"u1": [producto]})},
        repo,
        image_service=ImagenRota(),
    )

    result = service.ejecutar("converse", ["u1"])

    assert result.guardados == 1
    assert result.errores_imagen == ["converse/A1"]
    assert repo.obtener("converse", "A1") is not None


def test_fallo_de_imagen_conserva_la_anterior_para_reintentar():
    class ImagenRota:
        def process(self, product, previous=None):
            raise RuntimeError("S3 no disponible")

    repo = RepositorioEnMemoria()
    anterior = prod("A1").model_copy(update={
        "source_image_url": "https://cdn.example/old.jpg",
        "image_url": "https://bucket.example/old-detail.webp",
        "image_card_url": "https://bucket.example/old-card.webp",
        "image_detail_url": "https://bucket.example/old-detail.webp",
        "image_card_key": "products/old-card.webp",
        "image_detail_key": "products/old-detail.webp",
        "image_hash": "a" * 64,
    })
    repo.guardar(anterior)
    nuevo = prod("A1", 69990).model_copy(update={
        "source_image_url": "https://cdn.example/new.jpg",
    })
    service = ScraperService(
        {"converse": ScraperFalso({"u1": [nuevo]})},
        repo,
        image_service=ImagenRota(),
    )

    service.ejecutar("converse", ["u1"])

    guardado = repo.obtener("converse", "A1")
    assert guardado.source_image_url == anterior.source_image_url
    assert guardado.image_detail_key == anterior.image_detail_key
    assert guardado.price == 69990


def test_fuente_ausente_no_borra_la_imagen_anterior():
    repo = RepositorioEnMemoria()
    anterior = prod("A1").model_copy(update={
        "source_image_url": "https://cdn.example/old.jpg",
        "image_url": "https://bucket.example/detail.webp",
        "image_detail_url": "https://bucket.example/detail.webp",
        "image_detail_key": "products/detail.webp",
    })
    repo.guardar(anterior)
    service = ScraperService(
        {"converse": ScraperFalso({"u1": [prod("A1", 69990)]})},
        repo,
        image_service=object(),
    )

    service.ejecutar("converse", ["u1"])

    guardado = repo.obtener("converse", "A1")
    assert guardado.image_detail_key == anterior.image_detail_key
    assert guardado.price == 69990


def test_detecta_la_bajada_de_precio():
    repo = RepositorioEnMemoria()
    sc = ScraperFalso({"u1": [prod("A1", 79990)]})
    s = ScraperService({"converse": sc}, repo)
    s.ejecutar("converse", ["u1"])
    sc.por_url["u1"] = [prod("A1", 59990)]
    r = s.ejecutar("converse", ["u1"])
    assert r.cambios_de_precio == 1
    assert [o.price for o in repo.historial("converse", "A1")] == [59990, 79990]


def test_tienda_desconocida():
    s = ScraperService({}, RepositorioEnMemoria())
    with pytest.raises(KeyError, match="nike"):
        s.ejecutar("nike", ["u1"])


def test_el_resumen_es_legible():
    repo = RepositorioEnMemoria()
    s = ScraperService({"converse": ScraperFalso({"u1": [prod("A1")]})}, repo)
    r = s.ejecutar("converse", ["u1", "otra"])
    # "otra" se descarga bien pero no trae producto: cuenta como URL ok.
    assert "[converse]" in r.resumen()
    assert "2/2 URLs" in r.resumen()
    assert "1 sin producto" in r.resumen()
