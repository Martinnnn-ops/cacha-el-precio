# src/scraper/domain/offer.py
"""
Un precio observado en una tienda en un instante concreto.

Es la fila del historial: cada vez que el scraper ve un producto,
guarda una Offer. De ahi salen los graficos de "bajo de precio" y las
alertas, que es el motivo de existir de Cacha el Precio.

Product guarda el estado ACTUAL de un producto en una tienda; Offer
guarda todos los precios por los que ha pasado.
"""

from __future__ import annotations

from datetime import UTC, datetime
from typing import TYPE_CHECKING

from pydantic import BaseModel, Field

if TYPE_CHECKING:
    # Solo para el anotado: en tiempo de ejecucion no se importa, y asi
    # no hay ciclo entre product.py y offer.py.
    from scraper.domain.product import Product


class Offer(BaseModel):
    # A que producto pertenece: el mismo par (store, external_id).
    store: str
    external_id: str

    price: int
    currency: str = "CLP"
    available: bool = True
    scraped_at: datetime = Field(default_factory=lambda: datetime.now(UTC))

    @classmethod
    def desde_producto(cls, producto: Product) -> Offer:
        """Toma la foto de precio de un producto recien scrapeado."""
        return cls(
            store=producto.store,
            external_id=producto.external_id,
            price=producto.price,
            currency=producto.currency,
            available=producto.available,
            scraped_at=producto.scraped_at,
        )
