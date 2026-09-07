# tests/unit/falabella/test_parser.py
"""Parser de Falabella contra fichas reales guardadas."""

from pathlib import Path

import pytest

from scraper.scrapers.falabella.parser import FalabellaParser

DIR = Path(__file__).parents[2] / "fixtures" / "falabella"


@pytest.fixture(scope="module")
def con_stock() -> str:
    return (DIR / "product_instock.html").read_text(encoding="utf-8", errors="replace")


@pytest.fixture(scope="module")
def sin_stock() -> str:
    return (DIR / "product_outofstock.html").read_text(encoding="utf-8", errors="replace")


@pytest.fixture
def parser() -> FalabellaParser:
    return FalabellaParser()


def test_extrae_producto_con_stock(parser, con_stock):
    p = parser.parse_product(con_stock)
    assert p is not None
    assert p.store == "falabella"
    assert p.price > 0
    assert p.currency == "CLP"
    assert p.available is True
    assert p.external_id and p.name and p.brand
    assert p.product_url.startswith("https://www.falabella.com/")


def test_marca_desanidada(parser, con_stock):
    """En Falabella brand llega como {"@type":"Brand","name":"..."}."""
    p = parser.parse_product(con_stock)
    assert p.brand
    assert not p.brand.startswith("{")     # no se colo el dict como texto


def test_detecta_producto_sin_stock(parser, sin_stock):
    """
    Es lo que Converse no publicaba. Aqui el campo available deja de
    ser un valor por defecto y refleja la realidad de la tienda.
    """
    p = parser.parse_product(sin_stock)
    assert p is not None
    assert p.available is False
    assert p.price > 0                     # sin stock pero con precio publicado


def test_offers_vacio_no_produce_producto(parser):
    """
    Falabella devuelve "offers": [] en descatalogados. Sin precio no
    se guarda: una web de comparar precios no quiere filas sin precio.
    """
    html = """
    <html><head>
      <script type="application/ld+json">
      {"@context":"https://schema.org","@type":"Product","name":"Algo","sku":"1","offers":[]}
      </script>
      <meta property="og:url" content="https://www.falabella.com/x">
    </head></html>
    """
    assert parser.parse_product(html) is None
