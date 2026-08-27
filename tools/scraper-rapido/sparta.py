#!/usr/bin/env python3
"""
Scraper de arranque para Sparta (Magento GraphQL publico).

Esto NO es el scraper-service definitivo: ese es una Lambda de Micronaut y va
en su propio modulo. Este script existe por una sola razon, y conviene tenerla
clara antes de tocarlo:

    El historial de precios solo existe si empieza a acumularse. Cada dia que
    no corre es un hueco que despues no se recupera. TAREAS.md lo pedia para
    la semana 0 y no se hizo, asi que arranca ahora aunque sea feo.

Guarda el JSON crudo tal como lo devuelve la tienda, sin normalizar nada. El
crudo es la fuente de verdad (ADR-011): cuando exista el scraper de verdad,
estas capturas se pueden re-ingestar y el historial no se pierde.

Sin dependencias externas a proposito: solo stdlib. Un cron que se cae porque
un venv se movio no sirve de nada.
"""

import gzip
import json
import os
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone

ENDPOINT = "https://www.sparta.cl/graphql"

# Marcas por id del atributo gral_marca, NO por texto libre.
#
# Medido el 27-08 contra el catalogo real: buscar "nike zapatillas" devuelve
# 1126 resultados de los cuales solo 24 son Nike de verdad. La busqueda de
# Magento es difusa y el termino "zapatillas" se come el de marca, asi que
# las cuatro marcas devolvian practicamente los mismos productos.
#
# Con el filtro, los totales reales de Sparta son otra historia:
#   New Balance 857 | Adidas 752 | Joma 267 | Asics 96 | Nike 89 | Puma 27
# Sparta es una tienda de New Balance y Adidas. Nike y Puma son marginales,
# justo las dos que PLAN.md usa de ejemplo canonico -> ver BITACORA 27-08.
MARCAS = {
    "new-balance": "21",
    "adidas": "3",
    "joma": "18644",
    "asics": "447",
    "nike": "23",
    "puma": "30",
}

PAGE_SIZE = 50          # tope comodo: Magento acepta mas, pero responde lento
MAX_PAGINAS = 40        # cortafuegos. Con filtro real la marca mas grande son 18 paginas
PAUSA_SEGUNDOS = 1.5    # no le pegamos fuerte a una tienda que no nos debe nada
TIMEOUT = 45
REINTENTOS = 3

# La query pide exactamente lo que el modelo de datos necesita y nada mas:
# sku (trae el style code adentro), nombre, precio final, precio de lista,
# y stock por talla via las variantes del producto configurable.
QUERY = """
query ProductosPorMarca($marca: String!, $pagina: Int!, $tam: Int!) {
  products(filter: {gral_marca: {eq: $marca}}, pageSize: $tam, currentPage: $pagina) {
    total_count
    page_info { current_page total_pages }
    items {
      sku
      name
      url_key
      stock_status
      price_range {
        minimum_price {
          final_price { value currency }
          regular_price { value currency }
          discount { amount_off percent_off }
        }
      }
      ... on ConfigurableProduct {
        variants {
          product { sku stock_status }
          attributes { label code value_index }
        }
      }
    }
  }
}
"""


def raiz_datos():
    """Las capturas viven fuera de git (.gitignore ya tiene 'capturas/')."""
    aqui = os.path.dirname(os.path.abspath(__file__))
    repo = os.path.abspath(os.path.join(aqui, "..", ".."))
    return os.path.join(repo, "capturas", "sparta")


def pedir(id_marca, pagina):
    """Una llamada al GraphQL, con reintentos y espera creciente."""
    cuerpo = json.dumps({
        "query": QUERY,
        "variables": {"marca": id_marca, "pagina": pagina, "tam": PAGE_SIZE},
    }).encode("utf-8")

    peticion = urllib.request.Request(
        ENDPOINT,
        data=cuerpo,
        headers={
            "Content-Type": "application/json",
            # Nos identificamos de verdad. Si a Sparta le molesta el trafico,
            # que sepan a quien escribirle en vez de banear a ciegas.
            "User-Agent": "cacha-el-precio/0.1 (proyecto universitario DSY1107)",
            "Accept": "application/json",
        },
        method="POST",
    )

    ultimo_error = None
    for intento in range(1, REINTENTOS + 1):
        try:
            with urllib.request.urlopen(peticion, timeout=TIMEOUT) as r:
                datos = json.loads(r.read().decode("utf-8"))
            if "errors" in datos:
                raise RuntimeError(f"GraphQL respondio errores: {datos['errors']}")
            return datos["data"]["products"]
        except Exception as e:                      # noqa: BLE001 - queremos reintentar todo
            ultimo_error = e
            if intento < REINTENTOS:
                espera = 2 ** intento
                print(f"    intento {intento} fallo ({e}); reintento en {espera}s",
                      file=sys.stderr)
                time.sleep(espera)

    raise RuntimeError(f"agotados los {REINTENTOS} intentos: {ultimo_error}")


