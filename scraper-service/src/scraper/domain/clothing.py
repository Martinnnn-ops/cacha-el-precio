"""Clasificacion conservadora de productos aptos para el catalogo."""

from __future__ import annotations

import re
import unicodedata
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from scraper.domain.product import Product


_TERMINOS = {
    # Ropa
    "abrigo", "blazer", "blusa", "body", "boxer", "calza", "calcetin",
    "calcetines", "camisa", "camiseta", "chaqueta", "chaleco", "cortaviento",
    "enterito", "falda", "jeans", "jockey", "jogger", "legging", "leggings",
    "medias", "pantalon", "pantalones", "parka", "pijama", "polera", "poleron",
    "poncho", "short", "shorts", "sosten", "sudadera", "sueter", "sweater",
    "traje", "vestido", "vestuario",
    # Calzado
    "bota", "botas", "botin", "botines", "calzado", "mocasin", "mocasines",
    "pantufla", "pantuflas", "sandalia", "sandalias", "zapatilla", "zapatillas",
    "zapato", "zapatos",
}

_FRASES = {
    "ropa interior", "traje de bano", "traje de baño", "chuck taylor",
    "all star", "run star",
}

_EXCLUSIONES = {
    "ropa de cama", "toalla", "toallas", "billetera", "billeteras",
}


def _normalizar(texto: str) -> str:
    texto = unicodedata.normalize("NFKD", texto.lower())
    texto = "".join(c for c in texto if not unicodedata.combining(c))
    return re.sub(r"[^a-z0-9]+", " ", texto).strip()


def es_vestimenta(producto: Product) -> bool:
    """Solo acepta productos cuyo nombre o URL indiquen ropa o calzado."""
    texto = _normalizar(f"{producto.name} {producto.product_url}")
    palabras = set(texto.split())
    if palabras & _EXCLUSIONES or any(frase in texto for frase in _EXCLUSIONES):
        return False
    return bool(palabras & _TERMINOS) or any(
        _normalizar(frase) in texto for frase in _FRASES
    )
