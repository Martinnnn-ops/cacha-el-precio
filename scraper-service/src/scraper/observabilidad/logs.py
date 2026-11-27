"""
Logs en JSON: una linea por evento con campos con nombre, igual que los
servicios .NET, para poder filtrarlos juntos (por servicio, nivel o
correlationId) en vez de leer texto suelto.

Solo libreria estandar, a proposito: el formato de un log no justifica una
dependencia mas.

El correlationId vive en un ContextVar: lo fija quien empieza un trabajo (un
barrido) y todo log que ocurra dentro lo lleva solo, sin pasarlo a mano.
"""

from __future__ import annotations

import contextlib
import contextvars
import json
import logging
from collections.abc import Iterator
from datetime import UTC, datetime

SERVICIO = "scraper-service"

_correlacion: contextvars.ContextVar[str | None] = contextvars.ContextVar(
    "correlation_id", default=None
)


def correlacion_actual() -> str | None:
    return _correlacion.get()


@contextlib.contextmanager
def con_correlacion(correlation_id: str | None) -> Iterator[None]:
    """Todo log dentro del bloque lleva este correlationId."""
    token = _correlacion.set(correlation_id)
    try:
        yield
    finally:
        _correlacion.reset(token)


class FormatoJson(logging.Formatter):
    def format(self, record: logging.LogRecord) -> str:
        datos: dict[str, object] = {
            "Timestamp": datetime.fromtimestamp(record.created, UTC).strftime(
                "%Y-%m-%dT%H:%M:%S.%f"
            )[:-3]
            + "Z",
            "LogLevel": record.levelname.capitalize(),
            "Servicio": SERVICIO,
            "Category": record.name,
            "Message": record.getMessage(),
        }
        cid = _correlacion.get()
        if cid:
            datos["correlationId"] = cid
        if record.exc_info:
            datos["Exception"] = self.formatException(record.exc_info)
        return json.dumps(datos, ensure_ascii=False)


def configurar_logs(nivel: str = "INFO") -> None:
    """Un solo manejador en JSON para todo, incluidos los logs de uvicorn."""
    manejador = logging.StreamHandler()
    manejador.setFormatter(FormatoJson())

    raiz = logging.getLogger()
    # Solo se reemplazan los manejadores de consola comunes (los de basicConfig
    # o una llamada anterior). Los demas se respetan: por ejemplo, el que usan
    # las pruebas para capturar los logs.
    raiz.handlers[:] = [h for h in raiz.handlers if type(h) is not logging.StreamHandler]
    raiz.addHandler(manejador)
    raiz.setLevel(nivel.upper())

    # uvicorn trae sus propios manejadores en texto: se le quitan para que sus
    # lineas ("GET /health 200") tambien salgan en JSON por la raiz.
    for nombre in ("uvicorn", "uvicorn.error", "uvicorn.access"):
        lg = logging.getLogger(nombre)
        lg.handlers[:] = []
        lg.propagate = True