def capturar_marca(id_marca):
    """Pagina hasta agotar la marca. Devuelve (items, total_declarado)."""
    items = []
    total_declarado = None
    pagina = 1

    while pagina <= MAX_PAGINAS:
        bloque = pedir(id_marca, pagina)

        if total_declarado is None:
            total_declarado = bloque.get("total_count")

        recibidos = bloque.get("items") or []
        items.extend(recibidos)

        info = bloque.get("page_info") or {}
        total_paginas = info.get("total_pages") or 1
        print(f"    pagina {pagina}/{total_paginas}: {len(recibidos)} productos")

        if pagina >= total_paginas or not recibidos:
            break

        pagina += 1
        time.sleep(PAUSA_SEGUNDOS)

    return items, total_declarado


def main():
    inicio = datetime.now(timezone.utc)
    sello = inicio.strftime("%Y%m%dT%H%M%SZ")

    destino = os.path.join(raiz_datos(), inicio.strftime("%Y-%m-%d"))
    os.makedirs(destino, exist_ok=True)

    print(f"[{sello}] captura de Sparta -> {destino}")

    resultados = {}
    errores = {}

    for marca, id_marca in MARCAS.items():
        print(f"  marca: {marca} (gral_marca={id_marca})")
        try:
            items, total = capturar_marca(id_marca)
            resultados[marca] = items
            print(f"    -> {len(items)} productos (la tienda declara {total})")
        except Exception as e:                      # noqa: BLE001
            errores[marca] = str(e)
            print(f"    !! {marca} fallo: {e}", file=sys.stderr)
        time.sleep(PAUSA_SEGUNDOS)

    fin = datetime.now(timezone.utc)
    productos_ok = sum(len(v) for v in resultados.values())

    # El sobre lleva su propia metadata: sin esto, un JSON suelto dentro de seis
    # meses no dice de cuando es ni con que query se saco. Ademas es justo lo
    # que la tabla ejecucion_captura va a querer despues.
    sobre = {
        "tienda": "sparta",
        "plataforma": "magento-graphql",
        "endpoint": ENDPOINT,
        "capturado_en": inicio.isoformat(),
        "termino_en": fin.isoformat(),
        "duracion_segundos": round((fin - inicio).total_seconds(), 1),
        "version_scraper": "0.1-bootstrap",
        "marcas_pedidas": MARCAS,
        "filtro": "gral_marca",
        "productos_ok": productos_ok,
        "marcas_con_error": errores,
        "estado": "ok" if not errores else ("parcial" if resultados else "fallido"),
        "productos": resultados,
    }

    archivo = os.path.join(destino, f"sparta_{sello}.json.gz")
    with gzip.open(archivo, "wt", encoding="utf-8") as f:
        json.dump(sobre, f, ensure_ascii=False)

    tam_kb = os.path.getsize(archivo) / 1024
    print(f"[{sello}] {sobre['estado']}: {productos_ok} productos -> "
          f"{archivo} ({tam_kb:.0f} KB)")

    # Bitacora de una linea por corrida: sirve para ver de un vistazo si el cron
    # se cayo alguna noche, sin abrir ningun .gz.
    with open(os.path.join(raiz_datos(), "corridas.log"), "a", encoding="utf-8") as f:
        f.write(f"{inicio.isoformat()}\t{sobre['estado']}\t{productos_ok}\t"
                f"{sobre['duracion_segundos']}s\t{os.path.basename(archivo)}\n")

    # Salida distinta de 0 si no se trajo nada: asi el cron lo puede detectar.
    return 0 if productos_ok else 1


if __name__ == "__main__":
    sys.exit(main())
