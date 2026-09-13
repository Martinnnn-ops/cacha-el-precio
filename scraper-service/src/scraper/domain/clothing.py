"""Clasificacion conservadora de productos aptos para el catalogo."""

from __future__ import annotations

import re
import unicodedata
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from scraper.domain.product import Product


_TERMINOS = {
    # Ropa
    "abrigo",
    "blazer",
    "blusa",
    "body",
    "boxer",
    "boxers",
    "bralette",
    "brasier",
    "brassiere",
    "calza",
    "calzon",
    "calzones",
    "calzoncillo",
    "calzoncillos",
    "calcetin",
    "calcetines",
    "camisa",
    "camiseta",
    "chaqueta",
    "chaleco",
    "colaless",
    "cortaviento",
    "enterito",
    "falda",
    "jeans",
    "jockey",
    "jogger",
    "legging",
    "leggings",
    "medias",
    "pantalon",
    "pantalones",
    "parka",
    "pijama",
    "polera",
    "poleron",
    "poncho",
    "short",
    "shorts",
    "sosten",
    "tanga",
    "sudadera",
    "sueter",
    "sweater",
    "top",
    "traje",
    "vestido",
    "vestuario",
    "cardigan",
    "jersey",
    # Calzado
    "bota",
    "botas",
    "botin",
    "botines",
    "calzado",
    "mocasin",
    "mocasines",
    "pantufla",
    "pantuflas",
    "sandalia",
    "sandalias",
    "zapatilla",
    "zapatillas",
    "zapato",
    "zapatos",
    # Accesorios de vestir
    "bikini",
    "bikinis",
    "banador",
    "bufanda",
    "bufandas",
    "cinturon",
    "cinturones",
    "corbata",
    "corbatas",
    "conjunto",
    "conjuntos",
    "gorro",
    "gorros",
    "guante",
    "guantes",
    "sombrero",
    "sombreros",
}

_FRASES = {
    "ropa interior",
    "traje de bano",
    "traje de baño",
    "chuck taylor",
    "all star",
    "run star",
}

_EXCLUSIONES = {
    "ropa de cama",
    "toalla",
    "toallas",
    "billetera",
    "billeteras",
}


def _normalizar(texto: str) -> str:
    texto = unicodedata.normalize("NFKD", texto.lower())
    texto = "".join(c for c in texto if not unicodedata.combining(c))
    return re.sub(r"[^a-z0-9]+", " ", texto).strip()


def texto_parece_vestimenta(texto_crudo: str) -> bool:
    """Clasifica un nombre, descripción o URL sin necesitar un ``Product``."""
    texto = _normalizar(texto_crudo)
    palabras = set(texto.split())
    if palabras & _EXCLUSIONES or any(_normalizar(frase) in texto for frase in _EXCLUSIONES):
        return False
    return bool(palabras & _TERMINOS) or any(_normalizar(frase) in texto for frase in _FRASES)


def es_vestimenta(producto: Product) -> bool:
    """Solo acepta productos cuyo contenido indique ropa o calzado."""
    return texto_parece_vestimenta(
        f"{producto.name} {producto.taxonomy_context or ''} "
        f"{producto.description or ''} {producto.product_url}"
    )
