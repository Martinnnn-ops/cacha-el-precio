"""Orquesta descarga, transformacion y almacenamiento de imagenes."""

from __future__ import annotations

import re

from scraper.domain.product import Product
from scraper.image.downloader import ImageDownloader
from scraper.image.processor import ImageProcessor
from scraper.infrastructure.storage.storage import ImageStorage


class ProductImageService:
    def __init__(
        self,
        downloader: ImageDownloader,
        processor: ImageProcessor,
        storage: ImageStorage,
    ) -> None:
        self._downloader = downloader
        self._processor = processor
        self._storage = storage

    def process(self, product: Product, previous: Product | None = None) -> Product:
        source = product.source_image_url
        if not source:
            return product

        if (
            previous
            and previous.source_image_url == source
            and previous.image_card_key
            and previous.image_detail_key
        ):
            return product.model_copy(update={
                "image_url": previous.image_detail_url or previous.image_url,
                "image_card_url": previous.image_card_url,
                "image_detail_url": previous.image_detail_url,
                "image_card_key": previous.image_card_key,
                "image_detail_key": previous.image_detail_key,
                "image_hash": previous.image_hash,
            })

        downloaded = self._downloader.download(source)
        processed = self._processor.process(downloaded.content)
        prefix = f"products/{self._safe(product.store)}/{self._safe(product.external_id)}"
        short_hash = processed.content_hash[:16]
        card_key = f"{prefix}/{short_hash}-card.webp"
        detail_key = f"{prefix}/{short_hash}-detail.webp"

        # PutObject sobre una clave derivada del hash es idempotente y solo
        # requiere permiso de escritura; HeadObject puede responder 403 si el
        # rol no tiene ListBucket aunque la posterior subida si este permitida.
        self._storage.upload(card_key, processed.card.content)
        self._storage.upload(detail_key, processed.detail.content)

        card_url = self._storage.public_url(card_key)
        detail_url = self._storage.public_url(detail_key)
        return product.model_copy(update={
            "image_url": detail_url,
            "image_card_url": card_url,
            "image_detail_url": detail_url,
            "image_card_key": card_key,
            "image_detail_key": detail_key,
            "image_hash": processed.content_hash,
        })

    @staticmethod
    def _safe(value: str) -> str:
        safe = re.sub(r"[^a-zA-Z0-9._-]+", "-", value).strip("-.")
        return safe[:120] or "unknown"
