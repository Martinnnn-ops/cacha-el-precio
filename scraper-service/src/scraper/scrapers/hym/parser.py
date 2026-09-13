"""Parser de H&M Chile: JSON-LD más variantes publicadas por Next.js."""

from __future__ import annotations

import json
from typing import Any

from bs4 import BeautifulSoup

from scraper.domain.product import Product
from scraper.scrapers.base.jsonld_parser import ParserJsonLd


class HymParser(ParserJsonLd):
    STORE = "hym"
    MARCA_POR_DEFECTO = "H&M"

    def parse_product(self, html: str, url: str | None = None) -> Product | None:
        producto = super().parse_product(html, url)
        if producto is None:
            return None
        datos = self._datos_next(BeautifulSoup(html, "html.parser"))
        categorias = datos.get("categories", [])
        contexto = " ".join(str(categoria) for categoria in categorias)
        return producto.model_copy(update={"taxonomy_context": contexto or None})

    def _completar(self, sopa: BeautifulSoup, html: str) -> dict[str, Any]:
        producto = self._datos_next(sopa)
        if not producto:
            return {}

        variantes = producto.get("isVariantOf", {}).get("skuVariants", {})
        tallas = [
            opcion.get("value")
            for opciones in variantes.get("availableVariations", {}).values()
            for opcion in opciones
            if isinstance(opcion, dict) and opcion.get("value")
        ]
        return {"size": tallas}

    @staticmethod
    def _datos_next(sopa: BeautifulSoup) -> dict[str, Any]:
        etiqueta = sopa.find("script", id="__NEXT_DATA__")
        if etiqueta is None:
            return {}
        try:
            datos = json.loads(etiqueta.string or "")
            producto = datos["props"]["pageProps"]["data"]["product"]
        except json.JSONDecodeError, KeyError, TypeError:
            return {}
        return producto if isinstance(producto, dict) else {}
