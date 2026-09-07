# src/scraper/domain/categoria.py
"""
Clasifica el tipo de prenda a partir del nombre del producto.

Es_vestimenta decide SI algo entra al catalogo; esto decide en que
categoria entra. Se elige por palabras del nombre (que es lo unico
que el scraper puede saber sin ir a otra pagina): el nombre de un
producto casi siempre dice que prenda es.

El orden de las categorias importa: se recorre de arriba hacia abajo y
se queda con la primera que haga match. Por eso van primero las mas
especificas ("zapatilla" antes que "calzado").
"""

from __future__ import annotations

import re
import unicodedata
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from scraper.domain.product import Product

FALLBACK = "Vestimenta"

_CATEGORIAS: list[tuple[str, tuple[str, ...]]] = [
    ("Zapatillas", ("zapatilla", "zapatillas", "calzado", "running", "championes")),
    ("Botas", ("bota", "botas", "botin", "botines", "bomber")),
    ("Sandalias", ("sandalia", "sandalias", "chancla", "pantufla", "pantuflas")),
    ("Zapatos", ("zapato", "zapatos", "mocasin", "mocasines")),
    ("Polerones", ("poleron", "polerones", "buzo", "buzos", "hoodie", "sudadera")),
    ("Poleras", ("polera", "poleras", "camiseta", "t-shirt", "tshirt")),
    ("Camisas", ("camisa", "camisas", "blusa", "blusas", "polo")),
    ("Pantalones", (
        "pantalon", "pantalones", "jeans", "jogger", "joggers", "legging",
        "leggings", "calza", "calzas", "short", "shorts", "bermuda",
    )),
    ("Chaquetas", ("chaqueta", "jacket", "parka", "abrigo", "blazer", "cortaviento", "chaleco")),
    ("Vestidos", ("vestido", "vestidos", "falda", "faldas", "enterito")),
    ("Pijamas", ("pijama", "pijamas")),
    ("Ropa interior", ("boxer", "boxers", "calzoncillo", "sosten", "corpiño")),
    ("Medias", ("calcetin", "calcetines", "media", "medias")),
]


def _normalizar(texto: str) -> str:
    texto = unicodedata.normalize("NFKD", texto.lower())
    texto = "".join(c for c in texto if not unicodedata.combining(c))
    return re.sub(r"[^a-z0-9]+", " ", texto).strip()


def categoria_de(producto: Product) -> str:
    """Tipo de prenda del producto segun su nombre (ej. "Poleras")."""
    texto = _normalizar(producto.name)
    palabras = set(texto.split())
    for categoria, terminos in _CATEGORIAS:
        if palabras & set(terminos):
            return categoria
    return FALLBACK