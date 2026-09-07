# src/scraper/scrapers/falabella/parser.py
"""
Parser de falabella.com (Chile).

Igual que Converse, publica schema.org/Product en JSON-LD, asi que
toda la mecanica viene de ParserJsonLd.

Particularidades de Falabella:
  - "brand" viene anidado: {"@type": "Brand", "name": "APPLE"}. La base
    ya lo desanida.
  - SI declara "availability" (InStock / OutOfStock), asi que el campo
    available del dominio se llena de verdad y no por defecto.
  - En productos descatalogados devuelve "offers": []. Sin precio no
    hay producto, y la base devuelve None. Es lo correcto: no queremos
    guardar filas sin precio en una web de comparar precios.
"""

from scraper.scrapers.base.jsonld_parser import ParserJsonLd


class FalabellaParser(ParserJsonLd):
    STORE = "falabella"
