# src/scraper/main.py
"""
Punto de entrada del job de scraping.

Uso:
    scrapper tiendas
    scrapper barrer converse --urls urls.txt
    scrapper barrer converse --url https://www.converse.cl/algun-producto
    scrapper esquema          (crea las tablas en PostgreSQL)

Pensado para colgarlo de un timer de systemd o de cron. Devuelve 0 si
el barrido termino y 1 si fallaron TODAS las URLs, para que el timer
pueda marcar el fallo.
"""

from __future__ import annotations

import argparse
import logging
import sys
from pathlib import Path

from scraper.config.settings import Settings, get_settings
from scraper.infrastructure.database.repositories.product_repository import (
    RepositorioEnMemoria,
)
from scraper.infrastructure.http.client import HttpClient
from scraper.infrastructure.http.sitemap import leer_sitemap
from scraper.scrapers.base.scraper_http import ScraperHttp
from scraper.scrapers.converse.scraper import ConverseScraper
from scraper.scrapers.falabella.scraper import FalabellaScraper
from scraper.scrapers.hites.scraper import HitesScraper
from scraper.scrapers.paris.scraper import ParisScraper
from scraper.scrapers.ripley.scraper import RipleyScraper
from scraper.scrapers.sparta.scraper import SpartaScraper
from scraper.services.scraper_service import Scraper, ScraperService

log = logging.getLogger("scrapper")


def construir_repositorio(cfg: Settings):
    """
    PostgreSQL si hay DATABASE_URL; si no, memoria y un aviso claro.

    El aviso importa: sin el, un barrido nocturno mal configurado
    correria durante horas y tiraria todo lo scrapeado al salir.
    """
    if not cfg.database_url:
        log.warning(
            "DATABASE_URL no esta definida: se usara un repositorio EN MEMORIA "
            "y los datos se perderan al terminar."
        )
        return RepositorioEnMemoria()

    try:
        import psycopg
    except ModuleNotFoundError:
        log.error('psycopg no esta instalado. Instala el extra:  pip install -e ".[db]"')
        raise SystemExit(2) from None

    from scraper.infrastructure.database.repositories.postgres_product_repository import (
        RepositorioPostgres,
    )

    try:
        conexion = psycopg.connect(cfg.database_url, connect_timeout=10)
    except psycopg.OperationalError as e:
        # Un job por cron deja esto en su log. Una traza de Python de 15
        # lineas no dice que hacer; este mensaje si.
        log.error("no se pudo conectar a PostgreSQL: %s", str(e).splitlines()[0])
        log.error("  Si usas el compose:  docker compose up -d db")
        raise SystemExit(3) from None
    return RepositorioPostgres(conexion)


def construir_servicio(cfg: Settings, repo) -> ScraperService:
    http = HttpClient(timeout=cfg.http_timeout, user_agent=cfg.http_user_agent)
    # Argumentos explicitos y no **dict: un dict[str, float] desdibuja
    # los tipos y mypy no puede comprobar la llamada.
    demora = cfg.scraper_request_delay
    reintentos = cfg.http_max_retries
    scrapers: dict[str, Scraper] = {
        "converse": ConverseScraper(http, delay=demora, reintentos=reintentos),
        "falabella": FalabellaScraper(http, delay=demora, reintentos=reintentos),
        "paris": ParisScraper(http, delay=demora, reintentos=reintentos),
        "ripley": RipleyScraper(http, delay=demora, reintentos=reintentos),
        "hites": HitesScraper(http, delay=demora, reintentos=reintentos),
        "sparta": SpartaScraper(http, delay=demora, reintentos=reintentos),
    }
    image_service = None
    if cfg.aws_s3_bucket:
        try:
            from scraper.image.downloader import ImageDownloader
            from scraper.image.processor import ImageProcessor
            from scraper.image.service import ProductImageService
            from scraper.infrastructure.storage.storage import S3ImageStorage
        except ModuleNotFoundError:
            log.error('faltan dependencias de imagen/S3. Instala: pip install -e ".[s3,imagen]"')
            raise SystemExit(2) from None

        public_base_url = cfg.s3_public_base_url
        assert public_base_url is not None
        image_service = ProductImageService(
            ImageDownloader(
                timeout=cfg.http_timeout,
                user_agent=cfg.http_user_agent,
                max_bytes=cfg.image_max_download_bytes,
            ),
            ImageProcessor(
                card_size=cfg.image_card_size,
                detail_width=cfg.image_max_width,
                detail_height=cfg.image_max_height,
                quality=cfg.image_webp_quality,
                max_pixels=cfg.image_max_pixels,
            ),
            S3ImageStorage(
                cfg.aws_s3_bucket,
                public_base_url,
                region=cfg.aws_region,
            ),
        )
    return ScraperService(scrapers, repo, delay=demora, image_service=image_service)


