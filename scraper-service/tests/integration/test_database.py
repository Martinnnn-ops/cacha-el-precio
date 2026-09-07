# tests/integration/test_database.py
"""
Prueba el repositorio contra un PostgreSQL de verdad.

Se salta sola si no hay base disponible, para que la suite siga
corriendo en una maquina limpia. Para ejecutarla:

    docker compose up -d db
    DATABASE_URL=postgresql://cacha:cacha@localhost:5432/cacha_el_precio pytest tests/integration
"""

from __future__ import annotations

import os
from datetime import UTC, datetime, timedelta

import pytest

from scraper.domain.product import Product

# DATABASE_URL_REAL la fija tests/conftest.py antes de que el
# aislamiento de las unitarias vacie DATABASE_URL.
URL = os.getenv("DATABASE_URL_REAL") or os.getenv("DATABASE_URL")

psycopg = pytest.importorskip("psycopg", reason="psycopg no instalado; pip install -e '.[db]'")
pytestmark = pytest.mark.skipif(not URL, reason="DATABASE_URL no definida")


@pytest.fixture
def repo():
    from scraper.infrastructure.database.repositories.postgres_product_repository import (
        RepositorioPostgres,
    )

    con = psycopg.connect(URL)
    r = RepositorioPostgres(con)
    r.crear_esquema()
    # Cada prueba parte de cero para no arrastrar estado entre casos.
    with con.cursor() as cur:
        cur.execute("DELETE FROM price_history")
        cur.execute("DELETE FROM products")
    con.commit()
    yield r
    con.close()


def producto(precio=79990, disponible=True, minutos=0) -> Product:
    return Product(
        external_id="TEST-1", store="pruebas", name="Zapatilla de prueba",
        brand="Marca", price=precio, product_url="https://ejemplo.cl/1",
        available=disponible, scraped_at=datetime.now(UTC) + timedelta(minutes=minutos),
    )


def test_crear_esquema_es_idempotente(repo):
    repo.crear_esquema()          # segunda vez: no debe fallar
    repo.crear_esquema()


def test_guardar_y_recuperar(repo):
    assert repo.guardar(producto()) is True
    p = repo.obtener("pruebas", "TEST-1")
    assert p is not None
    assert p.price == 79990 and p.brand == "Marca" and p.available is True


def test_guarda_referencias_de_imagen_procesada(repo):
    p = producto().model_copy(update={
        "source_image_url": "https://cdn.example/original.jpg",
        "image_url": "https://bucket.example/products/test/detail.webp",
        "image_card_url": "https://bucket.example/products/test/card.webp",
        "image_detail_url": "https://bucket.example/products/test/detail.webp",
        "image_card_key": "products/test/card.webp",
        "image_detail_key": "products/test/detail.webp",
        "image_hash": "a" * 64,
    })
    repo.guardar(p)

    stored = repo.obtener("pruebas", "TEST-1")
    assert stored is not None
    assert stored.source_image_url == p.source_image_url
    assert stored.image_card_key == p.image_card_key
    assert stored.image_detail_url == p.image_detail_url
    assert stored.image_hash == p.image_hash


def test_upsert_no_duplica_el_producto(repo):
    repo.guardar(producto(79990, minutos=0))
    repo.guardar(producto(69990, minutos=1))
    p = repo.obtener("pruebas", "TEST-1")
    assert p.price == 69990       # se actualizo, no se inserto otro


def test_historial_solo_con_cambios(repo):
    repo.guardar(producto(79990, minutos=0))
    repo.guardar(producto(79990, minutos=1))     # mismo precio: no aporta fila
    repo.guardar(producto(59990, minutos=2))
    assert [o.price for o in repo.historial("pruebas", "TEST-1")] == [59990, 79990]


def test_producto_inexistente(repo):
    assert repo.obtener("pruebas", "NO-EXISTE") is None
    assert repo.historial("pruebas", "NO-EXISTE") == []
