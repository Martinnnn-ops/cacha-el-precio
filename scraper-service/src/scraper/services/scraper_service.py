# src/scraper/services/scraper_service.py
"""
Orquesta un barrido: scrapear una tienda y persistir el resultado.

El servicio no sabe de HTML ni de SQL. Recibe scrapers y un repositorio
ya construidos y solo coordina. Eso es lo que permite ejecutarlo entero
en las pruebas sin red y sin base de datos.
"""

from __future__ import annotations

import logging
import time
from collections.abc import Mapping
from dataclasses import dataclass, field
from typing import Protocol

from scraper.domain.clothing import es_vestimenta
from scraper.domain.product import Product

log = logging.getLogger(__name__)


class Scraper(Protocol):
    def scrape(self, url: str) -> list[Product]: ...


class Repositorio(Protocol):
    def guardar(self, producto: Product) -> bool: ...

    def obtener(self, store: str, external_id: str) -> Product | None: ...


class ImageService(Protocol):
    def process(self, product: Product, previous: Product | None = None) -> Product: ...


@dataclass
class ResultadoBarrido:
    """Lo que hay que poder mirar despues de un barrido nocturno."""

    tienda: str
    urls_pedidas: int = 0
    productos_extraidos: int = 0
    guardados: int = 0
    cambios_de_precio: int = 0
    productos_descartados: int = 0
    imagenes_procesadas: int = 0
    urls_fallidas: list[str] = field(default_factory=list)   # no se pudo descargar
    errores_guardado: list[str] = field(default_factory=list)
    errores_imagen: list[str] = field(default_factory=list)
    sin_producto: list[str] = field(default_factory=list)    # bajo, pero no hay producto
    segundos: float = 0.0

    @property
    def urls_ok(self) -> int:
        """Descargadas correctamente, tengan producto o no."""
        return self.urls_pedidas - len(self.urls_fallidas)

    def resumen(self) -> str:
        return (
            f"[{self.tienda}] {self.urls_ok}/{self.urls_pedidas} URLs, "
            f"{self.guardados} guardados, {self.cambios_de_precio} cambios de precio, "
            f"{self.productos_descartados} fuera de vestimenta, "
            f"{self.imagenes_procesadas} imagenes procesadas, "
            f"{len(self.sin_producto)} sin producto, "
            f"{len(self.urls_fallidas)} fallos de descarga, "
            f"{len(self.errores_guardado)} fallos de guardado, "
            f"{len(self.errores_imagen)} fallos de imagen, {self.segundos:.1f}s"
        )


class ScraperService:
    # Mapping y no dict: dict es invariante en el valor, asi que un
    # dict[str, ConverseScraper] NO encajaria aqui aunque cumpla el
    # protocolo. Mapping si es covariante.
    def __init__(
        self,
        scrapers: Mapping[str, Scraper],
        repositorio: Repositorio,
        *,
        delay: float = 0.0,
        image_service: ImageService | None = None,
    ) -> None:
        self._scrapers: Mapping[str, Scraper] = scrapers
        self._repo = repositorio
        self._delay = max(0.0, delay)
        self._image_service = image_service

    def tiendas(self) -> list[str]:
        return sorted(self._scrapers)

    def ejecutar(self, tienda: str, urls: list[str]) -> ResultadoBarrido:
        """
        Barre una tienda. No lanza excepcion por una URL rota: la anota
        en urls_fallidas y sigue, porque un barrido nocturno de cientos
        de productos no puede morir por uno.
        """
        scraper = self._scrapers.get(tienda)
        if scraper is None:
            raise KeyError(f"no hay scraper registrado para la tienda {tienda!r}")

        res = ResultadoBarrido(tienda=tienda, urls_pedidas=len(urls))
        inicio = time.monotonic()

        for indice, url in enumerate(urls):
            if indice and self._delay:
                time.sleep(self._delay)
            try:
                productos = scraper.scrape(url)
            except Exception:
                # Incluye DescargaFallida y cualquier fallo inesperado del
                # scraper: en ambos casos la URL no dio datos por un
                # problema tecnico, no porque el producto no exista.
                log.warning("no se pudo procesar %s", url)
                res.urls_fallidas.append(url)
                continue

            if not productos:
                # Se descargo bien: el producto esta descatalogado o sin
                # precio. No es un fallo del scraper.
                res.sin_producto.append(url)
                continue

            res.productos_extraidos += len(productos)
            for producto in productos:
                if not es_vestimenta(producto):
                    res.productos_descartados += 1
                    log.info("producto fuera de vestimenta descartado: %s", producto.product_url)
                    continue
                if self._image_service:
                    anterior = self._repo.obtener(producto.store, producto.external_id)
                    if producto.source_image_url:
                        try:
                            producto = self._image_service.process(producto, anterior)
                            if producto.image_detail_key:
                                res.imagenes_procesadas += 1
                        except Exception:
                            log.exception(
                                "no se pudo procesar la imagen de %s", producto.product_url
                            )
                            res.errores_imagen.append(f"{producto.store}/{producto.external_id}")
                            producto = self._preservar_imagen(producto, anterior)
                    else:
                        producto = self._preservar_imagen(producto, anterior)
                try:
                    if self._repo.guardar(producto):
                        res.cambios_de_precio += 1
                    res.guardados += 1
                except Exception:
                    # Un fallo al guardar no debe perder el resto del barrido.
                    log.exception("no se pudo guardar %s/%s", producto.store, producto.external_id)
                    res.errores_guardado.append(f"{producto.store}/{producto.external_id}")

        res.segundos = time.monotonic() - inicio
        log.info("%s", res.resumen())
        return res

    @staticmethod
    def _preservar_imagen(producto: Product, anterior: Product | None) -> Product:
        """Un fallo temporal no debe borrar una imagen que ya era valida."""
        if anterior is None:
            return producto
        return producto.model_copy(update={
            "source_image_url": anterior.source_image_url,
            "image_url": anterior.image_url,
            "image_card_url": anterior.image_card_url,
            "image_detail_url": anterior.image_detail_url,
            "image_card_key": anterior.image_card_key,
            "image_detail_key": anterior.image_detail_key,
            "image_hash": anterior.image_hash,
        })
