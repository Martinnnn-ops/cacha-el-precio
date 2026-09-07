"""Reglas que mantienen el catalogo limitado a ropa y calzado."""

import pytest

from scraper.domain.clothing import es_vestimenta
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
