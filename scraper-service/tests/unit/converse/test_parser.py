# tests/unit/converse/test_parser.py
"""
Prueba el parser contra el HTML real guardado en tests/fixtures.

Se usa un fixture y no la web en vivo a proposito: asi la prueba no
depende de internet ni de que la tienda cambie el catalogo, y falla
solo cuando se rompe el parser.
"""

from pathlib import Path

import pytest

from scraper.scrapers.converse.parser import ConverseParser

FIXTURE = Path(__file__).parents[2] / "fixtures" / "converse" / "product.html"


@pytest.fixture(scope="module")
def html() -> str:
    return FIXTURE.read_text(encoding="utf-8", errors="replace")


@pytest.fixture
def parser() -> ConverseParser:
    return ConverseParser()


def test_extrae_el_producto_completo(parser, html):
    p = parser.parse_product(html)
    assert p is not None
    assert p.name == "Converse x Dragon Ball Z Goku Chuck Taylor All Star"
    assert p.external_id == "A21842C-800"
    assert p.price == 79990
    assert p.currency == "CLP"
    assert p.store == "converse"
    assert p.brand == "Converse"
    assert p.product_url.startswith("https://www.converse.cl/")
    assert p.source_image_url is not None and p.source_image_url.endswith(".jpg")
    assert p.image_url is None
    assert p.description


def test_el_precio_es_el_del_producto_y_no_el_de_un_recomendado(parser, html):
    """
    La ficha lleva un carrusel de recomendados con sus propios
    data-price-amount (29990, 37990, 39990, 42990, 44990). Este es el
    error que cometeria un selector CSS de precio.
    """
    p = parser.parse_product(html)
    assert p.price not in {29990, 37990, 39990, 42990, 44990}


def test_devuelve_none_si_no_es_una_ficha_de_producto(parser):
    assert parser.parse_product("<html><body>hola</body></html>") is None


def test_un_json_ld_roto_no_revienta_el_parser(parser):
    roto = """
    <html><head>
      <script type="application/ld+json">{ esto no es json }</script>
      <meta property="og:title" content="Zapatilla de prueba">
      <meta property="og:url" content="https://www.converse.cl/x">
      <meta property="product:price:amount" content="19990">
    </head></html>
    """
    p = parser.parse_product(roto)
    # Se cae a Open Graph en vez de propagar la excepcion.
    assert p is not None
    assert p.name == "Zapatilla de prueba"
    assert p.price == 19990


def test_usa_la_url_pasada_si_la_pagina_no_trae_og_url(parser):
    sin_url = """
    <html><head>
      <meta property="og:title" content="Zapatilla">
      <meta property="product:price:amount" content="9990">
    </head></html>
    """
    p = parser.parse_product(sin_url, url="https://www.converse.cl/manual")
    assert p is not None
    assert p.product_url == "https://www.converse.cl/manual"
    # Sin sku en el JSON-LD, el id se deriva del ultimo tramo de la URL.
    assert p.external_id == "manual"
