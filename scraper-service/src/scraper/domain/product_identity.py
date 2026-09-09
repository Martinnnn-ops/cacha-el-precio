"""Identidad canónica compartida entre ofertas de distintas tiendas."""

from __future__ import annotations

import re
import unicodedata

_IGNORADAS = {
    "zapatilla",
    "zapatillas",
    "zapato",
    "zapatos",
    "calzado",
    "hombre",
    "mujer",
    "nino",
    "nina",
    "unisex",
    "adulto",
    "infantil",
    "urbana",
    "urbano",
    "color",
    "modelo",
    "talla",
    "blanco",
    "blanca",
    "negro",
    "negra",
    "azul",
    "rojo",
    "roja",
    "verde",
    "gris",
    "beige",
    "cafe",
    "rosado",
    "rosada",
    "the",
    "de",
    "del",
    "para",
    "con",
    "y",
}


def _normalizar(value: str) -> str:
    decomposed = unicodedata.normalize("NFKD", value.strip().lower())
    without_accents = "".join(c for c in decomposed if not unicodedata.combining(c))
    return re.sub(r"[^a-z0-9]+", " ", without_accents).strip()


def canonical_key(brand: str, name: str) -> str:
    """Clave estable por marca/modelo; color, género y talla no crean otro producto."""
    normalized_brand = _normalizar(brand)
    brand_tokens = set(normalized_brand.split())
    normalized_name = re.sub(r"\btalla\s+\d+(?:[.]\d+)?\b", " ", _normalizar(name))
    model_tokens = sorted(
        {
            token
            for token in normalized_name.split()
            if token not in _IGNORADAS and token not in brand_tokens
        }
    )
    model = "-".join(model_tokens) or _normalizar(name).replace(" ", "-")
    return f"{normalized_brand.replace(' ', '-')}:{model}"
