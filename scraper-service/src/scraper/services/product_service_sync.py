# src/scraper/services/product_service_sync.py
"""
Lleva los productos scrapeados al Product Service.

Product Service guarda en SQLite y el frontend lee de ahi; el scraper
guarda en PostgreSQL y es dueño de sus datos. Este sincronizador hace de
puente: asegura un catalogo por CATEGORIA de prenda y crea o actualiza
los productos segun existan.

No hay id externo con que identificar un producto en Product Service,
asi que el dedup se hace por la URL del producto dentro del catalogo:
si por aqui ya paso un producto con esa URL, se actualiza (precio,
descripcion, activo, imagen, marca); si no, se crea. La URL es mas
estable que el nombre: el nombre cambia de redaccion, la URL no.

Ademas de precio y categoria, el producto lleva lo que el frontend pide
para mostrarse completo: url (el boton "ver en tienda"), imagen y marca.
"""

from __future__ import annotations

import logging

from scraper.domain.categoria import categoria_de
from scraper.domain.product import Product
from scraper.infrastructure.http.product_service_client import ProductServiceClient

log = logging.getLogger(__name__)


class ProductServiceSync:
    """
    Sincronizador hacia el Product Service. No sabe de HTML ni de SQL:
    solo recibe un Product del dominio y decide crear o actualizar.

    Mantiene un catalogo por categoria de prenda ("Poleras", "Zapatillas",
    "Pantalones"...), que es lo que el frontend espera ver como categoria.
    """

    def __init__(self, cliente: ProductServiceClient) -> None:
        self._cliente = cliente
        # categoria de prenda -> id del catalogo en Product Service
        self._catalogo_id: dict[str, int] = {}
        # url del producto -> id en Product Service, por catalogo
        self._existentes: dict[int, dict[str, int]] = {}
        self._cargados: set[int] = set()

    def sincronizar(self, producto: Product) -> bool:
        """Crea o actualiza un producto en Product Service. False si no hay nada que hacer."""
        catalogo_id = self._catalogo_para(categoria_de(producto))

        producto_id = self._id_existente(catalogo_id, producto.product_url)
        payload = {
            "nombre": producto.name,
            "descripcion": producto.description or "",
            "precio": producto.price,
            "catalogoId": catalogo_id,
            "activo": producto.available,
            "url": producto.product_url,
            "imagen": producto.image_card_url
            or producto.image_detail_url
            or producto.image_url,
            "marca": producto.brand or "",
        }

        if producto_id is not None:
            self._cliente.actualizar_producto(producto_id, payload)
            return True

        creado = self._cliente.crear_producto(payload)
        self._existentes[catalogo_id][producto.product_url] = creado["id"]
        return True

    def _catalogo_para(self, categoria: str) -> int:
        """El id del catalogo de la categoria, creandolo si hace falta."""
        if categoria in self._catalogo_id:
            return self._catalogo_id[categoria]

        catalogos = self._cliente.listar_catalogos()
        existente = next((c for c in catalogos if c.get("nombre") == categoria), None)
        if existente is not None:
            catalogo_id = int(existente["id"])
        else:
            creado = self._cliente.crear_catalogo(categoria, f"Categoria {categoria}")
            catalogo_id = int(creado["id"])

        self._catalogo_id[categoria] = catalogo_id
        return catalogo_id

    def _id_existente(self, catalogo_id: int, url: str) -> int | None:
        """Id del producto con esa URL dentro del catalogo, o None."""
        if catalogo_id not in self._cargados:
            existentes: dict[str, int] = {}
            for p in self._cliente.listar_productos(catalogo_id):
                existentes[p.get("url", "")] = int(p["id"])
            self._existentes[catalogo_id] = existentes
            self._cargados.add(catalogo_id)

        return self._existentes[catalogo_id].get(url)