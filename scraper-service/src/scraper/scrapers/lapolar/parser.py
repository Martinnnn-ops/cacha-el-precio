"""Parser de La Polar/ABC para su ficha con microdatos schema.org."""

from __future__ import annotations

from typing import Any

from bs4 import BeautifulSoup

from scraper.scrapers.base.jsonld_parser import ParserJsonLd


class LaPolarParser(ParserJsonLd):
    STORE = "lapolar"

    def _completar(self, sopa: BeautifulSoup, html: str) -> dict[str, Any]:
        raiz = sopa.select_one('[itemtype="http://schema.org/Product"]')
        if raiz is None:
            return {}

        def contenido(selector: str, atributo: str | None = None) -> str | None:
            nodo = raiz.select_one(selector)
            if nodo is None:
                return None
            valor = nodo.get(atributo) if atributo else nodo.get_text(" ", strip=True)
            return str(valor).strip() if valor else None

        tallas = [
            str(nodo.get("data-attr-value-size")).strip()
            for nodo in raiz.select("[data-attr-value-size].selectable")
            if nodo.get("data-attr-value-size")
        ]
        disponible = raiz.select_one('[itemprop="availability"]')
        availability = disponible.get("href") if disponible else None
        return {
            "name": contenido('.product-name[itemprop="name"]'),
            "sku": contenido('[itemprop="sku"]', "data-sku"),
            "brand": contenido('[itemprop="brand"] [itemprop="name"]'),
            "description": contenido('[itemprop="description"]'),
            "image": contenido('[itemprop="image"]', "src"),
            "size": tallas,
            "offers": {
                "price": contenido('[itemprop="price"]', "data-value"),
                "priceCurrency": contenido('[itemprop="priceCurrency"]', "content"),
                "availability": availability,
            },
        }
