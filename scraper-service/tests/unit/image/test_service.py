"""Coordinacion entre descarga, conversion y almacenamiento."""

from io import BytesIO

from PIL import Image

from scraper.domain.product import Product
from scraper.image.downloader import DownloadedImage
from scraper.image.processor import ImageProcessor
from scraper.image.service import ProductImageService


def jpeg_bytes() -> bytes:
    output = BytesIO()
    Image.new("RGB", (900, 600), "navy").save(output, format="JPEG")
    return output.getvalue()


class DownloaderFalso:
    def __init__(self):
        self.calls = 0

    def download(self, url):
        self.calls += 1
        return DownloadedImage(jpeg_bytes(), "image/jpeg", url)


class StorageFalso:
    def __init__(self):
        self.objects = {}

    def exists(self, key):
        return key in self.objects

    def upload(self, key, content):
        self.objects[key] = content
        return self.public_url(key)

    def public_url(self, key):
        return f"https://bucket.example/{key}"


def product():
    return Product(
        external_id="SKU / 1",
        store="paris",
        name="Polera",
        brand="Marca",
        price=9990,
        product_url="https://tienda.example/producto",
        source_image_url="https://cdn.example/original.jpg",
    )


def test_uploads_two_variants_and_enriches_product():
    storage = StorageFalso()
    result = ProductImageService(
        DownloaderFalso(), ImageProcessor(), storage
    ).process(product())

    assert len(storage.objects) == 2
    assert result.image_card_key.startswith("products/paris/SKU-1/")
    assert result.image_card_url.endswith("-card.webp")
    assert result.image_detail_url.endswith("-detail.webp")
    assert result.image_url == result.image_detail_url
    assert result.image_hash and len(result.image_hash) == 64


def test_reuses_previous_image_when_source_did_not_change():
    downloader = DownloaderFalso()
    storage = StorageFalso()
    service = ProductImageService(downloader, ImageProcessor(), storage)
    previous = service.process(product())
    assert downloader.calls == 1

    current = service.process(product().model_copy(update={"price": 8990}), previous)

    assert downloader.calls == 1
    assert current.image_detail_key == previous.image_detail_key
    assert current.image_hash == previous.image_hash
