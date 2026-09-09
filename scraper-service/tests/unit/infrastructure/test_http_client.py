# tests/unit/infrastructure/test_http_client.py

from scraper.infrastructure.http.client import HttpClient


def test_http_client_get():
    with HttpClient() as client:
        html = client.get("https://example.com")

    assert "<html" in html.lower()
