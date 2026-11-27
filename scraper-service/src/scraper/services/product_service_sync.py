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
from typing import Protocol

from scraper.domain.catalog_policy import clave_generica, marca_catalogo, motivo_infantil
from scraper.domain.categoria import clasificacion_de
from scraper.domain.product import Product
from scraper.domain.product_identity import canonical_key

log = logging.getLogger(__name__)


class ClientePublicacion(Protocol):
    """
    Donde se deja cada prenda. Hoy hay dos: ProductServiceClient (POST
    /api/products) e IngestaColaClient (POST /ingesta -> cola SQS). El
    sincronizador no sabe cual le toco: lo decide MODO_PUBLICACION.
    """

    def sincronizar_producto(self, payload: dict) -> dict: ...


class PublicadorPrecios(Protocol):
    """
    Donde se manda el precio capturado para el stream de analisis (Kinesis).
    Opcional: sin stream configurado, el sincronizador no lo llama.
    """

    def publicar(self, producto: Product) -> None: ...


class ProductServiceSync:
    """
    Sincronizador hacia el Product Service. No sabe de HTML ni de SQL:
    solo recibe un Product del dominio y publica una oferta idempotente.

    Adapta el producto del scraper al contrato ingles de Product Service.

    Si se le da un `stream`, despues de publicar la prenda manda tambien su
    precio al stream de Kinesis (EA3/EFT). Es un efecto de lado de mejor
    esfuerzo: el propio productor se traga los fallos, asi que nunca corta la
    sincronizacion de la prenda.
    """

    def __init__(
        self, cliente: ClientePublicacion, stream: PublicadorPrecios | None = None
    ) -> None:
        self._cliente = cliente
        self._stream = stream

    def sincronizar(self, producto: Product) -> bool:
        """Crea o actualiza la oferta en Product Service."""
        if motivo_infantil(
            producto.name, f"{producto.taxonomy_context or ''} {producto.product_url}"
        ):
            return False
        clasificacion = clasificacion_de(producto)
        payload = {
            "canonicalKey": (
                clave_generica(producto.store, producto.external_id)
                if marca_catalogo(producto.brand) == "Genéricas"
                else canonical_key(producto.brand, producto.name)
            ),
            "externalId": producto.external_id,
            "store": producto.store,
            "name": producto.name,
            "brand": marca_catalogo(producto.brand),
            "category": clasificacion.categoria,
            "bodyArea": clasificacion.zona_corporal,
            "gender": clasificacion.genero,
            "layer": clasificacion.capa,
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
        if self._stream is not None:
            self._stream.publicar(producto)
        return True
