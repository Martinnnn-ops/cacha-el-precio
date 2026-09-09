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

from __future__ import annotations

import json
from typing import Any

from bs4 import BeautifulSoup

from scraper.domain.product import Product
from scraper.scrapers.base.jsonld_parser import ParserJsonLd


class FalabellaParser(ParserJsonLd):
    STORE = "falabella"

    def parse_listing(self, html: str) -> list[Product]:
        """Extrae todas las fichas incluidas por Falabella en un listado."""
        sopa = BeautifulSoup(html, "html.parser")
        etiqueta = sopa.find("script", id="__NEXT_DATA__")
        if etiqueta is None:
            return []

        try:
            datos = json.loads(etiqueta.string or "")
            resultados = datos["props"]["pageProps"]["results"]
        except json.JSONDecodeError, KeyError, TypeError:
            return []
        if not isinstance(resultados, list):
            return []

        productos: list[Product] = []
        for resultado in resultados:
            producto = self._producto_del_listado(resultado)
            if producto is not None:
                productos.append(producto)
        return productos

    def _producto_del_listado(self, datos: Any) -> Product | None:
        if not isinstance(datos, dict):
            return None

        nombre = self._texto(datos.get("displayName"))
        enlace = self._texto(datos.get("url"))
        external_id = self._texto(datos.get("skuId")) or self._texto(datos.get("offeringId"))
        precio = self._precio_listado(datos.get("prices"))
        if not nombre or not enlace or not external_id or precio is None:
            return None

        marca = self._texto(datos.get("brand")) or self.STORE
        imagenes = datos.get("mediaUrls")
        imagen = self._texto(imagenes[0]) if isinstance(imagenes, list) and imagenes else None

        return Product(
            external_id=external_id,
            store=self.STORE,
            name=nombre,
            brand=marca,
            price=precio,
            product_url=enlace,
            source_image_url=imagen,
            sizes=self._tallas_listado(datos.get("variants")),
        )

    def _precio_listado(self, precios: Any) -> int | None:
        if not isinstance(precios, list):
            return None

        # El precio vigente aparece sin tachar. El precio normal tachado
        # queda solo como ultimo recurso si el listado no trae otro valor.
        ordenados = sorted(
            (precio for precio in precios if isinstance(precio, dict)),
            key=lambda precio: bool(precio.get("crossed")),
        )
        for precio in ordenados:
            valores = precio.get("price")
            valor = (valores[0] if valores else None) if isinstance(valores, list) else valores
            normalizado = self._precio({"price": valor}, {})
            if normalizado is not None:
                return normalizado
        return None

    def _tallas_listado(self, variantes: Any) -> list[str]:
        if not isinstance(variantes, list):
            return []

        tallas: list[str] = []
        for variante in variantes:
            if not isinstance(variante, dict):
                continue
            opciones = variante.get("options")
            if not isinstance(opciones, list):
                continue
            for opcion in opciones:
                if not isinstance(opcion, dict):
                    continue
                sizes = opcion.get("sizes")
                candidatas: list[Any] = sizes if isinstance(sizes, list) else [opcion]
                for candidata in candidatas:
                    if not isinstance(candidata, dict) or candidata.get("available") is False:
                        continue
                    talla = self._texto(candidata.get("value")) or self._texto(
                        candidata.get("label")
                    )
                    if talla and talla not in tallas:
                        tallas.append(talla)
        return tallas
