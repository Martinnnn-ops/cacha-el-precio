"""Validacion y conversion de imagenes de producto a variantes WebP."""

from __future__ import annotations

import hashlib
from dataclasses import dataclass
from io import BytesIO

from PIL import Image, ImageOps, UnidentifiedImageError


class ImageProcessingError(Exception):
    """Los bytes descargados no representan una imagen procesable."""


@dataclass(frozen=True)
class ImageVariant:
    content: bytes
    width: int
    height: int


@dataclass(frozen=True)
class ProcessedImages:
    content_hash: str
    card: ImageVariant
    detail: ImageVariant


class ImageProcessor:
    def __init__(
        self,
        *,
        card_size: int = 400,
        detail_width: int = 800,
        detail_height: int = 800,
        quality: int = 82,
        max_pixels: int = 40_000_000,
    ) -> None:
        self._card_size = card_size
        self._detail_size = (detail_width, detail_height)
        self._quality = quality
        self._max_pixels = max_pixels

    def process(self, content: bytes) -> ProcessedImages:
        try:
            with Image.open(BytesIO(content)) as probe:
                probe.verify()
            with Image.open(BytesIO(content)) as opened:
                opened.seek(0)
                if opened.width * opened.height > self._max_pixels:
                    raise ImageProcessingError("imagen con demasiados pixeles")
                image = ImageOps.exif_transpose(opened)
                has_alpha = image.mode in {"RGBA", "LA"} or (
                    image.mode == "P" and "transparency" in image.info
                )
                base = image.convert("RGBA" if has_alpha else "RGB")
        except ImageProcessingError:
            raise
        except (Image.DecompressionBombError, UnidentifiedImageError, OSError) as exc:
            raise ImageProcessingError("archivo de imagen invalido") from exc

        digest = hashlib.sha256(content).hexdigest()
        return ProcessedImages(
            content_hash=digest,
            card=self._variant(base, (self._card_size, self._card_size)),
            detail=self._variant(base, self._detail_size),
        )

    def _variant(self, image: Image.Image, max_size: tuple[int, int]) -> ImageVariant:
        variant = image.copy()
        variant.thumbnail(max_size, Image.Resampling.LANCZOS)
        output = BytesIO()
        variant.save(
            output,
            format="WEBP",
            quality=self._quality,
            method=6,
            optimize=True,
        )
        return ImageVariant(output.getvalue(), variant.width, variant.height)
