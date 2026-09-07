# src/scraper/scrapers/base/scraper_http.py
"""
Scraper generico: junta un cliente HTTP con un parser.

El cliente y el parser se inyectan en el constructor en vez de crearse
aqui dentro. Es lo que permite probar el scraper sin salir a internet:
las pruebas le pasan un cliente falso que devuelve el HTML guardado en
tests/fixtures.
"""

from __future__ import annotations

import logging
import re
import time
from typing import Protocol

from scraper.domain.product import Product
from scraper.scrapers.base.base_scraper import BaseScraper
from scraper.scrapers.base.jsonld_parser import ParserJsonLd

log = logging.getLogger(__name__)


class DescargaFallida(Exception):
    """
    No se pudo traer la pagina tras agotar los reintentos.

    Se distingue de "la pagina no tenia producto" a proposito: en un
    barrido desatendido hay que poder separar "la tienda cambio o esta
    caida" de "estos productos ya no se venden". Si ambos casos se
    cuentan como fallo, un cambio de maquetado pasa desapercibido
    entre productos descatalogados.
    """


class ClienteHttp(Protocol):
    """Lo unico que el scraper necesita de un cliente HTTP."""

    def get(self, url: str) -> str: ...


class ScraperHttp(BaseScraper):
    """
    Descarga fichas y las pasa por un parser. Cada tienda solo aporta
    su parser; los reintentos, el retardo y el manejo de errores son
    identicos para todas.
    """

    STORE: str = ""

    # Patron de URL de ficha de producto. Sirve cuando el sitemap de la
    # tienda mezcla fichas con categorias y paginas estaticas: filtrar
    # antes evita gastar una peticion por cada pagina que no es producto.
    # None = el sitemap ya trae solo fichas.
    FILTRO_URL: re.Pattern[str] | None = None

    @classmethod
    def urls_de_producto(cls, urls: list[str]) -> list[str]:
        if cls.FILTRO_URL is None:
            return urls
        return [u for u in urls if cls.FILTRO_URL.match(u)]

    def __init__(
        self,
        http: ClienteHttp,
        parser: ParserJsonLd,
        *,
        delay: float = 1.0,
        reintentos: int = 3,
    ) -> None:
        self._http = http
        self._parser = parser
        self._delay = delay
        self._reintentos = max(1, reintentos)

    def scrape(self, url: str) -> list[Product]:
        """
        Descarga una ficha y devuelve el producto que contenga.

        Devuelve [] si la pagina se descargo pero no contiene producto
        (descatalogado, sin precio publicado). Lanza DescargaFallida si
        no se pudo descargar. Quien orquesta decide que hacer con cada
        caso; el servicio los cuenta por separado.
        """
        html = self._descargar(url)
        if html is None:
            raise DescargaFallida(url)

        producto = self._parser.parse_product(html, url=url)
        if producto is None:
            log.info("sin producto en %s (descatalogado o sin precio)", url)
            return []
        return [producto]

    def scrape_varios(self, urls: list[str]) -> list[Product]:
        """
        Recorre varias fichas respetando el retardo entre peticiones.

        El retardo va ENTRE peticiones, no despues de la ultima: en un
        barrido de 300 productos eso son 300 segundos de diferencia.
        """
        productos: list[Product] = []
        for i, url in enumerate(urls):
            if i:
                time.sleep(self._delay)
            try:
                productos.extend(self.scrape(url))
            except DescargaFallida:
                continue      # ya quedo registrado en el log
        return productos

    def _descargar(self, url: str) -> str | None:
        """Descarga con reintentos. Devuelve None si se agotan."""
        for intento in range(1, self._reintentos + 1):
            try:
                return self._http.get(url)
            except Exception as e:  # da igual el motivo: se reintenta igual
                if intento == self._reintentos:
                    log.error("fallo definitivo al descargar %s: %s", url, e)
                    return None
                espera = self._delay * intento      # retroceso lineal
                log.warning("intento %d/%d fallo en %s: %s", intento, self._reintentos, url, e)
                time.sleep(espera)
        return None
