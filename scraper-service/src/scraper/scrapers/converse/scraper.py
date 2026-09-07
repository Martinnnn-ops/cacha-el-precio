# src/scraper/scrapers/converse/scraper.py
"""Scraper de converse.cl. La mecanica esta en ScraperHttp."""

from __future__ import annotations

from scraper.scrapers.base.scraper_http import ClienteHttp, ScraperHttp
from scraper.scrapers.converse.parser import ConverseParser


class ConverseScraper(ScraperHttp):
    STORE = "converse"

    # Firma explicita y no **kw: con **kw mypy no puede descartar que
    # el diccionario acabe rellenando "parser", que es posicional.
    def __init__(
        self,
        http: ClienteHttp,
        parser: ConverseParser | None = None,
        *,
        delay: float = 1.0,
        reintentos: int = 3,
    ) -> None:
        super().__init__(http, parser or ConverseParser(), delay=delay, reintentos=reintentos)
