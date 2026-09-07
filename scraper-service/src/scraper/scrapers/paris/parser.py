# src/scraper/scrapers/paris/parser.py
"""
Parser de paris.cl.

La mecanica esta en ParserJsonLd. Particularidades de esta tienda:

  - Publica JSON-LD schema.org/Product completo: sku, nombre, marca
    anidada y precio.
  - NO declara "availability", asi que el producto se asume disponible
    (comportamiento por defecto de la base).
"""

from scraper.scrapers.base.jsonld_parser import ParserJsonLd


class ParisParser(ParserJsonLd):
    STORE = "paris"
