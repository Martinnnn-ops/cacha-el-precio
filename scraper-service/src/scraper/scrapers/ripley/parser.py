# src/scraper/scrapers/ripley/parser.py
"""
Parser de simple.ripley.cl.

La mecanica esta en ParserJsonLd. Particularidades de esta tienda:

  - El Product va anidado dentro de "@graph", no suelto en la raiz.
  - La pagina trae DOS bloques Product: uno con solo valoraciones y
    otro con los datos reales. La base elige el mas completo; sin eso
    salia un producto vacio.
  - El precio del JSON-LD es el de venta (con descuento aplicado), que
    es justo el que interesa para comparar precios.
"""

from scraper.scrapers.base.jsonld_parser import ParserJsonLd


class RipleyParser(ParserJsonLd):
    STORE = "ripley"
