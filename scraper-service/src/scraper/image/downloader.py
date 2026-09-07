"""Descarga segura y acotada de imagenes publicadas por las tiendas."""

from __future__ import annotations

import ipaddress
import socket
from collections.abc import Callable, Iterable
from dataclasses import dataclass
from typing import ClassVar
from urllib.parse import urljoin, urlparse

import httpx


class ImageDownloadError(Exception):
    """La imagen no pudo descargarse o no cumple los limites aceptados."""


@dataclass(frozen=True)
class DownloadedImage:
    content: bytes
    content_type: str
    source_url: str


class ImageDownloader:
    ALLOWED_CONTENT_TYPES: ClassVar[set[str]] = {
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/octet-stream",
    }

    def __init__(
        self,
        *,
        timeout: float = 30.0,
        user_agent: str,
        max_bytes: int = 8 * 1024 * 1024,
        max_redirects: int = 5,
        client: httpx.Client | None = None,
        resolver: Callable[[str], Iterable[str]] | None = None,
    ) -> None:
        self._max_bytes = max_bytes
        self._max_redirects = max_redirects
        self._client = client or httpx.Client(
            timeout=timeout,
            follow_redirects=False,
            headers={"User-Agent": user_agent},
        )
        self._resolver = resolver or self._resolve_host

    def download(self, url: str) -> DownloadedImage:
        actual = url
        for _ in range(self._max_redirects + 1):
            self._validate_public_https_url(actual)
            try:
                with self._client.stream("GET", actual) as response:
                    if response.is_redirect:
                        location = response.headers.get("location")
                        if not location:
                            raise ImageDownloadError("redireccion sin destino")
                        actual = urljoin(actual, location)
                        continue

                    response.raise_for_status()
                    content_type = response.headers.get("content-type", "")
                    content_type = content_type.split(";", 1)[0].strip().lower()
                    if content_type not in self.ALLOWED_CONTENT_TYPES:
                        raise ImageDownloadError(f"tipo de contenido no permitido: {content_type}")

                    declared_size = response.headers.get("content-length")
                    if declared_size and int(declared_size) > self._max_bytes:
                        raise ImageDownloadError("imagen demasiado grande")

                    chunks: list[bytes] = []
                    size = 0
                    for chunk in response.iter_bytes():
                        size += len(chunk)
                        if size > self._max_bytes:
                            raise ImageDownloadError("imagen demasiado grande")
                        chunks.append(chunk)
                    if not chunks:
                        raise ImageDownloadError("imagen vacia")
                    return DownloadedImage(b"".join(chunks), content_type, actual)
            except ImageDownloadError:
                raise
            except (httpx.HTTPError, ValueError) as exc:
                raise ImageDownloadError(str(exc)) from exc
        raise ImageDownloadError("demasiadas redirecciones")

    def _validate_public_https_url(self, url: str) -> None:
        parsed = urlparse(url)
        if parsed.scheme != "https" or not parsed.hostname:
            raise ImageDownloadError("la imagen debe usar una URL HTTPS")
        host = parsed.hostname.lower()
        if host == "localhost" or host.endswith(".local"):
            raise ImageDownloadError("host de imagen no permitido")
        try:
            address = ipaddress.ip_address(host)
        except ValueError:
            try:
                addresses = self._resolver(host)
            except OSError as exc:
                raise ImageDownloadError("no se pudo resolver el host de imagen") from exc
            if not addresses or any(
                not ipaddress.ip_address(ip).is_global
                for ip in addresses
            ):
                raise ImageDownloadError(
                    "el host de imagen resuelve a una red no publica"
                ) from None
        else:
            if not address.is_global:
                raise ImageDownloadError("direccion de imagen no publica")

    @staticmethod
    def _resolve_host(host: str) -> set[str]:
        return {
            str(address[4][0])
            for address in socket.getaddrinfo(
                host,
                None,
                type=socket.SOCK_STREAM,
            )
        }
