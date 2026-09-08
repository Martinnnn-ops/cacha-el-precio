"""Pruebas del adaptador entre el scraper y Product Service."""

from scraper.domain.product import Product
from scraper.services.product_service_sync import ProductServiceSync


class ClienteProductServiceFalso:
    """Emula el contrato de Product Service sin red ni SQLite."""

    def __init__(self):
        self.productos = []
        self.creados = 0
        self.actualizados = 0
        self._siguiente_id = 1

    def listar_productos(self):
        return list(self.productos)

    def crear_producto(self, payload):
        self.creados += 1
        product = dict(payload)
        product["id"] = self._siguiente_id
        self._siguiente_id += 1
        self.productos.append(product)
        return product

    def actualizar_producto(self, producto_id, payload):
        self.actualizados += 1
        for product in self.productos:
            if product["id"] == producto_id:
                product.update(payload)
                return product
        raise AssertionError(f"producto {producto_id} no existe")


def producto(
    nombre="Poleron",
    precio=79990,
    store="paris",
    external_id="MK4QJFISTR",
    url="https://paris.cl/x",
):
    return Product(
        external_id=external_id,
        store=store,
        name=nombre,
        brand="CK",
        price=precio,
        product_url=url,
    )


def test_adapta_producto_al_contrato_csharp():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(nombre="Poleron CK"))

    assert cliente.creados == 1
    product = cliente.productos[0]
    assert product["name"] == "Poleron CK"
    assert product["price"] == 79990
    assert product["category"] == "Polerones"
    assert product["externalId"] == "MK4QJFISTR"
    assert product["store"] == "paris"
    assert product["sizes"] == {
        "xs": False,
        "s": False,
        "m": False,
        "l": False,
        "xl": False,
        "xxl": False,
    }


def test_envia_url_imagen_y_marca():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)
    product = producto(nombre="Poleron CK").model_copy(update={
        "image_card_url": "https://s3.example/card.webp",
        "image_detail_url": "https://s3.example/detail.webp",
    })

    sync.sincronizar(product)

    enviado = cliente.productos[0]
    assert enviado["url"] == "https://paris.cl/x"
    assert enviado["image"] == "https://s3.example/card.webp"
    assert enviado["brand"] == "CK"


def test_actualiza_por_tienda_e_id_externo():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(nombre="Poleron CK", precio=79990))
    sync.sincronizar(producto(nombre="Poleron CK (2X1)", precio=59990))

    assert cliente.creados == 1
    assert cliente.actualizados == 1
    assert cliente.productos[0]["price"] == 59990


def test_ids_externos_distintos_crean_productos_distintos():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(external_id="SKU-A"))
    sync.sincronizar(producto(external_id="SKU-B"))

    assert cliente.creados == 2


def test_mismo_id_en_tiendas_distintas_no_se_pisa():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(store="paris"))
    sync.sincronizar(producto(store="converse"))

    assert cliente.creados == 2
