# src/scraper/infrastructure/http/product_service_client.py
"""
Cliente HTTP hacia el Product Service (Micronaut).

Product Service mantiene su PROPIA base (SQLite) y es el que atiende al
frontend. El scraper no le escribe en la base: usa su API REST, igual
que lo haria el frontend. Asi el microservicio de catalogo no necesita
saber que existimos.

La API exige versionar cada peticion con la cabecera X-API-VERSION:
    0.1.0  CRUD completo (crear, actualizar, listar)
    0.3.0  listado con filtros (catalogoId, soloActivos)
"""

from __future__ import annotations

import httpx

_HEADERS = {"X-API-VERSION": "0.1.0", "X-VERSION": "0.1.0"}
_HEADERS_FILTRO = {"X-API-VERSION": "0.3.0", "X-VERSION": "0.3.0"}


class ProductServiceClient:
    """
    Cliente del Product Service. Recibe la URL base y habla con sus
    endpoints /catalogos y /productos. Cada llamada levanta si el
    servicio responde mal, para que el barrido lo cuente como fallo de
    sync y siga con el siguiente producto.
    """

    def __init__(self, base_url: str, timeout: float = 30.0) -> None:
        self._http = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout)

    def listar_catalogos(self) -> list[dict]:
        r = self._http.get("/catalogos", headers=_HEADERS)
        r.raise_for_status()
        return r.json()

    def crear_catalogo(self, nombre: str, descripcion: str) -> dict:
        r = self._http.post(
            "/catalogos",
            headers=_HEADERS,
            json={"nombre": nombre, "descripcion": descripcion},
        )
        r.raise_for_status()
        return r.json()

    def listar_productos(self, catalogo_id: int) -> list[dict]:
        r = self._http.get(
            "/productos",
            headers=_HEADERS_FILTRO,
            params={"catalogoId": catalogo_id, "soloActivos": "false"},
        )
        r.raise_for_status()
        return r.json()

    def crear_producto(self, payload: dict) -> dict:
        r = self._http.post("/productos", headers=_HEADERS, json=payload)
        r.raise_for_status()
        return r.json()

    def actualizar_producto(self, producto_id: int, payload: dict) -> dict:
        r = self._http.put(f"/productos/{producto_id}", headers=_HEADERS, json=payload)
        r.raise_for_status()
        return r.json()

    def close(self) -> None:
        self._http.close()

    def __enter__(self) -> ProductServiceClient:
        return self

    def __exit__(self, exc_type, exc_value, traceback) -> None:
        self.close()