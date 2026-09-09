"""La identidad canónica debe coincidir entre tiendas."""

from scraper.domain.product_identity import canonical_key


def test_ignora_marca_genero_color_y_talla_del_nombre():
    assert (
        canonical_key(
            "Converse",
            "Zapatilla Converse Chuck Taylor Mujer Negra talla 38",
        )
        == "converse:chuck-taylor"
    )


def test_conserva_los_tokens_del_modelo_en_orden_estable():
    assert canonical_key("Nike", "Air Max 90") == "nike:90-air-max"
    assert canonical_key("Nike", "Air Max 90") == canonical_key("Nike", "90 Max Air")
