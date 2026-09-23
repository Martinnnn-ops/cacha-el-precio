"""Taxonomía semántica común entre scraper, catálogo y armador de outfits.

La categoría describe *qué* es la prenda; ``body_area`` indica *dónde* se
usa y ``layer`` en qué capa del outfit entra. Mantener los tres conceptos
separados permite combinar, por ejemplo, polera + polerón y calcetines +
zapatillas sin confundirlos por ocupar la misma zona corporal.
"""

from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from scraper.domain.product import Product

FALLBACK = "Vestimenta"
ZONA_FALLBACK = "Cuerpo"
GENERO_FALLBACK = "Unisex"
CAPA_FALLBACK = "General"


@dataclass(frozen=True)
class Clasificacion:
    categoria: str
    zona_corporal: str
    genero: str
    capa: str


# Las reglas específicas van primero. Los términos se comparan como palabras
# completas para que, por ejemplo, "running" no active "calzado".
_CATEGORIAS: list[tuple[str, str, str, tuple[str, ...]]] = [
    (
        "Ropa interior femenina",
        "Piernas",
        "Ropa interior",
        ("calzon", "calzones", "colaless", "tanga", "bombacha"),
    ),
    (
        "Ropa interior femenina",
        "Torso",
        "Ropa interior",
        ("sosten", "brasier", "brassiere", "bralette"),
    ),
    (
        "Ropa interior masculina",
        "Piernas",
        "Ropa interior",
        ("boxer", "boxers", "calzoncillo", "calzoncillos", "slip"),
    ),
    ("Zapatillas", "Pies", "Calzado", ("zapatilla", "zapatillas", "championes")),
    ("Botas", "Pies", "Calzado", ("bota", "botas", "botin", "botines")),
    ("Sandalias", "Pies", "Calzado", ("sandalia", "sandalias", "chancla", "chanclas")),
    ("Pantuflas", "Pies", "Calzado", ("pantufla", "pantuflas")),
    ("Zapatos", "Pies", "Calzado", ("zapato", "zapatos", "mocasin", "mocasines", "calzado")),
    ("Calcetines", "Pies", "Calcetería", ("calcetin", "calcetines", "media", "medias")),
    ("Gorros", "Cabeza", "Accesorio", ("gorro", "gorros", "beanie")),
    ("Jockeys", "Cabeza", "Accesorio", ("jockey", "jockeys", "cap", "caps")),
    ("Sombreros", "Cabeza", "Accesorio", ("sombrero", "sombreros")),
    (
        "Polerones",
        "Torso",
        "Intermedia",
        ("poleron", "polerones", "hoodie", "hoodies", "sudadera", "sudaderas"),
    ),
    ("Poleras", "Torso", "Base", ("polera", "poleras", "camiseta", "camisetas", "tshirt")),
    ("Blusas", "Torso", "Base", ("blusa", "blusas")),
    ("Camisas", "Torso", "Base", ("camisa", "camisas", "polo", "polos")),
    (
        "Abrigos",
        "Torso",
        "Abrigo",
        ("abrigo", "abrigos", "parka", "parkas", "poncho", "ponchos"),
    ),
    (
        "Chalecos",
        "Torso",
        "Intermedia",
        ("chaleco", "chalecos", "cardigan", "cardigans", "sweater", "sueter", "jersey"),
    ),
    (
        "Chaquetas",
        "Torso",
        "Abrigo",
        ("chaqueta", "chaquetas", "jacket", "jackets", "blazer", "cortaviento"),
    ),
    ("Shorts", "Piernas", "Inferior", ("short", "shorts", "bermuda", "bermudas")),
    ("Faldas", "Piernas", "Inferior", ("falda", "faldas")),
    ("Jeans", "Piernas", "Inferior", ("jean", "jeans", "denim")),
    ("Calzas", "Piernas", "Inferior", ("calza", "calzas", "legging", "leggings")),
    ("Pantalones", "Piernas", "Inferior", ("pantalon", "pantalones", "jogger", "joggers")),
    ("Vestidos", "Cuerpo completo", "Entero", ("vestido", "vestidos")),
    (
        "Enterizos",
        "Cuerpo completo",
        "Entero",
        ("enterito", "enteritos", "enterizo", "enterizos", "jumpsuit"),
    ),
    ("Overoles", "Cuerpo completo", "Entero", ("overol", "overoles")),
    (
        "Conjuntos",
        "Cuerpo completo",
        "Entero",
        ("conjunto", "conjuntos", "traje", "trajes", "buzo", "buzos"),
    ),
    ("Pijamas", "Cuerpo completo", "Entero", ("pijama", "pijamas")),
    (
        "Trajes de baño",
        "Cuerpo completo",
        "Entero",
        ("bikini", "bikinis", "banador", "trikini", "trikinis"),
    ),
    (
        "Accesorios",
        "Cuerpo",
        "Accesorio",
        (
            "bufanda",
            "bufandas",
            "cinturon",
            "cinturones",
            "corbata",
            "corbatas",
            "guante",
            "guantes",
        ),
    ),
]

