"""La categoria sale del nombre del producto, no del maquetado."""

from scraper.domain.categoria import FALLBACK, categoria_de
from scraper.domain.product import Product


def prod(nombre):
    return Product(external_id="X", store="paris", name=nombre, brand="",
                   price=100, product_url="https://paris.cl/x")


def test_clasifica_desde_el_nombre():
    casos = {
        "Poleron CK Institutional Blanco": "Polerones",
        "Zapatilla Urbana Go Walk Hombre": "Zapatillas",
        "Camisa Hombre Silver Ridge": "Camisas",
        "Pijama Mujer 8542": "Pijamas",
        "Pantalon Chino Slim": "Pantalones",
        "Botin Chelsea Cuero": "Botas",
        "Sandalia Mujer Talon": "Sandalias",
    }
    for nombre, esperada in casos.items():
        assert categoria_de(prod(nombre)) == esperada, nombre


def test_sin_match_usa_fallback():
    assert categoria_de(prod("Gato Loco Tapiz")) == FALLBACK