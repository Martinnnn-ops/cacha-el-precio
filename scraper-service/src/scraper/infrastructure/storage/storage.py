"""Puerto de almacenamiento de imagenes e implementacion para Amazon S3."""

from __future__ import annotations

from typing import Any, Protocol
from urllib.parse import quote


class ImageStorage(Protocol):
    def exists(self, key: str) -> bool: ...

    def upload(self, key: str, content: bytes) -> str: ...

    def public_url(self, key: str) -> str: ...


class S3ImageStorage:
    def __init__(
        self,
        bucket: str,
        public_base_url: str,
        *,
        region: str = "us-east-1",
        client: Any | None = None,
    ) -> None:
        if client is None:
            import boto3

            client = boto3.client("s3", region_name=region)
        self._client = client
        self._bucket = bucket
        self._public_base_url = public_base_url.rstrip("/")

    def exists(self, key: str) -> bool:
        try:
            self._client.head_object(Bucket=self._bucket, Key=key)
            return True
        except Exception as exc:
            response = getattr(exc, "response", {})
            code = str(response.get("Error", {}).get("Code", ""))
            if code in {"404", "NoSuchKey", "NotFound"}:
                return False
            raise

    def upload(self, key: str, content: bytes) -> str:
        self._client.put_object(
            Bucket=self._bucket,
            Key=key,
            Body=content,
            ContentType="image/webp",
            CacheControl="public, max-age=31536000, immutable",
            ServerSideEncryption="AES256",
        )
        return self.public_url(key)

    def public_url(self, key: str) -> str:
        return f"{self._public_base_url}/{quote(key, safe='/')}"
