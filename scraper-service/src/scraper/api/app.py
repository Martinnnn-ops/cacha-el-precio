# src/scraper/api/app.py
"""
API minima del microservicio.

Su papel es acotado a proposito: el grueso del trabajo lo hace el job
programado (el CLI). Esta API existe para tres cosas concretas que el
job no cubre:

  - /health   para que el orquestador sepa si el servicio esta vivo.
  - /tiendas  para descubrir qué tiendas se pueden scrapear.
  - /scrape   para forzar un barrido puntual sin esperar al cron,
              util cuando alguien anade una tienda o corrige un parser.
  - consultas de producto e historial, para depurar sin abrir la base.

El scrapeo NO se hace dentro de la peticion: tarda segundos o minutos
y dejaria la conexion colgada. Se lanza en segundo plano y se responde
202 Accepted.
"""

from __future__ import annotations

import logging
from threading import Lock
from typing import Any

from fastapi import BackgroundTasks, FastAPI, HTTPException, Query

from scraper.config.settings import get_settings
from scraper.domain.clothing import es_vestimenta
from scraper.infrastructure.http.client import HttpClient
from scraper.main import construir_repositorio, construir_servicio, descubrir_urls, sitemap_de

log = logging.getLogger(__name__)

app = FastAPI(
    title="Cacha el Precio — Scraper",
    description="Microservicio de scraping. El grueso corre por cron; esto es la puerta de servicio.",  # noqa: E501
    version="0.1.0",
)

# Se construyen una vez, no por peticion: abrir la conexion a la base y
# el cliente HTTP en cada request seria un desperdicio.
_cfg = get_settings()
_repo = construir_repositorio(_cfg)
_servicio = construir_servicio(_cfg, _repo)

# Ultimo resultado por tienda, para poder consultar como fue el barrido
# que se lanzo en segundo plano.
_ultimo: dict[str, dict[str, Any]] = {}
_barrido_lock = Lock()


@app.get("/health", summary="Sonda de vida")
def health() -> dict[str, Any]:
    return {
        "estado": "ok",
        "entorno": _cfg.app_env,
        "persistencia": type(_repo).__name__,
        "tiendas": _servicio.tiendas(),
    }


@app.get("/tiendas", summary="Tiendas con scraper disponible")
def tiendas() -> list[dict[str, Any]]:
    return [
        {
            "tienda": t,
            "sitemap": sitemap_de(_cfg, t),
            "modo": (
                "urls_autorizadas"
                if t == "converse"
                else "listados"
                if t == "falabella"
                else "sitemap"
            ),
        }
        for t in _servicio.tiendas()
    ]


def _barrer(tienda: str, urls: list[str]) -> None:
    """Se ejecuta en segundo plano; guarda el resultado para /status."""
    try:
        # El repositorio PostgreSQL comparte una conexion. Serializar los
        # barridos evita mezclar transacciones de dos peticiones simultaneas.
        with _barrido_lock:
            r = _servicio.ejecutar(tienda, urls)
        _ultimo[tienda] = {
            "resumen": r.resumen(),
            "urls_pedidas": r.urls_pedidas,
            "guardados": r.guardados,
            "cambios_de_precio": r.cambios_de_precio,
            "productos_descartados": r.productos_descartados,
            "imagenes_procesadas": r.imagenes_procesadas,
            "sincronizados": r.sincronizados,
            "sin_producto": len(r.sin_producto),
            "fallos_de_descarga": len(r.urls_fallidas),
            "fallos_de_guardado": len(r.errores_guardado),
            "fallos_de_imagen": len(r.errores_imagen),
            "fallos_de_sync": len(r.errores_sync),
            "segundos": round(r.segundos, 1),
        }
    except Exception as e:
        log.exception("barrido de %s fallo", tienda)
        _ultimo[tienda] = {"error": str(e)}


@app.post("/scrape/{tienda}", status_code=202, summary="Lanza un barrido en segundo plano")
def scrape(
    tienda: str,
    tareas: BackgroundTasks,
    limite: int = Query(50, ge=1, le=1000, description="maximo de paginas de la fuente"),
) -> dict[str, Any]:
    if tienda not in _servicio.tiendas():
        raise HTTPException(404, f"no hay scraper para la tienda {tienda!r}")

    url_sitemap = sitemap_de(_cfg, tienda)
    if not url_sitemap:
        raise HTTPException(
            400,
            f"la tienda {tienda!r} no tiene una fuente automatica autorizada: "
            "usa el CLI solo con URLs entregadas o aprobadas por la tienda",
        )

    http = HttpClient(timeout=_cfg.http_timeout, user_agent=_cfg.http_user_agent)
    urls = descubrir_urls(_cfg, tienda, limite, http)
    if not urls:
        raise HTTPException(502, "el sitemap no devolvio ninguna URL")

    # 202: aceptado y encolado. El scrapeo tarda demasiado para hacerlo
    # dentro de la peticion.
    tareas.add_task(_barrer, tienda, urls)
    return {"estado": "encolado", "tienda": tienda, "urls": len(urls)}


@app.get("/scrape/{tienda}/status", summary="Resultado del ultimo barrido")
def estado_barrido(tienda: str) -> dict[str, Any]:
    if tienda not in _ultimo:
        raise HTTPException(404, f"no hay barridos registrados para {tienda!r}")
    return _ultimo[tienda]


@app.get("/productos/{tienda}/{external_id}", summary="Un producto")
def producto(tienda: str, external_id: str) -> dict[str, Any]:
    p = _repo.obtener(tienda, external_id)
    if p is None or not es_vestimenta(p):
        raise HTTPException(404, "producto no encontrado")
    return p.model_dump(mode="json")


@app.get("/productos/{tienda}/{external_id}/historial", summary="Historial de precios")
def historial(
    tienda: str,
    external_id: str,
    limite: int = Query(100, ge=1, le=1000),
) -> list[dict[str, Any]]:
    p = _repo.obtener(tienda, external_id)
    if p is None or not es_vestimenta(p):
        raise HTTPException(404, "producto no encontrado")
    return [o.model_dump(mode="json") for o in _repo.historial(tienda, external_id, limite)]
