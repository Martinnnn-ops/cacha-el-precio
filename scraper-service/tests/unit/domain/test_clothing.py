"""Reglas que mantienen el catalogo limitado a ropa y calzado."""

import pytest

from scraper.domain.clothing import es_vestimenta, texto_parece_vestimenta
from scraper.domain.product import Product


def producto(nombre: str, url: str = "https://tienda.cl/producto") -> Product:
    return Product(
        external_id="1",
        store="tienda",
        name=nombre,
        brand="Marca",
        price=19990,
        product_url=url,
    )


@pytest.mark.parametrize(
    ("nombre", "url"),
    [
        ("Polera manga corta", "https://tienda.cl/1"),
        ("Pantalon de hombre", "https://tienda.cl/1"),
        ("Zapatillas running", "https://tienda.cl/1"),
        ("Converse Chuck Taylor", "https://tienda.cl/1"),
        ("Producto Nike", "https://tienda.cl/chaqueta-nike-1"),
        ("Cardigan tejido", "https://tienda.cl/1"),
        ("Bikini deportivo", "https://tienda.cl/1"),
        ("Cinturón de cuero", "https://tienda.cl/1"),
    ],
)
def test_acepta_ropa_y_calzado(nombre, url):
    assert es_vestimenta(producto(nombre, url)) is True


@pytest.mark.parametrize(
    "nombre",
    [
        "Control PS5 DualSense Sony",
        "Contenedor hermetico de vidrio",
        "Toalla de microfibra",
        "Billetera hombre",
        "Televisor LED 55 pulgadas",
    ],
)
def test_rechaza_productos_fuera_de_vestimenta(nombre):
    assert es_vestimenta(producto(nombre)) is False


def test_ropa_de_cama_no_pasa_por_contener_ropa():
    assert es_vestimenta(producto("Set de ropa de cama")) is False


@pytest.mark.parametrize(
    ("nombre", "url"),
    [
        # El nombre no dice la edad; la tienda la marca solo en la ruta.
        ("Polera estampada", "https://falabella.cl/nino/poleras/123"),
        ("Zapatillas con luces", "https://paris.cl/ninas/calzado/123"),
        ("Conjunto algodon", "https://ripley.cl/bebe/conjuntos/123"),
        ("Buzo deportivo", "https://hites.cl/infantil/buzos/123"),
    ],
)
def test_rechaza_prendas_infantiles_marcadas_solo_en_la_url(nombre, url):
    assert es_vestimenta(producto(nombre, url)) is False


@pytest.mark.parametrize(
    "nombre",
    [
        "Mochila escolar reforzada",
        "Perfume de mujer 100ml",
        "Peluche de oso gigante",
        "Cartera de mano",
    ],
)
def test_rechaza_no_vestimenta_que_menciona_prendas(nombre):
    # La ficha de estos productos suele nombrar prendas ("combina con tu
    # polera", "ideal para regalar"), y antes se colaban por eso.
    assert es_vestimenta(producto(nombre)) is False


def test_prenda_de_adulto_con_nombre_de_corte_no_se_confunde_con_edad():
    # "baby tee" / "baby doll" son cortes de ropa de adulto, no edades.
    assert es_vestimenta(producto("Polera baby tee mujer")) is True


def test_filtra_slugs_de_sitemap_antes_de_descargar_fichas():
    assert texto_parece_vestimenta("https://paris.cl/poleron-hombre-123.html") is True
    assert texto_parece_vestimenta("https://paris.cl/refrigerador-lg-123.html") is False
