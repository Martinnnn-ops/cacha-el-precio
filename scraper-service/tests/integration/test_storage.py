# tests/integration/test_storage.py
"""Prueba opt-in contra un bucket S3 real y limpia el objeto creado."""

import os
from uuid import uuid4

import pytest

RUN = os.getenv("RUN_S3_INTEGRATION") == "1"
BUCKET = os.getenv("AWS_S3_BUCKET")
pytestmark = pytest.mark.skipif(
    not RUN or not BUCKET,
    reason="define RUN_S3_INTEGRATION=1 y AWS_S3_BUCKET para probar S3",
)


def test_upload_exists_and_public_url():
    import boto3

    from scraper.infrastructure.storage.storage import S3ImageStorage

    region = os.getenv("AWS_REGION", "us-east-1")
    base = os.getenv(
        "AWS_S3_PUBLIC_BASE_URL",
        f"https://{BUCKET}.s3.{region}.amazonaws.com",
    )
    client = boto3.client("s3", region_name=region)
    storage = S3ImageStorage(BUCKET, base, region=region, client=client)
    key = f"products/tests/{uuid4().hex}.webp"
    try:
        storage.upload(key, b"RIFF-test-WEBP")
        assert storage.exists(key) is True
        assert storage.public_url(key).startswith(base)
    finally:
        client.delete_object(Bucket=BUCKET, Key=key)
