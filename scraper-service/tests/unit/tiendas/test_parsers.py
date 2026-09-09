# tests/unit/tiendas/test_parsers.py
"""
Un mismo contrato para todas las tiendas, comprobado contra fichas
reales guardadas en tests/fixtures.

Se parametriza en vez de escribir cuatro archivos casi identicos: lo
que hay que garantizar es lo mismo en todas, y asi anadir una tienda
nueva es una linea.
"""

from pathlib import Path

import pytest

from scraper.domain.clothing import es_vestimenta
from scraper.scrapers.hites.parser import HitesParser
from scraper.scrapers.paris.parser import ParisParser
from scraper.scrapers.ripley.parser import RipleyParser
from scraper.scrapers.sparta.parser import SpartaParser

FIXTURES = Path(__file__).parents[2] / "fixtures"

TIENDAS = [
    pytest.param(ParisParser, "paris", "MKVAN0WH9S", 21990, "Fox", id="paris"),
    pytest.param(RipleyParser, "ripley", "2000380632868", 69990, "SONY", id="ripley"),
    pytest.param(HitesParser, "hites", "937898001", 4500, "ROLLY GO", id="hites"),
    pytest.param(SpartaParser, "sparta", "x-61200000TMICROFS2400", 2990, "ZVibes", id="sparta"),
]


def html_de(tienda: str) -> str:
    return (FIXTURES / tienda / "product.html").read_text(encoding="utf-8", errors="replace")


@pytest.mark.parametrize("Parser,tienda,sku,precio,marca", TIENDAS)
def test_extrae_el_producto(Parser, tienda, sku, precio, marca):
    p = Parser().parse_product(html_de(tienda))
    assert p is not None, f"{tienda}: no se extrajo producto"
    assert p.store == tienda
    assert p.external_id == sku
    assert p.price == precio
    assert p.brand == marca
    assert p.currency == "CLP"
    assert p.name
    assert p.product_url.startswith("https://")


@pytest.mark.parametrize("Parser,tienda,sku,precio,marca", TIENDAS)
def test_pagina_sin_datos_devuelve_none(Parser, tienda, sku, precio, marca):
    assert Parser().parse_product("<html><body>nada</body></html>") is None


def test_ripley_encuentra_el_product_dentro_de_graph():
    """
    Ripley anida el Product en "@graph" y ademas publica OTRO bloque
    Product que solo lleva valoraciones. Quedarse con el primero daba
    un producto vacio.
    """
    p = RipleyParser().parse_product(html_de("ripley"))
    assert p.name and p.price > 0
    assert "CONTROL" in p.name.upper()


@pytest.mark.parametrize(
    ("Parser", "tienda", "esperado"),
    [
        (ParisParser, "paris", True),
        (RipleyParser, "ripley", False),
        (HitesParser, "hites", False),
        (SpartaParser, "sparta", False),
    ],
)
def test_clasifica_las_fichas_reales(Parser, tienda, esperado):
    producto = Parser().parse_product(html_de(tienda))
    assert producto is not None
    assert es_vestimenta(producto) is esperado


def test_sparta_saca_sku_y_marca_del_bloque_de_analytics():
    """
    Sparta es la unica que no publica schema.org/Product: el sku y la
    marca salen de su bloque de Analytics, no del JSON-LD.
    """
    p = SpartaParser().parse_product(html_de("sparta"))
    assert p.external_id == "x-61200000TMICROFS2400"  # no la URL
    assert p.brand == "ZVibes"  # no "sparta"


def test_sparta_usa_el_precio_final_y_no_el_de_lista():
    """
    Su bloque de Analytics trae 6990 de lista y 4000 de descuento;
    Open Graph trae 2990, que es lo que se paga. Para comparar precios
    interesa el segundo.
    """
    p = SpartaParser().parse_product(html_de("sparta"))
    assert p.price == 2990


