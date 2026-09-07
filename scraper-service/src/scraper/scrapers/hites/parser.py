# src/scraper/scrapers/hites/parser.py
"""
Parser de hites.com.

La mecanica esta en ParserJsonLd. Particularidades de esta tienda:

  - Publica JSON-LD completo con sku, marca anidada, precio y
    "availability", asi que el stock se lee de verdad.
"""

from scraper.scrapers.base.jsonld_parser import ParserJsonLd


class HitesParser(ParserJsonLd):
    STORE = "hites"
