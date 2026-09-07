# src/scraper/scrapers/paris/scraper.py
"""Scraper de paris.cl. La mecanica esta en ScraperHttp."""

from __future__ import annotations

from scraper.scrapers.base.scraper_http import ClienteHttp, ScraperHttp
from scraper.scrapers.paris.parser import ParisParser


class ParisScraper(ScraperHttp):
    STORE = "paris"

    def __init__(
        self,
        http: ClienteHttp,
        parser: ParisParser | None = None,
        *,
        delay: float = 1.0,
        reintentos: int = 3,
    ) -> None:
        super().__init__(http, parser or ParisParser(), delay=delay, reintentos=reintentos)
