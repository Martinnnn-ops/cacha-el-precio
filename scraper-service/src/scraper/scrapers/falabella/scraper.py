# src/scraper/scrapers/falabella/scraper.py
"""Scraper de falabella.com (Chile). La mecanica esta en ScraperHttp."""

from __future__ import annotations

from scraper.scrapers.base.scraper_http import ClienteHttp, ScraperHttp
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
        super().__init__(http, parser or FalabellaParser(), delay=delay, reintentos=reintentos)
