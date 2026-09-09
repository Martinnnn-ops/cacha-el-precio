# src/scraper/scrapers/base/jsonld_parser.py
"""
Base comun para tiendas que publican datos estructurados.

Casi todo el comercio grande publica schema.org/Product en un bloque
JSON-LD y etiquetas Open Graph, porque lo necesitan para Google. Eso
es mucho mas estable que las clases CSS del maquetado, que cambian con
cada rediseno, y evita una trampa concreta: las fichas suelen llevar
carruseles de recomendados con SUS precios, y un selector CSS puede
coger el del producto vecino.

Cada tienda solo tiene que declarar su slug y, si hace falta, ajustar
algun campo. Eso es el patron Strategy del README: misma mecanica,
detalles por tienda.
"""

from __future__ import annotations

import json
import re
from typing import Any

from bs4 import BeautifulSoup

from scraper.domain.product import Product


class ParserJsonLd:
    """Extrae un Product de una ficha con JSON-LD y/o Open Graph."""

    STORE: str = ""
    MARCA_POR_DEFECTO: str | None = None

    def parse_product(self, html: str, url: str | None = None) -> Product | None:
        sopa = BeautifulSoup(html, "html.parser")
        datos = self._json_ld_producto(sopa)
        # Gancho para tiendas que no publican todo en JSON-LD. Sparta,
        # por ejemplo, deja el sku y la marca en un bloque de Analytics.
        datos = {**self._completar(sopa, html), **datos}
        og = self._open_graph(sopa)
        oferta = self._primera_oferta(datos)

        nombre = self._texto(datos.get("name")) or og.get("og:title")
        precio = self._precio(oferta, og)
        enlace = og.get("og:url") or url

        # Sin nombre, precio y enlace no hay producto que guardar.
        if not nombre or precio is None or not enlace:
            return None

        return Product(
            external_id=self._id_externo(datos, oferta, enlace),
            name=nombre,
            brand=self._marca(datos),
            price=precio,
            product_url=enlace,
            store=self.STORE,
            description=self._texto(datos.get("description")) or og.get("og:description"),
            currency=self._moneda(oferta, og),
            source_image_url=self._imagen(datos) or og.get("og:image"),
            available=self._disponible(oferta),
            sizes=self._tallas(datos, oferta),
        )

    def _completar(self, sopa: BeautifulSoup, html: str) -> dict[str, Any]:
        """
        Campos que la tienda no publica en JSON-LD.

        Lo que devuelva se usa SOLO donde el JSON-LD no llega: el
        JSON-LD tiene prioridad, porque es el formato estandar y el
        que la tienda mantiene para los buscadores.
        """
        return {}

    # ---------- fuentes ----------

    def _json_ld_producto(self, sopa: BeautifulSoup) -> dict[str, Any]:
        """
        El bloque JSON-LD de tipo Product mas completo de la pagina.

        Dos cosas que no son obvias y costaron encontrar:

        1. El Product puede venir anidado dentro de "@graph", no suelto
           en la raiz. Ripley lo publica asi.
        2. Una misma pagina puede traer VARIOS bloques Product. Ripley
           tiene uno que solo lleva aggregateRating y review, y otro
           con nombre, precio y sku. Quedarse con el primero da un
           producto vacio, asi que se elige el que mas campos utiles
           tenga.
        """
        candidatos: list[dict[str, Any]] = []
        for etiqueta in sopa.find_all("script", type="application/ld+json"):
            try:
                cargado = json.loads(etiqueta.string or "")
            except json.JSONDecodeError, TypeError:
                continue  # un JSON-LD roto no debe tumbar el scrapeo
            for item in cargado if isinstance(cargado, list) else [cargado]:
                candidatos.extend(self._buscar_productos(item))

        if not candidatos:
            return {}
        # "Util" = tiene lo que necesitamos para construir un Product.
        return max(candidatos, key=self._utilidad)

    def _buscar_productos(self, nodo: Any) -> list[dict[str, Any]]:
        """Recoge Product/ProductGroup aunque estén anidados fuera de @graph."""
        if isinstance(nodo, list):
            return [producto for hijo in nodo for producto in self._buscar_productos(hijo)]
        if not isinstance(nodo, dict):
            return []
        encontrados: list[dict[str, Any]] = []
        tipo = nodo.get("@type")
        tipos = tipo if isinstance(tipo, list) else [tipo]
        if "Product" in tipos or "ProductGroup" in tipos:
            encontrados.append(nodo)
        for hijo in nodo.values():
            encontrados.extend(self._buscar_productos(hijo))
        return encontrados

    @staticmethod
    def _utilidad(nodo: dict[str, Any]) -> int:
        """Cuantos campos que nos importan trae el bloque."""
        return sum(bool(nodo.get(c)) for c in ("name", "offers", "sku", "brand", "image"))

    def _open_graph(self, sopa: BeautifulSoup) -> dict[str, str]:
        etiquetas: dict[str, str] = {}
        for meta in sopa.find_all("meta"):
            clave = meta.get("property") or meta.get("name")
            contenido = meta.get("content")
            if clave and contenido and str(clave).startswith(("og:", "product:")):
                etiquetas[str(clave)] = str(contenido)
        return etiquetas

    def _primera_oferta(self, datos: dict[str, Any]) -> dict[str, Any]:
        """
        offers puede ser un objeto, una lista, o una lista VACIA.

        Falabella devuelve [] en productos descatalogados; ahi no hay
        precio y el producto se descarta arriba.
        """
        ofertas = datos.get("offers")
        if isinstance(ofertas, dict):
            return ofertas
        if isinstance(ofertas, list):
            for o in ofertas:
                if isinstance(o, dict):
                    return o
        variantes = datos.get("hasVariant", [])
        if isinstance(variantes, dict):
            variantes = [variantes]
        if isinstance(variantes, list):
            for variante in variantes:
                if isinstance(variante, dict):
                    oferta = self._primera_oferta(variante)
                    if oferta:
                        return oferta
        return {}

    # ---------- normalizacion ----------

    @staticmethod
    def _texto(valor: Any) -> str | None:
        if isinstance(valor, str) and valor.strip():
            return valor.strip()
        return None

    def _precio(self, oferta: dict[str, Any], og: dict[str, str]) -> int | None:
        """
        Llega como "79990.00" o "11990" segun la tienda. Se devuelve en
        entero porque el peso chileno no tiene decimales.
        """
        crudo: Any = oferta.get("price") or oferta.get("lowPrice")
        if crudo is None:
            crudo = og.get("product:price:amount")
        if crudo is None:
            return None
        texto = re.sub(r"[^0-9,.-]", "", str(crudo)).strip()
        if not texto:
            return None

        # En Chile 79.990 y 79,990 suelen ser separadores de miles. En
        # cambio 79990.50 es un decimal real. CLP se redondea a entero.
        if re.fullmatch(r"\d{1,3}(?:[.,]\d{3})+", texto):
            texto = texto.replace(".", "").replace(",", "")
        elif "," in texto and "." not in texto:
            texto = texto.replace(",", ".")
        elif "," in texto and "." in texto:
            texto = texto.replace(".", "").replace(",", ".")

        try:
            precio = round(float(texto))
        except ValueError:
            return None
        # Un 0 en un catalogo de tienda no significa "gratis": significa
        # "sin precio publicado". Sparta lo hace con los productos
        # agotados, y guardarlos con precio 0 los dejaria apareciendo
        # como los mas baratos de toda la web. Sin precio, no se guarda.
        return precio if precio > 0 else None

    def _moneda(self, oferta: dict[str, Any], og: dict[str, str]) -> str:
        return self._texto(oferta.get("priceCurrency")) or og.get("product:price:currency") or "CLP"

    def _disponible(self, oferta: dict[str, Any]) -> bool:
        """
        schema.org/InStock, /OutOfStock, /SoldOut...

        Si la tienda no lo declara (Converse no lo hace), se asume que
        esta disponible: si la ficha se sirve y tiene precio, lo normal
        es que se pueda comprar.
        """
        crudo = self._texto(oferta.get("availability"))
        if crudo is None:
            return True
        estado = crudo.rsplit("/", 1)[-1].lower()
        return estado not in {"outofstock", "soldout", "discontinued", "backorder"}

    def _marca(self, datos: dict[str, Any]) -> str:
        marca = datos.get("brand")
        if isinstance(marca, dict):
            marca = marca.get("name")
        return self._texto(marca) or self.MARCA_POR_DEFECTO or self.STORE

    def _imagen(self, datos: dict[str, Any]) -> str | None:
        imagen = datos.get("image")
        if isinstance(imagen, list):
            return self._texto(imagen[0]) if imagen else None
        return self._texto(imagen)

    def _tallas(self, datos: dict[str, Any], oferta: dict[str, Any]) -> list[str]:
        """Lee tallas schema.org, incluidas variantes y números decimales."""
        valores: list[Any] = [datos.get("size"), oferta.get("size")]
        variantes = datos.get("hasVariant", [])

        if isinstance(variantes, dict):
            variantes = [variantes]
        if isinstance(variantes, list):
            for variante in variantes:
                if not isinstance(variante, dict):
                    continue
                valores.append(variante.get("size"))
                oferta_variante = self._primera_oferta(variante)
                valores.append(oferta_variante.get("size"))

        tallas: list[str] = []
        for valor in valores:
            items = valor if isinstance(valor, list) else [valor]
            for item in items:
                if not isinstance(item, (str, int, float)):
                    continue
                # La coma sin espacio puede ser decimal (42,5); con espacio
                # se interpreta como una lista ("38, 39").
                for parte in re.split(r"\s*[/;|]\s*|,\s+", str(item)):
                    normalizada = parte.strip().upper().replace(",", ".")
                    if normalizada and normalizada not in tallas:
                        tallas.append(normalizada)
        return tallas

    def _id_externo(self, datos: dict[str, Any], oferta: dict[str, Any], enlace: str) -> str:
        """SKU de la tienda; si no lo publica, el ultimo tramo de la URL."""
        sku = self._texto(datos.get("sku")) or self._texto(oferta.get("sku"))
        if sku:
            return sku
        # Sparta no publica sku: se deriva del ultimo tramo de la URL,
        # sin la extension (".../zapatilla-x-123.html" -> "zapatilla-x-123").
        tramo = enlace.split("?")[0].rstrip("/").rsplit("/", 1)[-1]
        return tramo.removesuffix(".html").removesuffix(".htm") or tramo
