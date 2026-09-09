# src/scraper/scrapers/falabella/scraper.py
"""Scraper de falabella.com (Chile). La mecanica esta en ScraperHttp."""

from __future__ import annotations

from scraper.domain.product import Product
from scraper.scrapers.base.scraper_http import ClienteHttp, DescargaFallida, ScraperHttp
from scraper.scrapers.falabella.parser import FalabellaParser


class FalabellaScraper(ScraperHttp):
    STORE = "falabella"

    # Firma explicita y no **kw: con **kw mypy no puede descartar que
    # el diccionario acabe rellenando "parser", que es posicional.
    def __init__(
        self,
        http: ClienteHttp,
        parser: FalabellaParser | None = None,
        *,
        delay: float = 1.0,
        reintentos: int = 3,
    ) -> None:
        self._falabella_parser = parser or FalabellaParser()
        super().__init__(http, self._falabella_parser, delay=delay, reintentos=reintentos)

    def scrape(self, url: str) -> list[Product]:
        """Acepta tanto una ficha individual como un listado de categoria."""
        html = self._descargar(url)
        if html is None:
            raise DescargaFallida(url)

        productos = self._falabella_parser.parse_listing(html)
        if productos:
            return productos

        producto = self._falabella_parser.parse_product(html, url=url)
        return [producto] if producto is not None else []
