"""Conversion segura de originales a variantes WebP."""

from io import BytesIO

import pytest
from PIL import Image

from scraper.image.processor import ImageProcessingError, ImageProcessor


def image_bytes(size=(1200, 600), mode="RGB", image_format="JPEG"):
    output = BytesIO()
    color = (20, 40, 60, 120) if mode == "RGBA" else (20, 40, 60)
    Image.new(mode, size, color).save(output, format=image_format)
    return output.getvalue()


def test_generates_proportional_webp_variants():
    result = ImageProcessor().process(image_bytes())

    assert (result.card.width, result.card.height) == (400, 200)
    assert (result.detail.width, result.detail.height) == (800, 400)
    assert len(result.content_hash) == 64
    with Image.open(BytesIO(result.card.content)) as card:
        assert card.format == "WEBP"


def test_does_not_upscale_small_images():
    result = ImageProcessor().process(image_bytes((200, 100)))
    assert (result.card.width, result.card.height) == (200, 100)
    assert (result.detail.width, result.detail.height) == (200, 100)


def test_preserves_transparency():
    result = ImageProcessor().process(image_bytes((100, 100), "RGBA", "PNG"))
    with Image.open(BytesIO(result.detail.content)) as detail:
        assert detail.mode == "RGBA"


def test_rejects_corrupt_files():
    with pytest.raises(ImageProcessingError, match="invalido"):
        ImageProcessor().process(b"esto no es una imagen")


def test_rejects_excessive_pixel_count():
    with pytest.raises(ImageProcessingError, match="demasiados pixeles"):
        ImageProcessor(max_pixels=100).process(image_bytes((20, 20)))
