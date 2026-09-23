import pytest

from scraper.domain.catalog_policy import clave_generica, marca_catalogo, motivo_infantil


def test_clave_generica_preserva_identidad_opaca_del_sku():
    assert clave_generica("Paris", "SKU-A") != clave_generica("Paris", "A-SKU")
    assert clave_generica("Paris", "SKU-A") != clave_generica("Falabella", "SKU-A")
    assert clave_generica(" Paris ", " SKU-A ") == clave_generica("paris", "SKU-A")


@pytest.mark.parametrize("value", ["", "GENÉRICO", "Sin Marca", "No brand", "S/M", "Genéricas"])
def test_marcas_genericas(value):
    assert marca_catalogo(value) == "Genéricas"


@pytest.mark.parametrize("value", ["Maui", "Nike", "Converse", "Baby Mink", "Marca pequeña"])
def test_conserva_marcas_reales(value):
    assert marca_catalogo(value) == value


@pytest.mark.parametrize(
    "value",
    [
        "Gorro bebé",
        "Polera Niña",
        "Zapatos niños",
        "Kids hoodie",
        "Body newborn",
        "Pijama infantil",
    ],
)
def test_excluye_infantil(value):
    assert motivo_infantil(value)


@pytest.mark.parametrize(
    "value",
    [
        "Polera adolescente",
        "Polerón juvenil",
        "Zapatilla junior",
        "Baby tee mujer",
        "Baby doll mujer",
        "Gorro XS",
        "Polera hombre",
        "Pantalón 14 años",
    ],
)
def test_no_infiere_edad_por_talla_o_corte(value):
    assert motivo_infantil(value) is None
