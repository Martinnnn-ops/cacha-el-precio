# src/scraper/scrapers/hites/scraper.py
"""Scraper de hites.com. La mecanica esta en ScraperHttp."""

from __future__ import annotations

from scraper.scrapers.base.scraper_http import ClienteHttp, ScraperHttp
from scraper.scrapers.hites.parser import HitesParser


class HitesScraper(ScraperHttp):
    STORE = "hites"

    def __init__(
        self,
        http: ClienteHttp,
        parser: HitesParser | None = None,
        *,
        delay: float = 1.0,
        reintentos: int = 3,
    ) -> None:
        super().__init__(http, parser or HitesParser(), delay=delay, reintentos=reintentos)
