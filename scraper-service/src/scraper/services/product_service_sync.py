# src/scraper/services/product_service_sync.py
"""
Lleva los productos scrapeados al Product Service.

Product Service guarda en SQLite y el frontend lee de ahi; el scraper
guarda en PostgreSQL y es dueño de sus datos. Este sincronizador hace de
puente: para cada tienda asegura un catalogo y crea o actualiza los
productos segun existan.

No hay id externo con que identificar un producto en Product Service,
asi que el dedup se hace por NOMBRE dentro del catalogo de la tienda:
si por aqui ya pasamos un producto con ese nombre, se actualiza (precio,
descripcion, activo); si no, se crea. Es lo mismo que hace el frontend
para evitar duplicados en el listado del comparador.
"""

from __future__ import annotations

import logging

from scraper.domain.product import Product
from scraper.infrastructure.http.product_service_client import ProductServiceClient

log = logging.getLogger(__name__)


class ProductServiceSync:
    """
    Sincronizador hacia el Product Service. No sabe de HTML ni de SQL:
    solo recibe un Product del dominio y decide crear o actualizar.

    Mantiene un catalogo por tienda. El nombre del catalogo reusa el del
    slug de la tienda (ej. "paris"), porque es lo unico estable que
    sabemos de cada tienda.
    """

    def __init__(self, cliente: ProductServiceClient) -> None:
        self._cliente = cliente
        # slug de tienda -> id del catalogo en Product Service
        self._catalogo_id: dict[str, int] = {}
        # nombre del producto -> id en Product Service, por catalogo
        self._existentes: dict[int, dict[str, int]] = {}
        self._cargados: set[int] = set()

    def sincronizar(self, producto: Product) -> bool:
        """Crea o actualiza un producto en Product Service. False si no hay nada que hacer."""
        catalogo_id = self._catalogo_para(producto.store)

        producto_id = self._id_existente(catalogo_id, producto.name)
        payload = {
            "nombre": producto.name,
            "descripcion": producto.description or "",
            "precio": producto.price,
            "catalogoId": catalogo_id,
            "activo": producto.available,
        }

        if producto_id is not None:
            self._cliente.actualizar_producto(producto_id, payload)
            return True

        creado = self._cliente.crear_producto(payload)
        self._existentes[catalogo_id][producto.name] = creado["id"]
        return True

    def _catalogo_para(self, tienda: str) -> int:
        """El id del catalogo de la tienda, creandolo si hace falta."""
        if tienda in self._catalogo_id:
            return self._catalogo_id[tienda]

        catalogos = self._cliente.listar_catalogos()
        existente = next((c for c in catalogos if c.get("nombre") == tienda), None)
        if existente is not None:
            catalogo_id = int(existente["id"])
        else:
            creado = self._cliente.crear_catalogo(tienda, f"Productos de {tienda}")
            catalogo_id = int(creado["id"])

        self._catalogo_id[tienda] = catalogo_id
        return catalogo_id

    def _id_existente(self, catalogo_id: int, nombre: str) -> int | None:
        """Id del producto con ese nombre dentro del catalogo, o None."""
        if catalogo_id not in self._cargados:
            existentes: dict[str, int] = {}
            for p in self._cliente.listar_productos(catalogo_id):
                existentes[p.get("nombre", "")] = int(p["id"])
            self._existentes[catalogo_id] = existentes
            self._cargados.add(catalogo_id)

        return self._existentes[catalogo_id].get(nombre)