"""Scraper de cl.hm.com. Las URLs de su sitemap ya son fichas."""

from __future__ import annotations

from scraper.scrapers.base.scraper_http import ClienteHttp, ScraperHttp
from scraper.scrapers.hym.parser import HymParser


class HymScraper(ScraperHttp):
    STORE = "hym"

    def __init__(
        self,
        http: ClienteHttp,
        parser: HymParser | None = None,
        *,
        delay: float = 1.0,
        reintentos: int = 3,
    ) -> None:
        super().__init__(http, parser or HymParser(), delay=delay, reintentos=reintentos)
