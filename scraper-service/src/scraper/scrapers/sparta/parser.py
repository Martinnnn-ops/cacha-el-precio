# src/scraper/scrapers/sparta/parser.py
"""
Parser de sparta.cl.

Es la unica de las cinco tiendas que NO publica schema.org/Product.
Lo que si tiene:

  - Open Graph con og:title, og:image, og:url y product:price:amount.
    De ahi salen nombre, imagen, enlace y precio.
  - Un bloque de Analytics (Improntus_DataLayersGA4) con el sku, la
    marca y el precio de lista.

Ojo con el precio: el bloque de Analytics trae el precio de LISTA y el
descuento por separado (6990 y 4000 en el ejemplo), mientras que
product:price:amount trae 2990, que es lo que el cliente paga. Para
comparar precios interesa el segundo, asi que el precio se deja en
manos de la base (que usa Open Graph) y de aqui solo se sacan sku y
marca.
"""

from __future__ import annotations

import json
import logging
from typing import Any

from bs4 import BeautifulSoup

from scraper.scrapers.base.jsonld_parser import ParserJsonLd

log = logging.getLogger(__name__)

_CLAVE_GA4 = '"Improntus_DataLayersGA4/js/actions/product-detail"'


class SpartaParser(ParserJsonLd):
    STORE = "sparta"

    def _completar(self, sopa: BeautifulSoup, html: str) -> dict[str, Any]:
        bloque = self._bloque_ga4(html)
        if not bloque:
            return {}
        datos: dict[str, Any] = {}
        sku = bloque.get("sku")
        if isinstance(sku, str) and sku.strip():
            datos["sku"] = sku.strip()
        marca = bloque.get("brand")
        if isinstance(marca, str) and marca.strip():
            datos["brand"] = marca.strip()
        return datos

    @staticmethod
    def _bloque_ga4(html: str) -> dict[str, Any]:
        """
        Extrae el objeto JSON que sigue a la clave de Analytics.

        Se recorta contando llaves y no con una expresion regular: el
        objeto lleva objetos anidados y un regex "hasta la primera }"
        cortaria por la mitad.
        """
        i = html.find(_CLAVE_GA4)
        if i == -1:
            return {}
        inicio = html.find("{", i + len(_CLAVE_GA4))
        if inicio == -1:
            return {}
        profundidad = 0
        for j in range(inicio, len(html)):
            if html[j] == "{":
                profundidad += 1
            elif html[j] == "}":
                profundidad -= 1
                if profundidad == 0:
                    try:
                        return json.loads(html[inicio : j + 1])
                    except json.JSONDecodeError:
                        log.debug("bloque GA4 de sparta no parsea")
                        return {}
        return {}
