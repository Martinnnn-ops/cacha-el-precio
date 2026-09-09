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
    assert not p.brand.startswith("{")  # no se colo el dict como texto


def test_detecta_producto_sin_stock(parser, sin_stock):
    """
    Es lo que Converse no publicaba. Aqui el campo available deja de
    ser un valor por defecto y refleja la realidad de la tienda.
    """
    p = parser.parse_product(sin_stock)
    assert p is not None
    assert p.available is False
    assert p.price > 0  # sin stock pero con precio publicado


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


def test_extrae_varios_productos_del_listado(parser):
    html = """
    <script id="__NEXT_DATA__" type="application/json">
    {"props":{"pageProps":{"results":[
      {
        "skuId":"SKU-1",
        "displayName":"Zapatilla Urbana Mujer",
        "url":"https://www.falabella.com/falabella-cl/product/1/zapatilla",
        "brand":"CONVERSE",
        "mediaUrls":["https://media.falabella.com/sku-1/public"],
        "prices":[
          {"type":"eventPrice","crossed":false,"price":["49.990"]},
          {"type":"normalPrice","crossed":true,"price":["69.990"]}
        ],
        "variants":[{"type":"COLOR","options":[{
          "label":"Negro",
          "sizes":[
            {"value":"38","available":true},
            {"value":"42.5","available":true},
            {"value":"44","available":false}
          ]
        }]}]
      },
      {"skuId":"SIN-PRECIO","displayName":"Polera","url":"https://example/polera"}
    ]}}}
    </script>
    """

    productos = parser.parse_listing(html)

    assert len(productos) == 1
    assert productos[0].external_id == "SKU-1"
    assert productos[0].price == 49990
    assert productos[0].sizes == ["38", "42.5"]
    assert productos[0].source_image_url == "https://media.falabella.com/sku-1/public"
