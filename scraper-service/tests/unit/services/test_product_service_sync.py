"""
El sincronizador hacia Product Service se prueba con un cliente falso:
sin red, sin SQLite y sin el microservicio Java, pero con el mismo
comportamiento que su API (el catalogo se crea una vez, los productos
se deduplican por nombre dentro del catalogo).
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


def producto(nombre="Poleron", precio=79990, store="paris"):
    return Product(external_id="MK4QJFISTR", store=store, name=nombre,
                   brand="CK", price=precio, product_url="https://paris.cl/x")


def test_crea_catalogo_y_producto():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto())

    assert len(cliente.catalogos) == 1
    assert cliente.catalogos[0]["nombre"] == "paris"
    assert cliente.creados_producto == 1
    assert len(cliente.productos) == 1
    assert cliente.productos[0]["nombre"] == "Poleron"
    assert cliente.productos[0]["precio"] == 79990
    assert cliente.productos[0]["catalogoId"] == 1


def test_no_duplica_catalogo_entre_productos_de_la_misma_tienda():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto("Poleron A"))
    sync.sincronizar(producto("Poleron B"))

    assert cliente.creados_catalogo == 1
    assert cliente.creados_producto == 2


def test_deduplica_por_nombre_dentro_del_catalogo():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto("Poleron CK", 79990))
    sync.sincronizar(producto("Poleron CK", 59990))  # bajo de precio

    # Mismo producto: se actualiza, no se crea otro.
    assert cliente.creados_producto == 1
    assert cliente.actualizados == 1
    assert len(cliente.productos) == 1
    assert cliente.productos[0]["precio"] == 59990


def test_catalogo_por_tienda():
    cliente = ClienteProductServiceFalso()
    sync = ProductServiceSync(cliente)

    sync.sincronizar(producto(store="paris"))
    sync.sincronizar(producto(store="converse"))

    assert [c["nombre"] for c in cliente.catalogos] == ["paris", "converse"]
    assert cliente.creados_catalogo == 2