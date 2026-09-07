# src/scraper/infrastructure/database/repositories/product_repository.py
"""
Contrato de persistencia y una implementacion en memoria.

El contrato vive aqui, en infrastructure, y no en domain, porque de
momento solo hay un consumidor. Si algun dia el dominio necesita
depender de el, se mueve a domain/ports.py y las implementaciones se
quedan aqui: eso es lo que pide Ports & Adapters.

La implementacion en memoria no es un juguete de pruebas: es lo que
permite probar los servicios sin levantar PostgreSQL.
"""

from __future__ import annotations

from typing import Protocol

from scraper.domain.clothing import es_vestimenta
from scraper.domain.offer import Offer
from scraper.domain.product import Product


class ProductRepository(Protocol):
    """Lo que los servicios necesitan de la persistencia."""

    def guardar(self, producto: Product) -> bool:
        """
        Guarda el estado actual del producto y, si el precio o la
        disponibilidad cambiaron, anade una fila al historial.

        Returns:
            True si se anadio historial (o sea, si algo cambio).
        """
        ...

    def obtener(self, store: str, external_id: str) -> Product | None: ...

    def historial(self, store: str, external_id: str, limite: int = 100) -> list[Offer]: ...


class RepositorioEnMemoria:
    """
    Implementacion en memoria del contrato.

    Por que solo se guarda historial cuando el precio cambia: el job
    corre varias veces al dia sobre cientos de productos. Guardar una
    fila por barrido llenaria la tabla de duplicados y los graficos
    tendrian miles de puntos identicos. Guardando solo los cambios, el
    historial ES la lista de cambios de precio, que es justo lo que
    quiere mostrar Cacha el Precio.
    """

    def __init__(self) -> None:
        self._productos: dict[tuple[str, str], Product] = {}
        self._historial: dict[tuple[str, str], list[Offer]] = {}

    def guardar(self, producto: Product) -> bool:
        if not es_vestimenta(producto):
            raise ValueError(f"producto fuera de vestimenta: {producto.product_url}")
        clave = producto.clave
        anterior = self._productos.get(clave)
        self._productos[clave] = producto

        cambio = (
            anterior is None
            or anterior.price != producto.price
            or anterior.available != producto.available
        )
        if cambio:
            self._historial.setdefault(clave, []).append(Offer.desde_producto(producto))
        return cambio

    def obtener(self, store: str, external_id: str) -> Product | None:
        return self._productos.get((store, external_id))

    def historial(self, store: str, external_id: str, limite: int = 100) -> list[Offer]:
        filas = self._historial.get((store, external_id), [])
        # Se desempata por orden de insercion: dos barridos muy seguidos
        # pueden compartir scraped_at, y sin el indice el orden quedaria
        # indefinido justo cuando mas importa (el precio recien visto).
        ordenadas = sorted(enumerate(filas), key=lambda par: (par[1].scraped_at, par[0]),
                           reverse=True)
        return [o for _, o in ordenadas][:limite]

    # --- utilidades para pruebas y para el CLI ---

    def todos(self) -> list[Product]:
        return list(self._productos.values())

    def __len__(self) -> int:
        return len(self._productos)
