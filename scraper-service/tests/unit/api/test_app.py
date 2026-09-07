# tests/unit/api/test_app.py
"""
Pruebas de la API con el cliente de prueba de FastAPI: sin levantar
servidor y sin red, porque el repositorio por defecto es el de memoria.
"""

from datetime import UTC, datetime, timedelta

import pytest
from fastapi.testclient import TestClient

from scraper.api.app import _repo, app
from scraper.domain.product import Product


@pytest.fixture
def cliente() -> TestClient:
    return TestClient(app)


@pytest.fixture
def producto_guardado():
    p = Product(external_id="A1", store="converse", name="Zapatilla", brand="Converse",
                price=79990, product_url="https://www.converse.cl/a1")
    _repo.guardar(p)
    # Un segundo precio, mas tarde: es lo que pasa en un barrido real.
    _repo.guardar(p.model_copy(update={
        "price": 59990,
        "scraped_at": datetime.now(UTC) + timedelta(hours=1),
    }))
    return p


def test_health(cliente):
    r = cliente.get("/health")
    assert r.status_code == 200
    d = r.json()
    assert d["estado"] == "ok"
    assert "converse" in d["tiendas"] and "falabella" in d["tiendas"]


def test_tiendas_indica_cual_tiene_sitemap(cliente):
    d = cliente.get("/tiendas").json()
    por_nombre = {t["tienda"]: t for t in d}
    assert por_nombre["falabella"]["sitemap"]          # Falabella publica sitemap
    assert por_nombre["converse"]["sitemap"] is None   # Converse no


def test_producto_y_su_historial(cliente, producto_guardado):
    r = cliente.get("/productos/converse/A1")
    assert r.status_code == 200
    assert r.json()["price"] == 59990                  # el ultimo precio visto

    h = cliente.get("/productos/converse/A1/historial").json()
    assert [o["price"] for o in h] == [59990, 79990]   # mas reciente primero


def test_producto_inexistente_da_404(cliente):
    assert cliente.get("/productos/converse/NO-EXISTE").status_code == 404


def test_scrape_de_tienda_desconocida_da_404(cliente):
    assert cliente.post("/scrape/nike").status_code == 404


def test_scrape_sin_sitemap_da_400(cliente):
    """
    Converse no publica sitemap utilizable, asi que la API lo dice en
    vez de fallar de forma opaca.
    """
    r = cliente.post("/scrape/converse")
    assert r.status_code == 400
    assert "sitemap" in r.json()["detail"]


def test_status_sin_barridos_da_404(cliente):
    assert cliente.get("/scrape/falabella/status").status_code == 404
