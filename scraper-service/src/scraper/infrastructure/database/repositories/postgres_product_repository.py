# src/scraper/infrastructure/database/repositories/postgres_product_repository.py
"""
Implementacion del repositorio sobre PostgreSQL con psycopg 3.

Cumple el mismo contrato que RepositorioEnMemoria, asi que los
servicios no distinguen cual tienen delante. Esa es la utilidad de
haber definido el Protocol.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any

from scraper.domain.clothing import es_vestimenta
from scraper.domain.offer import Offer
from scraper.domain.product import Product

ESQUEMA = Path(__file__).parents[1] / "schema.sql"

_UPSERT = """
INSERT INTO products (
    store, external_id, name, brand, price, currency,
    product_url, description, source_image_url, image_url, image_card_url,
    image_detail_url, image_card_key, image_detail_key, image_hash, available, scraped_at
) VALUES (
    %(store)s, %(external_id)s, %(name)s, %(brand)s, %(price)s, %(currency)s,
    %(product_url)s, %(description)s, %(source_image_url)s, %(image_url)s,
    %(image_card_url)s, %(image_detail_url)s, %(image_card_key)s,
    %(image_detail_key)s, %(image_hash)s, %(available)s, %(scraped_at)s
)
ON CONFLICT (store, external_id) DO UPDATE SET
    name        = EXCLUDED.name,
    brand       = EXCLUDED.brand,
    price       = EXCLUDED.price,
    currency    = EXCLUDED.currency,
    product_url = EXCLUDED.product_url,
    description = EXCLUDED.description,
    source_image_url = EXCLUDED.source_image_url,
    image_url   = EXCLUDED.image_url,
    image_card_url = EXCLUDED.image_card_url,
    image_detail_url = EXCLUDED.image_detail_url,
    image_card_key = EXCLUDED.image_card_key,
    image_detail_key = EXCLUDED.image_detail_key,
    image_hash = EXCLUDED.image_hash,
    available   = EXCLUDED.available,
    scraped_at  = EXCLUDED.scraped_at
"""

_PRECIO_ANTERIOR = """
SELECT price, available FROM products
WHERE store = %(store)s AND external_id = %(external_id)s
"""

_INSERTA_HISTORIAL = """
INSERT INTO price_history (store, external_id, price, currency, available, scraped_at)
VALUES (%(store)s, %(external_id)s, %(price)s, %(currency)s, %(available)s, %(scraped_at)s)
"""

_SELECT_PRODUCTO = """
SELECT store, external_id, name, brand, price, currency,
       product_url, description, source_image_url, image_url, image_card_url,
       image_detail_url, image_card_key, image_detail_key, image_hash, available, scraped_at
FROM products
WHERE store = %(store)s AND external_id = %(external_id)s
"""

_SELECT_HISTORIAL = """
SELECT store, external_id, price, currency, available, scraped_at
FROM price_history
WHERE store = %(store)s AND external_id = %(external_id)s
ORDER BY scraped_at DESC
LIMIT %(limite)s
"""


class RepositorioPostgres:
    """Persistencia en PostgreSQL. Recibe una conexion psycopg ya abierta."""

    def __init__(self, conexion: Any) -> None:
        self._con = conexion

    def crear_esquema(self) -> None:
        """Aplica schema.sql. Es idempotente (todo va con IF NOT EXISTS)."""
        with self._con.cursor() as cur:
            cur.execute(ESQUEMA.read_text(encoding="utf-8"))
        self._con.commit()

    def guardar(self, producto: Product) -> bool:
        """
        Upsert del producto y, si cambio el precio o el stock, una fila
        de historial. Todo en una transaccion: o se guardan los dos o
        ninguno, para que el historial nunca contradiga al producto.
        """
        if not es_vestimenta(producto):
            raise ValueError(f"producto fuera de vestimenta: {producto.product_url}")
        datos = producto.model_dump()
        try:
            with self._con.cursor() as cur:
                cur.execute(_PRECIO_ANTERIOR, {"store": producto.store,
                                               "external_id": producto.external_id})
                fila = cur.fetchone()
                cambio = fila is None or fila[0] != producto.price or fila[1] != producto.available

                cur.execute(_UPSERT, datos)
                if cambio:
                    cur.execute(_INSERTA_HISTORIAL, datos)
            self._con.commit()
            return cambio
        except Exception:
            self._con.rollback()
            raise

    def obtener(self, store: str, external_id: str) -> Product | None:
        with self._con.cursor() as cur:
            cur.execute(_SELECT_PRODUCTO, {"store": store, "external_id": external_id})
            fila = cur.fetchone()
        if fila is None:
            return None
        campos = (
            "store", "external_id", "name", "brand", "price", "currency",
            "product_url", "description", "source_image_url", "image_url",
            "image_card_url", "image_detail_url", "image_card_key", "image_detail_key",
            "image_hash", "available", "scraped_at",
        )
        return Product(**dict(zip(campos, fila, strict=True)))

    def historial(self, store: str, external_id: str, limite: int = 100) -> list[Offer]:
        with self._con.cursor() as cur:
            cur.execute(_SELECT_HISTORIAL,
                        {"store": store, "external_id": external_id, "limite": limite})
            filas = cur.fetchall()
        campos = ("store", "external_id", "price", "currency", "available", "scraped_at")
        return [Offer(**dict(zip(campos, f, strict=True))) for f in filas]