_FRASES_INTERIOR = ("ropa interior", "lenceria")
_FEMENINO = {"mujer", "mujeres", "dama", "damas", "femenino", "femenina"}
_MASCULINO = {"hombre", "hombres", "varon", "varones", "masculino", "masculina"}
_NINAS = {"nina", "ninas"}
_NINOS = {"nino", "ninos"}
_BEBES = {"bebe", "bebes"}
_UNISEX = {"unisex"}


def _normalizar(texto: str) -> str:
    texto = unicodedata.normalize("NFKD", texto.lower())
    texto = "".join(c for c in texto if not unicodedata.combining(c))
    return re.sub(r"[^a-z0-9]+", " ", texto).strip()


def _texto_producto(producto: Product) -> str:
    # La descripción ayuda con productos cuyo título sólo dice "Conjunto";
    # la URL aporta género/categoría en varias tiendas.
    return _normalizar(
        f"{producto.name} {producto.taxonomy_context or ''} "
        f"{producto.description or ''} {producto.product_url}"
    )


def genero_de(producto: Product) -> str:
    fuentes = (
        producto.name,
        f"{producto.taxonomy_context or ''} {producto.product_url}",
        producto.description or "",
    )
    for fuente in fuentes:
        palabras = set(_normalizar(fuente).split())
        if palabras & _UNISEX:
            return "Unisex"
        if palabras & _NINAS:
            return "Niña"
        if palabras & _NINOS:
            return "Niño"
        if palabras & _BEBES:
            return "Bebé"
        if palabras & _FEMENINO:
            return "Mujer"
        if palabras & _MASCULINO:
            return "Hombre"
    return GENERO_FALLBACK


def clasificacion_de(producto: Product) -> Clasificacion:
    fuentes = (
        _normalizar(producto.name),
        _normalizar(f"{producto.taxonomy_context or ''} {producto.product_url}"),
        _normalizar(producto.description or ""),
    )

    for texto_fuente in fuentes:
        palabras = set(texto_fuente.split())
        for categoria, zona, capa, terminos in _CATEGORIAS:
            if palabras & set(terminos):
                return Clasificacion(categoria, zona, genero_de(producto), capa)

    texto = _texto_producto(producto)

    if any(frase in texto for frase in _FRASES_INTERIOR):
        genero = genero_de(producto)
        if genero in {"Mujer", "Niña"}:
            categoria = "Ropa interior femenina"
        elif genero in {"Hombre", "Niño"}:
            categoria = "Ropa interior masculina"
        else:
            categoria = "Ropa interior unisex"
        return Clasificacion(categoria, "Cuerpo", genero, "Ropa interior")

    return Clasificacion(FALLBACK, ZONA_FALLBACK, genero_de(producto), CAPA_FALLBACK)


def categoria_de(producto: Product) -> str:
    return clasificacion_de(producto).categoria


def zona_corporal_de(producto: Product) -> str:
    return clasificacion_de(producto).zona_corporal


def capa_de(producto: Product) -> str:
    return clasificacion_de(producto).capa