# De donde saca cada tienda sus URLs de producto.
def sitemap_de(cfg: Settings, tienda: str) -> str | None:
    return {
        "falabella": cfg.falabella_sitemap,
        "paris": cfg.paris_sitemap,
        "ripley": cfg.ripley_sitemap,
        "hites": cfg.hites_sitemap,
        "sparta": cfg.sparta_sitemap,
    }.get(tienda)


# Registro unico de tiendas: lo usan tanto el filtro de URLs como el
# comando "tiendas", que no debe necesitar la base para responder.
_TIENDAS: dict[str, type[ScraperHttp]] = {
    "converse": ConverseScraper,
    "falabella": FalabellaScraper,
    "paris": ParisScraper,
    "ripley": RipleyScraper,
    "hites": HitesScraper,
    "sparta": SpartaScraper,
}


def _solo_fichas(tienda: str, urls: list[str]) -> list[str]:
    """Aplica el filtro de URLs del scraper de esa tienda, si tiene."""
    clase = _TIENDAS.get(tienda)
    return clase.urls_de_producto(urls) if clase else urls


def leer_urls(args: argparse.Namespace, cfg: Settings) -> list[str]:
    if getattr(args, "sitemap", False):
        url = sitemap_de(cfg, args.tienda)
        if not url:
            log.error("la tienda %s no tiene sitemap configurado", args.tienda)
            raise SystemExit(2)
        http = HttpClient(timeout=cfg.http_timeout, user_agent=cfg.http_user_agent)
        # Se pide de mas y luego se filtra: si el sitemap mezcla
        # categorias, pedir justo el limite dejaria muy pocas fichas.
        crudas = leer_sitemap(url, http, max_urls=args.limite * 4)
        return _solo_fichas(args.tienda, crudas)[: args.limite]
    if args.url:
        return list(args.url)
    ruta = Path(args.urls)
    if not ruta.is_file():
        log.error("no existe el archivo de URLs: %s", ruta)
        raise SystemExit(2)
    # Se ignoran lineas vacias y comentarios para poder anotar el fichero.
    return [
        linea.strip()
        for linea in ruta.read_text(encoding="utf-8").splitlines()
        if linea.strip() and not linea.lstrip().startswith("#")
    ]


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="scrapper", description="Scraper de Cacha el Precio")
    sub = p.add_subparsers(dest="comando", required=True)

    sub.add_parser("tiendas", help="lista las tiendas con scraper disponible")
    sub.add_parser("esquema", help="crea las tablas en PostgreSQL")

    b = sub.add_parser("barrer", help="scrapea una tienda")
    b.add_argument("tienda")
    origen = b.add_mutually_exclusive_group(required=True)
    origen.add_argument("--urls", help="archivo con una URL por linea")
    origen.add_argument("--url", action="append", help="URL suelta (repetible)")
    origen.add_argument("--sitemap", action="store_true",
                        help="descubre las URLs desde el sitemap de la tienda")
    b.add_argument("--limite", type=int, default=50,
                   help="maximo de URLs a barrer con --sitemap (por defecto 50)")

    d = sub.add_parser("descubrir", help="lista URLs de producto desde el sitemap")
    d.add_argument("tienda")
    d.add_argument("--limite", type=int, default=20)

    args = p.parse_args(argv)
    cfg = get_settings()
    logging.basicConfig(
        level=cfg.log_level.upper(),
        format="%(asctime)s %(levelname)-7s %(name)s: %(message)s",
    )

    # "tiendas" y "descubrir" no tocan la base: se resuelven antes de
    # abrir conexion, para que sigan funcionando con la base apagada.
    if args.comando == "tiendas":
        for t in _TIENDAS:
            sm = sitemap_de(cfg, t)
            print(f"{t}\t{'sitemap disponible' if sm else 'sin sitemap: hay que dar las URLs'}")
        return 0

    if args.comando == "descubrir":
        url = sitemap_de(cfg, args.tienda)
        if not url:
            log.error("la tienda %s no tiene sitemap configurado", args.tienda)
            return 2
        http = HttpClient(timeout=cfg.http_timeout, user_agent=cfg.http_user_agent)
        crudas = leer_sitemap(url, http, max_urls=args.limite * 4)
        for u in _solo_fichas(args.tienda, crudas)[: args.limite]:
            print(u)
        return 0

    repo = construir_repositorio(cfg)

    if args.comando == "esquema":
        if not hasattr(repo, "crear_esquema"):
            log.error("el esquema solo se puede crear contra PostgreSQL")
            return 2
        repo.crear_esquema()
        log.info("esquema creado")
        return 0

    servicio = construir_servicio(cfg, repo)

    resultado = servicio.ejecutar(args.tienda, leer_urls(args, cfg))
    print(resultado.resumen())
    # Que fallen todas suele significar que la tienda cambio o que no
    # hay red; eso si merece marcar el job como fallido.
    fallo_total = resultado.urls_pedidas and resultado.urls_ok == 0
    return 1 if fallo_total or resultado.errores_guardado else 0


if __name__ == "__main__":
    sys.exit(main())
