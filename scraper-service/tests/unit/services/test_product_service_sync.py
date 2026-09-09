"""Pruebas del adaptador entre el scraper y Product Service."""

from scraper.domain.product import Product
from scraper.services.product_service_sync import ProductServiceSync


class ClienteProductServiceFalso:
    """Emula el upsert HTTP del Product Service sin red."""

    def __init__(self):
        self.productos = []
        self.sincronizados = 0

    def sincronizar_producto(self, payload):
        self.sincronizados += 1
        self.productos.append(dict(payload))
        return payload


def producto(
    nombre="Poleron",
    precio=79990,
    store="paris",
    external_id="MK4QJFISTR",
    url="https://paris.cl/x",
    brand="CK",
):
    return Product(
        external_id=external_id,
        store=store,
        name=nombre,
        brand=brand,
        price=precio,
        product_url=url,
    )


def test_adapta_producto_al_contrato_csharp():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(nombre="Poleron CK"))

    assert cliente.sincronizados == 1
    product = cliente.productos[0]
    assert product["name"] == "Poleron CK"
    assert product["price"] == 79990
    assert product["category"] == "Polerones"
    assert product["externalId"] == "MK4QJFISTR"
    assert product["store"] == "paris"
    assert product["sizes"] == []
    assert product["canonicalKey"] == "ck:poleron"


def test_envia_url_imagen_y_marca():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)
    product = producto(nombre="Poleron CK").model_copy(
        update={
            "image_card_url": "https://s3.example/card.webp",
            "image_detail_url": "https://s3.example/detail.webp",
        }
    )

    sync.sincronizar(product)

    enviado = cliente.productos[0]
    assert enviado["url"] == "https://paris.cl/x"
    assert enviado["image"] == "https://s3.example/card.webp"
    assert enviado["brand"] == "CK"


def test_usa_imagen_de_origen_si_s3_no_esta_configurado():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)
    product = producto().model_copy(update={"source_image_url": "https://tienda.cl/foto.jpg"})

    sync.sincronizar(product)

    assert cliente.productos[0]["image"] == "https://tienda.cl/foto.jpg"


def test_reenvia_la_misma_oferta_para_upsert_idempotente():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(nombre="Poleron CK", precio=79990))
    sync.sincronizar(producto(nombre="Poleron CK (2X1)", precio=59990))

    assert cliente.sincronizados == 2
    assert cliente.productos[-1]["price"] == 59990


def test_ids_externos_distintos_se_envian_como_ofertas_distintas():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(external_id="SKU-A"))
    sync.sincronizar(producto(external_id="SKU-B"))

    assert cliente.sincronizados == 2


def test_mismo_id_en_tiendas_distintas_no_se_pisa():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(store="paris"))
    sync.sincronizar(producto(store="converse"))

    assert cliente.sincronizados == 2


def test_mismo_modelo_en_dos_tiendas_comparte_clave_canonica():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(
        producto(
            nombre="Zapatilla Converse Chuck Taylor Negra",
            store="paris",
            brand="Converse",
        )
    )
    sync.sincronizar(
        producto(
            nombre="Converse Chuck Taylor Hombre",
            store="ripley",
            brand="Converse",
        )
    )

    assert cliente.productos[0]["canonicalKey"] == "converse:chuck-taylor"
    assert cliente.productos[1]["canonicalKey"] == "converse:chuck-taylor"


def test_envia_tallas_alfabeticas_y_numericas():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)
    item = producto().model_copy(update={"sizes": ["S", "38", "42.5"]})

    sync.sincronizar(item)

    assert cliente.productos[0]["sizes"] == ["S", "38", "42.5"]
