# src/scraper/scrapers/sparta/scraper.py
"""Scraper de sparta.cl. La mecanica esta en ScraperHttp."""

from __future__ import annotations

import re

from scraper.scrapers.base.scraper_http import ClienteHttp, ScraperHttp
from scraper.scrapers.sparta.parser import SpartaParser


class SpartaScraper(ScraperHttp):
    STORE = "sparta"

    # El sitemap de Sparta mezcla fichas, categorias y paginas
    # estaticas. Lo que distingue una ficha es que el ultimo tramo
    # termina en el SKU, que SIEMPRE lleva digitos; los slugs de
    # categoria son solo letras ("deportes-individuales.html").
    FILTRO_URL = re.compile(r"^https://sparta\.cl/[^/]+-[a-z0-9]*\d[a-z0-9]*\.html$", re.I)

    def __init__(
        self,
        http: ClienteHttp,
        parser: SpartaParser | None = None,
        *,
        delay: float = 1.0,
        reintentos: int = 3,
    ) -> None:
        super().__init__(http, parser or SpartaParser(), delay=delay, reintentos=reintentos)
