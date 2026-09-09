# src/scraper/domain/product.py
"""
Producto tal como lo publica UNA tienda.

Ojo con el nombre: esto no es un producto canonico compartido entre
tiendas. La misma zapatilla en converse.cl y en Falabella son dos Product
de este dominio porque cada tienda tiene SKU, precio y observacion propios.
Al publicarlas, ProductServiceSync deriva una clave marca/modelo y Product
Service las reune como ofertas de un producto canonico.

La identidad es el par (store, external_id).
"""

from datetime import UTC, datetime

from pydantic import BaseModel, Field


class Product(BaseModel):
    # --- identidad ---
    external_id: str  # SKU de la tienda. Ej: "A21842C-800"
    store: str  # slug de la tienda. Ej: "converse"

    # --- datos del producto ---
    name: str
    brand: str
    price: int  # en la unidad minima de la moneda; CLP no tiene decimales
    product_url: str

    description: str | None = None
    currency: str = "CLP"
    # La fuente pertenece a la tienda. Las demas URLs y claves apuntan a
    # nuestras copias WebP en S3.
    source_image_url: str | None = None
    image_url: str | None = None
    image_card_url: str | None = None
    image_detail_url: str | None = None
    image_card_key: str | None = None
    image_detail_key: str | None = None
    image_hash: str | None = None
    available: bool = True
    # Se guardan como texto para representar tanto XS/XL como 38, 42.5 o 10 US.
    sizes: list[str] = Field(default_factory=list)

    # Cuando se observo. Sin esto no hay forma de saber si un precio es
    # de hoy o de hace tres semanas, ni de construir el historial.
    scraped_at: datetime = Field(default_factory=lambda: datetime.now(UTC))

    @property
    def clave(self) -> tuple[str, str]:
        """Identidad del producto: la tienda y su id en esa tienda."""
        return (self.store, self.external_id)
