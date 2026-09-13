"""Scraper de La Polar/ABC. La mecánica común vive en ``ScraperHttp``."""

from __future__ import annotations

from scraper.scrapers.base.scraper_http import ClienteHttp, ScraperHttp
from scraper.scrapers.lapolar.parser import LaPolarParser


class LaPolarScraper(ScraperHttp):
    STORE = "lapolar"

    def __init__(
        self,
        http: ClienteHttp,
        parser: LaPolarParser | None = None,
        *,
        delay: float = 1.0,
        reintentos: int = 3,
    ) -> None:
        super().__init__(http, parser or LaPolarParser(), delay=delay, reintentos=reintentos)
