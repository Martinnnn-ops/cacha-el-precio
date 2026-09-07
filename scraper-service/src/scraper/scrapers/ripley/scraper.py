# src/scraper/scrapers/ripley/scraper.py
"""Scraper de simple.ripley.cl. La mecanica esta en ScraperHttp."""

from __future__ import annotations

from scraper.scrapers.base.scraper_http import ClienteHttp, ScraperHttp
from scraper.scrapers.ripley.parser import RipleyParser


class RipleyScraper(ScraperHttp):
    STORE = "ripley"

    def __init__(
        self,
        http: ClienteHttp,
        parser: RipleyParser | None = None,
        *,
        delay: float = 1.0,
        reintentos: int = 3,
    ) -> None:
        super().__init__(http, parser or RipleyParser(), delay=delay, reintentos=reintentos)
