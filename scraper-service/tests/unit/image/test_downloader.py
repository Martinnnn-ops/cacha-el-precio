"""Limites y validaciones de la descarga de imagenes."""

import httpx
import pytest

from scraper.image.downloader import ImageDownloader, ImageDownloadError


def downloader(handler, max_bytes=100):
    client = httpx.Client(transport=httpx.MockTransport(handler))
    return ImageDownloader(
        user_agent="test",
        max_bytes=max_bytes,
        client=client,
        resolver=lambda host: ["93.184.216.34"],
    )


def test_downloads_allowed_image():
    service = downloader(
        lambda request: httpx.Response(200, headers={"content-type": "image/jpeg"}, content=b"img")
    )
    result = service.download("https://cdn.example.com/image.jpg")
    assert result.content == b"img"
    assert result.content_type == "image/jpeg"


def test_rejects_non_image_content_type():
    service = downloader(
        lambda request: httpx.Response(200, headers={"content-type": "text/html"}, content=b"x")
    )
    with pytest.raises(ImageDownloadError, match="tipo de contenido"):
        service.download("https://cdn.example.com/image")


def test_rejects_download_over_limit():
    service = downloader(
        lambda request: httpx.Response(
            200, headers={"content-type": "image/png"}, content=b"12345"
        ),
        max_bytes=4,
    )
    with pytest.raises(ImageDownloadError, match="demasiado grande"):
        service.download("https://cdn.example.com/image.png")


@pytest.mark.parametrize("url", ["http://example.com/a.jpg", "https://127.0.0.1/a.jpg"])
def test_rejects_unsafe_urls(url):
    service = downloader(lambda request: httpx.Response(200, content=b"x"))
    with pytest.raises(ImageDownloadError):
        service.download(url)


def test_validates_redirect_destination():
    service = downloader(
        lambda request: httpx.Response(302, headers={"location": "https://169.254.169.254/x"})
    )
    with pytest.raises(ImageDownloadError, match="no publica"):
        service.download("https://cdn.example.com/image.jpg")


def test_rejects_hostname_resolving_to_private_network():
    client = httpx.Client(
        transport=httpx.MockTransport(lambda request: httpx.Response(200, content=b"x"))
    )
    service = ImageDownloader(
        user_agent="test",
        client=client,
        resolver=lambda host: ["169.254.169.254"],
    )
    with pytest.raises(ImageDownloadError, match="red no publica"):
        service.download("https://internal.example/image.jpg")
