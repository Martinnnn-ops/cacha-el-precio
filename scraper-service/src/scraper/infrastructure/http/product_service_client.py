# src/scraper/infrastructure/http/product_service_client.py
"""
Cliente HTTP hacia el Product Service (ASP.NET Core).

Product Service mantiene su PROPIO esquema PostgreSQL y atiende al
frontend. El scraper no le escribe en la base: usa su API REST, igual
que lo haria el frontend. Asi el microservicio de catalogo no necesita
saber que existimos.

La API se versiona con la cabecera Version y actualmente usa la version 1.0.
"""

from __future__ import annotations

import httpx

_HEADERS = {"Version": "1.0"}


class ProductServiceClient:
    """
    Cliente del Product Service. Recibe la URL base y habla con sus
    endpoints /api/products. Cada llamada levanta si el
    servicio responde mal, para que el barrido lo cuente como fallo de
    sync y siga con el siguiente producto.
    """

    def __init__(self, base_url: str, timeout: float = 30.0) -> None:
        self._http = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout)

    def sincronizar_producto(self, payload: dict) -> dict:
        r = self._http.post("/api/products", headers=_HEADERS, json=payload)
        r.raise_for_status()
        return r.json()

    def close(self) -> None:
        self._http.close()

    def __enter__(self) -> ProductServiceClient:
        return self

    def __exit__(self, exc_type, exc_value, traceback) -> None:
        self.close()
