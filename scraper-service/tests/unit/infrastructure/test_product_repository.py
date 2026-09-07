# tests/unit/infrastructure/test_product_repository.py
"""Comportamiento del repositorio, sobre todo la regla del historial."""

from datetime import UTC, datetime, timedelta

import pytest

from scraper.domain.product import Product
from scraper.infrastructure.database.repositories.product_repository import (
    RepositorioEnMemoria,
)


def producto(precio: int = 79990, disponible: bool = True, minutos: int = 0) -> Product:
    return Product(
        external_id="A21842C-800",
        store="converse",
        name="Goku Chuck Taylor",
        brand="Converse",
        price=precio,
        product_url="https://www.converse.cl/x",
        available=disponible,
        scraped_at=datetime.now(UTC) + timedelta(minutes=minutos),
    )


def test_guarda_y_recupera():
    r = RepositorioEnMemoria()
    assert r.guardar(producto()) is True          # primera vez: siempre es cambio
    p = r.obtener("converse", "A21842C-800")
    assert p is not None and p.price == 79990
    assert len(r) == 1


def test_no_duplica_historial_si_el_precio_no_cambia():
    """
    Es la razon de ser de la regla: el job corre varias veces al dia y
    el precio casi nunca cambia. Sin esto la tabla se llena de filas
    identicas y los graficos salen con miles de puntos iguales.
    """
    r = RepositorioEnMemoria()
    for i in range(5):
        r.guardar(producto(minutos=i))
    assert len(r.historial("converse", "A21842C-800")) == 1


def test_anade_historial_cuando_baja_el_precio():
    r = RepositorioEnMemoria()
    r.guardar(producto(79990, minutos=0))
    r.guardar(producto(79990, minutos=1))         # sin cambio
    assert r.guardar(producto(59990, minutos=2)) is True
    h = r.historial("converse", "A21842C-800")
    assert [o.price for o in h] == [59990, 79990]   # mas reciente primero


def test_tambien_registra_el_cambio_de_disponibilidad():
    r = RepositorioEnMemoria()
    r.guardar(producto(79990, disponible=True, minutos=0))
    assert r.guardar(producto(79990, disponible=False, minutos=1)) is True
    assert len(r.historial("converse", "A21842C-800")) == 2


def test_productos_de_tiendas_distintas_no_se_pisan():
    """
    La identidad es (store, external_id). Dos tiendas pueden usar el
    mismo SKU para cosas distintas.
    """
    r = RepositorioEnMemoria()
    a = producto()
    b = producto().model_copy(update={"store": "falabella", "price": 69990})
    r.guardar(a)
    r.guardar(b)
    assert len(r) == 2
    assert r.obtener("converse", "A21842C-800").price == 79990
    assert r.obtener("falabella", "A21842C-800").price == 69990


def test_producto_inexistente_devuelve_none():
    r = RepositorioEnMemoria()
    assert r.obtener("converse", "no-existe") is None
    assert r.historial("converse", "no-existe") == []


def test_repositorio_rechaza_productos_fuera_de_vestimenta():
    r = RepositorioEnMemoria()
    control = producto().model_copy(update={
        "name": "Control PS5 DualSense",
        "product_url": "https://tienda.cl/control-ps5",
    })

    with pytest.raises(ValueError, match="fuera de vestimenta"):
        r.guardar(control)


def test_desempata_por_orden_de_insercion_si_la_fecha_coincide():
    """
    Dos barridos muy seguidos pueden compartir scraped_at. Sin
    desempate, el "precio mas reciente" quedaria indefinido.
    """
    r = RepositorioEnMemoria()
    momento = datetime.now(UTC)
    r.guardar(producto(79990).model_copy(update={"scraped_at": momento}))
    r.guardar(producto(59990).model_copy(update={"scraped_at": momento}))
    assert [o.price for o in r.historial("converse", "A21842C-800")] == [59990, 79990]
