"""Metadatos y URLs producidos por el almacenamiento S3."""

from scraper.infrastructure.storage.storage import S3ImageStorage


class FakeS3:
    def __init__(self):
        self.objects = {}
        self.last_put = None

    def head_object(self, *, Bucket, Key):
        if Key not in self.objects:
            error = RuntimeError("not found")
            error.response = {"Error": {"Code": "404"}}
            raise error

    def put_object(self, **kwargs):
        self.last_put = kwargs
        self.objects[kwargs["Key"]] = kwargs["Body"]


def test_upload_sets_web_cache_and_encryption_headers():
    client = FakeS3()
    storage = S3ImageStorage(
        "bucket", "https://bucket.example/", client=client
    )

    url = storage.upload("products/tienda/a card.webp", b"webp")

    assert url == "https://bucket.example/products/tienda/a%20card.webp"
    assert client.last_put["ContentType"] == "image/webp"
    assert client.last_put["CacheControl"].endswith("immutable")
    assert client.last_put["ServerSideEncryption"] == "AES256"


def test_exists_distinguishes_missing_objects():
    client = FakeS3()
    storage = S3ImageStorage("bucket", "https://bucket.example", client=client)
    assert storage.exists("missing") is False
    client.objects["present"] = b"x"
    assert storage.exists("present") is True
