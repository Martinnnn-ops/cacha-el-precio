# src/scraper/services/product_service_sync.py
"""
Lleva los productos scrapeados al Product Service.

Product Service guarda en SQLite y el frontend lee de ahi; el scraper
guarda en PostgreSQL y es dueño de sus datos. Este sincronizador hace de
puente y crea o actualiza los productos segun existan.

La identidad compartida es (tienda, id externo), el mismo par que usa
el dominio del scraper. Esto evita duplicados aunque cambien nombre,
precio o URL.

Ademas de precio y categoria, el producto lleva lo que el frontend pide
para mostrarse completo: url (el boton "ver en tienda"), imagen y marca.
"""

from __future__ import annotations

import logging

from scraper.domain.categoria import categoria_de
from scraper.domain.product import Product
from scraper.infrastructure.http.product_service_client import ProductServiceClient

log = logging.getLogger(__name__)


class ProductServiceSync:
    """
    Sincronizador hacia el Product Service. No sabe de HTML ni de SQL:
    solo recibe un Product del dominio y decide crear o actualizar.

    Adapta el producto del scraper al contrato ingles de Product Service.
    """

    def __init__(self, cliente: ProductServiceClient) -> None:
        self._cliente = cliente
        self._existentes: dict[tuple[str, str], int] = {}
        self._cargados = False

    def sincronizar(self, producto: Product) -> bool:
        """Crea o actualiza un producto en Product Service. False si no hay nada que hacer."""
        producto_id = self._id_existente(producto.store, producto.external_id)
        payload = {
            "externalId": producto.external_id,
            "store": producto.store,
            "name": producto.name,
            "brand": producto.brand or "Sin marca",
            "category": categoria_de(producto),
            "price": producto.price,
            "sizes": {
                "xs": False,
                "s": False,
                "m": False,
                "l": False,
                "xl": False,
                "xxl": False,
            },
            "description": producto.description or "",
            "url": producto.product_url,
            "image": producto.image_card_url
            or producto.image_detail_url
            or producto.image_url,
            "active": producto.available,
        }

        if producto_id is not None:
            self._cliente.actualizar_producto(producto_id, payload)
            return True

        creado = self._cliente.crear_producto(payload)
        self._existentes[producto.clave] = creado["id"]
        return True

    def _id_existente(self, tienda: str, external_id: str) -> int | None:
        """Id interno para el par estable (tienda, id externo), o None."""
        if not self._cargados:
            for product in self._cliente.listar_productos():
                key = (product.get("store", ""), product.get("externalId", ""))
                self._existentes[key] = int(product["id"])
            self._cargados = True

        return self._existentes.get((tienda, external_id))
