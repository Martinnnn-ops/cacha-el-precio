# src/scraper/infrastructure/http/sitemap.py
"""
Lector de sitemaps XML.

Es la forma correcta de descubrir URLs de producto: la tienda las
publica ella misma en el formato estandar, con lo que no hay que
adivinar patrones de URL ni pasearse por el catalogo pagina a pagina.
Falabella publica 63 sub-sitemaps de fichas con 25.000 URLs cada uno.

Un sitemap puede ser de dos tipos y hay que distinguirlos:
  <sitemapindex> -> apunta a otros sitemaps (hay que bajar un nivel)
  <urlset>       -> ya son las URLs finales
"""

from __future__ import annotations

import gzip
import logging
import re
from typing import Protocol

log = logging.getLogger(__name__)

# Se parsea con expresion regular y no con un parser XML a proposito:
# estos archivos llegan a decenas de MB y solo se necesita <loc>.
_LOC = re.compile(r"<loc>\s*([^<]+?)\s*</loc>", re.I)
_ES_INDICE = re.compile(r"<sitemapindex", re.I)


class ClienteBytes(Protocol):
    def get_bytes(self, url: str) -> bytes: ...


def _descomprimir(cuerpo: bytes) -> str:
    """Los sitemaps suelen venir en .gz aunque la URL no lo diga."""
    if cuerpo[:2] == b"\x1f\x8b":
        cuerpo = gzip.decompress(cuerpo)
    return cuerpo.decode("utf-8", "replace")


def _es_indice(texto: str, enlaces: list[str]) -> bool:
    """
    Decide si el documento apunta a otros sitemaps.

    Lo normal es la etiqueta <sitemapindex>, pero Paris publica su
    indice como <urlset> con enlaces a archivos .xml. Sin esta segunda
    comprobacion, se devolvian 43 rutas de sitemaps creyendo que eran
    fichas de producto.
    """
    if _ES_INDICE.search(texto):
        return True
    if not enlaces:
        return False
    xml = sum(1 for e in enlaces if e.split("?")[0].endswith((".xml", ".xml.gz")))
    return xml == len(enlaces)


def leer_sitemap(
    url: str,
    cliente: ClienteBytes,
    *,
    max_urls: int | None = None,
    profundidad: int = 2,
) -> list[str]:
    """
    Devuelve las URLs finales de un sitemap, siguiendo los indices.

    Args:
        max_urls:    corta al llegar a este numero. Imprescindible: el
                     sitemap de Falabella son ~1,5 millones de URLs y
                     casi nunca se quieren todas de una vez.
        profundidad: cuantos niveles de indice seguir. Evita quedarse
                     en bucle si una tienda publica un indice circular.
    """
    if profundidad < 0:
        return []

    try:
        texto = _descomprimir(cliente.get_bytes(url))
    except Exception as e:  # una tienda caida no debe abortar el resto
        log.error("no se pudo leer el sitemap %s: %s", url, e)
        return []

    enlaces = _LOC.findall(texto)
    if not _es_indice(texto, enlaces):
        return enlaces[:max_urls] if max_urls else enlaces

    # Es un indice: hay que bajar un nivel.
    urls: list[str] = []
    for hijo in enlaces:
        restantes = None if max_urls is None else max_urls - len(urls)
        if restantes is not None and restantes <= 0:
            break
        urls.extend(
            leer_sitemap(hijo, cliente, max_urls=restantes, profundidad=profundidad - 1)
        )
    return urls[:max_urls] if max_urls else urls
