# src/scraper/services/product_service_sync.py
"""
Lleva los productos scrapeados al Product Service.

Product Service y el scraper guardan en esquemas PostgreSQL separados.
Este sincronizador cruza el límite por HTTP: nunca escribe directamente
en las tablas que pertenecen al catálogo.

Product Service agrupa las ofertas por una clave canónica de marca/modelo;
dentro del producto, (tienda, id externo) identifica cada oferta.

Ademas de precio y categoria, el producto lleva lo que el frontend pide
para mostrarse completo: url (el boton "ver en tienda"), imagen y marca.
"""

from __future__ import annotations

import logging

from scraper.domain.categoria import categoria_de
from scraper.domain.product import Product
from scraper.domain.product_identity import canonical_key
from scraper.infrastructure.http.product_service_client import ProductServiceClient

log = logging.getLogger(__name__)


class ProductServiceSync:
    """
    Sincronizador hacia el Product Service. No sabe de HTML ni de SQL:
    solo recibe un Product del dominio y publica una oferta idempotente.

    Adapta el producto del scraper al contrato ingles de Product Service.
    """

    def __init__(self, cliente: ProductServiceClient) -> None:
        self._cliente = cliente

    def sincronizar(self, producto: Product) -> bool:
        """Crea o actualiza la oferta en Product Service."""
        payload = {
            "canonicalKey": canonical_key(producto.brand, producto.name),
            "externalId": producto.external_id,
            "store": producto.store,
            "name": producto.name,
            "brand": producto.brand or "Sin marca",
            "category": categoria_de(producto),
            "price": producto.price,
            "sizes": producto.sizes,
            "description": producto.description or "",
            "url": producto.product_url,
            "image": producto.image_card_url
            or producto.image_detail_url
            or producto.image_url
            or producto.source_image_url,
            "active": producto.available,
        }

        self._cliente.sincronizar_producto(payload)
        return True
