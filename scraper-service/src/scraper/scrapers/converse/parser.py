# src/scraper/scrapers/converse/parser.py
"""
Parser de converse.cl.

Toda la mecanica esta en ParserJsonLd: la ficha publica un bloque
JSON-LD schema.org/Product con nombre, SKU, precio y moneda, ademas de
Open Graph. Aqui solo se declara lo propio de la tienda.

Particularidades de Converse:
  - El JSON-LD no trae "brand", asi que se cae a la constante.
  - Tampoco trae "availability", asi que el producto se asume
    disponible (lo cubre el comportamiento por defecto de la base).
"""

from scraper.scrapers.base.jsonld_parser import ParserJsonLd


class ConverseParser(ParserJsonLd):
    STORE = "converse"
    MARCA_POR_DEFECTO = "Converse"
