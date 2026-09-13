"""La categoria sale del nombre del producto, no del maquetado."""

from scraper.domain.categoria import FALLBACK, categoria_de, clasificacion_de
from scraper.domain.product import Product


def prod(nombre):
    return Product(
        external_id="X",
        store="paris",
        name=nombre,
        brand="",
        price=100,
        product_url="https://paris.cl/x",
    )


def test_clasifica_desde_el_nombre():
    casos = {
        "Poleron CK Institutional Blanco": "Polerones",
        "Zapatilla Urbana Go Walk Hombre": "Zapatillas",
        "Camisa Hombre Silver Ridge": "Camisas",
        "Pijama Mujer 8542": "Pijamas",
        "Pantalon Chino Slim": "Pantalones",
        "Botin Chelsea Cuero": "Botas",
        "Sandalia Mujer Talon": "Sandalias",
        "Bikini deportivo": "Trajes de baño",
        "Cinturón de cuero": "Accesorios",
        "Gorro tejido unisex": "Gorros",
        "Calcetines deportivos": "Calcetines",
        "Short denim mujer": "Shorts",
        "Boxer algodón hombre": "Ropa interior masculina",
        "Calzón encaje mujer": "Ropa interior femenina",
    }
    for nombre, esperada in casos.items():
        assert categoria_de(prod(nombre)) == esperada, nombre


def test_sin_match_usa_fallback():
    assert categoria_de(prod("Gato Loco Tapiz")) == FALLBACK


def test_running_no_convierte_una_polera_en_zapatilla():
    assert categoria_de(prod("Polera Running Dri-fit Hombre")) == "Poleras"


def test_clasificacion_separa_tipo_zona_y_genero():
    casos = {
        "Gorro lana mujer": ("Gorros", "Cabeza", "Mujer"),
        "Polera básica unisex": ("Poleras", "Torso", "Unisex"),
        "Polerón hombre": ("Polerones", "Torso", "Hombre"),
        "Calcetines hombre": ("Calcetines", "Pies", "Hombre"),
        "Calzón encaje mujer": ("Ropa interior femenina", "Piernas", "Mujer"),
        "Boxer algodón hombre": ("Ropa interior masculina", "Piernas", "Hombre"),
    }

    for nombre, esperado in casos.items():
        clasificacion = clasificacion_de(prod(nombre))
        assert (
            clasificacion.categoria,
            clasificacion.zona_corporal,
            clasificacion.genero,
        ) == esperado


def test_clasificacion_distingue_capas_en_la_misma_zona():
    casos = {
        "Polera básica mujer": ("Torso", "Base"),
        "Polerón con capucha mujer": ("Torso", "Abrigo"),
        "Calcetines deportivos": ("Pies", "Calcetería"),
        "Zapatillas urbanas": ("Pies", "Calzado"),
        "Boxer algodón hombre": ("Piernas", "Ropa interior"),
        "Pantalón chino hombre": ("Piernas", "Inferior"),
    }

    for nombre, esperado in casos.items():
        clasificacion = clasificacion_de(prod(nombre))
        assert (clasificacion.zona_corporal, clasificacion.capa) == esperado


def test_genero_del_titulo_gana_a_una_descripcion_generica():
    producto = prod("Gorro lana mujer").model_copy(
        update={"description": "Accesorio de estilo unisex"}
    )

    assert clasificacion_de(producto).genero == "Mujer"