def test_el_filtro_de_sparta_separa_fichas_de_categorias():
    """
    El sitemap de Sparta mezcla fichas con categorias y paginas
    estaticas. Sin filtrar, se gastaba una peticion por cada pagina
    que no era producto y el barrido devolvia 0 guardados.
    """
    from scraper.scrapers.sparta.scraper import SpartaScraper

    urls = [
        # fichas: el tramo final es el SKU y lleva digitos
        "https://sparta.cl/toalla-zvibes-microfibra-melange-turquesa-s-61200000tmicrofs2400.html",
        "https://sparta.cl/mug-camelback-thrive-vss-1l-verde-69000029833010011200.html",
        # no fichas: slugs de categoria, solo letras
        "https://sparta.cl/",
        "https://sparta.cl/zapatillas.html",
        "https://sparta.cl/zapatillas/zapatillas-running.html",
        "https://sparta.cl/deportes-individuales.html",
        "https://sparta.cl/terminos-y-condiciones.html",
    ]
    fichas = SpartaScraper.urls_de_producto(urls)
    assert len(fichas) == 2
    assert all("61200000" in u or "69000029" in u for u in fichas)


def test_las_tiendas_sin_filtro_devuelven_todo():
    """Falabella y Paris publican sitemaps solo de fichas: no filtran."""
    from scraper.scrapers.falabella.scraper import FalabellaScraper

    urls = ["https://www.falabella.com/falabella-cl/product/1/x/2"]
    assert FalabellaScraper.urls_de_producto(urls) == urls


def test_precio_cero_se_trata_como_sin_precio():
    """
    Sparta publica product:price:amount=0 en los productos agotados.
    Guardarlos con precio 0 los dejaria apareciendo como los mas
    baratos de toda la web. Sin precio real, no se guarda.
    """
    agotado = """
    <html><head>
      <meta property="og:title" content="Zapatilla agotada">
      <meta property="og:url" content="https://sparta.cl/x-123.html">
      <meta property="product:price:amount" content="0">
      <meta property="product:price:currency" content="CLP">
    </head></html>
    """
    assert SpartaParser().parse_product(agotado) is None


def test_precio_valido_sigue_pasando():
    """Control: la regla del 0 no debe descartar precios legitimos."""
    ok = """
    <html><head>
      <meta property="og:title" content="Zapatilla">
      <meta property="og:url" content="https://sparta.cl/x-123.html">
      <meta property="product:price:amount" content="1">
    </head></html>
    """
    p = SpartaParser().parse_product(ok)
    assert p is not None and p.price == 1


def test_lee_product_anidado_fuera_de_graph_y_tallas_mixtas():
    html = """
    <script type="application/ld+json">
    {
      "@context": "https://schema.org",
      "mainEntity": {
        "@type": "ProductGroup",
        "name": "Zapatilla Chuck Taylor",
        "brand": {"name": "Converse"},
        "sku": "CT-1",
        "image": "https://img.example/ct.webp",
        "hasVariant": [
          {"@type": "Product", "size": "38", "offers": {"price": "79.990", "priceCurrency": "CLP"}},
          {"@type": "Product", "size": "42,5",
           "offers": {"price": "79.990", "priceCurrency": "CLP"}},
          {"@type": "Product", "size": ["S", "M"]}
        ]
      }
    }
    </script>
    """

    p = ParisParser().parse_product(html, "https://paris.cl/chuck-taylor")

    assert p is not None
    assert p.price == 79990
    assert p.sizes == ["38", "42.5", "S", "M"]
    assert p.source_image_url == "https://img.example/ct.webp"


def test_lee_low_price_de_aggregate_offer():
    html = """
    <script type="application/ld+json">
      {"@type":"Product", "name":"Polera", "sku":"P-1",
       "offers":{"@type":"AggregateOffer", "lowPrice":"$12.990", "priceCurrency":"CLP"}}
    </script>
    """

    p = ParisParser().parse_product(html, "https://paris.cl/polera")

    assert p is not None and p.price == 12990
