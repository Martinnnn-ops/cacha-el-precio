"""Política editorial: marcas sin identidad comercial y público adolescente/adulto."""

import re
import unicodedata
from hashlib import sha256


def normalizar(texto: str) -> str:
    texto = unicodedata.normalize("NFKD", texto.casefold())
    return re.sub(
        r"[^a-z0-9]+", " ", "".join(c for c in texto if not unicodedata.combining(c))
    ).strip()


def marca_catalogo(marca: str | None) -> str:
    # Una marca poco conocida NO es necesariamente genérica.
    aliases = {
        "",
        "generica",
        "generico",
        "genericas",
        "genericos",
        "generic",
        "sin marca",
        "no brand",
        "unbranded",
        "s m",
        "n a",
        "marca generica",
    }
    return "Genéricas" if normalizar(marca or "") in aliases else (marca or "").strip()


def clave_generica(tienda: str, id_externo: str) -> str:
    # El SKU es opaco: no ordenar sus palabras ni quitar colores como en un nombre.
    identidad = f"{tienda.strip().lower()}\0{id_externo.strip()}"
    return "generica:" + sha256(identidad.encode("utf-8")).hexdigest()


def motivo_infantil(nombre: str, contexto: str = "") -> str | None:
    # No se usan descripción, talla, 'junior', 'teen' ni 'juvenil': son ambiguos.
    # Baby tee / baby doll son cortes de ropa de adultos, no edades.
    texto = normalizar(f"{nombre} {contexto}")
    texto = re.sub(r"\bbaby (tee|doll)\b", "", texto)
    patron = (
        r"\b(bebe|bebes|baby|babies|infantil|infantiles|infant|infants|"
        r"nino|ninos|nina|ninas|kids|kid|toddler|toddlers|newborn|"
        r"recien nacido|recien nacida|preescolar|children)\b"
    )
    coincidencia = re.search(patron, texto)
    return coincidencia.group(0) if coincidencia else None
