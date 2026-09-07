"""
El sincronizador hacia Product Service se prueba con un cliente falso:
sin red, sin SQLite y sin el microservicio Java, pero con el mismo
comportamiento que su API.
"""

from scraper.domain.product import Product
from scraper.services.product_service_sync import ProductServiceSync


class ClienteProductServiceFalso:
    """Emula lo minimo de la API de Product Service que usa el sync."""

    def __init__(self):
        self.catalogos = []
        self.productos = []
        self.creados_catalogo = 0
        self.creados_producto = 0
        self.actualizados = 0
        self._siguiente_id = 1

    def listar_catalogos(self):
        return list(self.catalogos)

    def crear_catalogo(self, nombre, descripcion):
        self.creados_catalogo += 1
        c = {"id": self._nuevo_id(), "nombre": nombre, "descripcion": descripcion}
        self.catalogos.append(c)
        return c

    def listar_productos(self, catalogo_id):
        return [p for p in self.productos if p["catalogoId"] == catalogo_id]

    def crear_producto(self, payload):
        self.creados_producto += 1
        p = dict(payload)
        p["id"] = self._nuevo_id()
        self.productos.append(p)
        return p

    def actualizar_producto(self, producto_id, payload):
        self.actualizados += 1
        for p in self.productos:
            if p["id"] == producto_id:
                p.update(payload)
                return p
        raise AssertionError(f"producto {producto_id} no existe")

    def _nuevo_id(self):
        id_ = self._siguiente_id
        self._siguiente_id += 1
        return id_


def producto(nombre="Poleron", precio=79990, store="paris", url="https://paris.cl/x"):
    return Product(external_id="MK4QJFISTR", store=store, name=nombre, brand="CK",
                   price=precio, product_url=url)


def test_crea_catalogo_por_categoria_y_producto():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(nombre="Poleron CK"))

    assert [c["nombre"] for c in cliente.catalogos] == ["Polerones"]
    assert cliente.creados_producto == 1
    p = cliente.productos[0]
    assert p["nombre"] == "Poleron CK"
    assert p["precio"] == 79990
    assert p["catalogoId"] == cliente.catalogos[0]["id"]


def test_envia_url_imagen_y_marca():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)
    prod = producto(nombre="Poleron CK").model_copy(update={
        "image_card_url": "https://s3.example/card.webp",
        "image_detail_url": "https://s3.example/detail.webp",
    })

    sync.sincronizar(prod)

    p = cliente.productos[0]
    assert p["url"] == "https://paris.cl/x"
    assert p["imagen"] == "https://s3.example/card.webp"
    assert p["marca"] == "CK"


def test_categoria_reutiliza_el_catalogo():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(nombre="Poleron A"))
    sync.sincronizar(producto(nombre="Poleron B"))

    assert cliente.creados_catalogo == 1


def test_dedup_por_url_dentro_del_catalogo():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(nombre="Poleron CK", precio=79990))
    sync.sincronizar(producto("Poleron CK (2X1)", 59990))  # bajo de precio, misma URL

    assert cliente.creados_producto == 1
    assert cliente.actualizados == 1
    assert cliente.productos[0]["precio"] == 59990


def test_distintas_urls_son_productos_distintos():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(url="https://paris.cl/a"))
    sync.sincronizar(producto(url="https://paris.cl/b"))

    assert cliente.creados_producto == 2


def test_catalogo_por_categoria_no_por_tienda():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(nombre="Poleron CK", store="paris"))
    sync.sincronizar(producto(nombre="Zapatilla Run", store="converse"))

    assert [c["nombre"] for c in cliente.catalogos] == ["Polerones", "Zapatillas"]
    assert cliente.creados_catalogo == 2