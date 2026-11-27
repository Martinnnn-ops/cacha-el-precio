#!/usr/bin/env python3
"""
Rellena el historial de precios de la ficha con lo que ya existia antes de
Kinesis, y recalcula el precio habitual desde ahi.

POR QUE. Kinesis empezo a contar el 27-11: sin relleno, cada oferta parte con
1 o 2 muestras, el gráfico sale vacío y el precio habitual dice "insuficiente"
(hacen falta 5). Pero el historial existe en dos lados:

  1. capturas/sparta/  -> el scraper rapido del notebook, 3 veces al dia desde
                          el 27-08 (JSON crudo, ver tools/scraper-rapido).
  2. scraper.price_history -> en la base, los CAMBIOS de precio que vio el
                          scraper-service.

Las dos fuentes se cargan como filas de product."ObservacionesPrecio" (la
misma tabla que llena el lector de Kinesis) y las estadisticas se rehacen desde
esa tabla. Solo entran las ofertas que estan en el catalogo (ProductOffers).

Se puede correr las veces que haga falta: una observacion repetida (misma
tienda, id y fecha) se ignora, y las estadisticas se recalculan completas.

Uso (con AWS prendido, desde la raiz del repo):
    python3 tools/rellenar-historial.py            # carga y recalcula
    python3 tools/rellenar-historial.py --seco    # solo cuenta, no toca la base
"""

import argparse
import csv
import glob
import gzip
import io
import os
import subprocess
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def leer_env(nombre, clave):
    ruta = os.path.join(RAIZ, nombre)
    with open(ruta, encoding="utf-8") as f:
        for linea in f:
            if linea.startswith(f"{clave}="):
                return linea.split("=", 1)[1].strip().strip('"')
    sys.exit(f"falta {clave} en {nombre}")


def observaciones_sparta():
    """(external_id, capturado_en ISO, precio) de cada captura cruda de Sparta."""
    archivos = sorted(glob.glob(os.path.join(RAIZ, "capturas", "sparta", "*", "sparta_*.json.gz")))
    vistas = set()
    for ruta in archivos:
        import json

        try:
            with gzip.open(ruta, "rt", encoding="utf-8") as f:
                captura = json.load(f)
        except (OSError, ValueError) as e:
            print(f"  ⚠️ {os.path.basename(ruta)} no se pudo leer ({e}); se salta", file=sys.stderr)
            continue
        cuando = captura.get("capturado_en")
        if not cuando:
            continue
        for productos in (captura.get("productos") or {}).values():
            for p in productos or []:
                sku = (p.get("sku") or "").strip()
                try:
                    precio = int(p["price_range"]["minimum_price"]["final_price"]["value"])
                except (KeyError, TypeError, ValueError):
                    continue
                # Un mismo producto puede venir en dos marcas de la misma captura.
                if not sku or precio <= 0 or (sku, cuando) in vistas:
                    continue
                vistas.add((sku, cuando))
                yield sku, cuando, precio
    print(f"  {len(archivos)} capturas de Sparta leidas del notebook", file=sys.stderr)


def armar_sql(seco):
    datos = io.StringIO()
    escritor = csv.writer(datos)
    n = 0
    for sku, cuando, precio in observaciones_sparta():
        escritor.writerow(["sparta", sku, cuando, precio])
        n += 1
    print(f"  {n} precios de Sparta listos para cruzar con el catalogo", file=sys.stderr)

    final = "ROLLBACK;" if seco else "COMMIT;"
    return f"""
\\set ON_ERROR_STOP on
BEGIN;
CREATE TEMP TABLE relleno (store text, external_id text, capturado_en timestamptz, precio int) ON COMMIT DROP;
\\copy relleno FROM STDIN WITH (FORMAT csv)
{datos.getvalue()}\\.

-- Fuente 2: los cambios de precio que vio el scraper-service.
INSERT INTO relleno
SELECT store, external_id, scraped_at, price FROM scraper.price_history WHERE price > 0;

-- Solo lo que esta en el catalogo (la tienda en minusculas, como la usa la ficha).
INSERT INTO product."ObservacionesPrecio" ("Store", "ExternalId", "CapturadoEn", "Precio")
SELECT DISTINCT ON (o."Store", r.external_id, r.capturado_en) o."Store", r.external_id, r.capturado_en, r.precio
FROM relleno r
JOIN product."ProductOffers" o ON lower(o."Store") = lower(r.store) AND o."ExternalId" = r.external_id
ON CONFLICT DO NOTHING;

-- El precio habitual, rehecho desde todas las observaciones.
INSERT INTO product."EstadisticasPrecio"
  ("Store", "ExternalId", "Muestras", "Suma", "SumaCuadrados", "Minimo", "Maximo", "UltimoPrecio", "UltimaCaptura")
SELECT "Store", "ExternalId", count(*), sum("Precio"), sum(("Precio")::numeric * "Precio"),
       min("Precio"), max("Precio"),
       (array_agg("Precio" ORDER BY "CapturadoEn" DESC))[1], max("CapturadoEn")
FROM product."ObservacionesPrecio"
GROUP BY "Store", "ExternalId"
ON CONFLICT ("Store", "ExternalId") DO UPDATE SET
  "Muestras" = EXCLUDED."Muestras", "Suma" = EXCLUDED."Suma", "SumaCuadrados" = EXCLUDED."SumaCuadrados",
  "Minimo" = EXCLUDED."Minimo", "Maximo" = EXCLUDED."Maximo",
  "UltimoPrecio" = EXCLUDED."UltimoPrecio", "UltimaCaptura" = EXCLUDED."UltimaCaptura";

SELECT 'observaciones' AS que, count(*) FROM product."ObservacionesPrecio"
UNION ALL SELECT 'ofertas con historial', count(DISTINCT ("Store", "ExternalId")) FROM product."ObservacionesPrecio"
UNION ALL SELECT 'ofertas con 5+ muestras', count(*) FROM product."EstadisticasPrecio" WHERE "Muestras" >= 5;
{final}
"""


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--seco", action="store_true", help="hace todo dentro de una transaccion y la deshace")
    args = ap.parse_args()

    ip = leer_env("infra.env", "INFRA_IP")
    llave = os.environ.get("LLAVE", os.path.expanduser("~/labsuser.pem"))
    sql = armar_sql(args.seco)

    remoto = (
        "cd /opt/cacha-el-precio && set -a; source .env; set +a; "
        "docker compose exec -T postgres psql -q -U $DB_USER -d $DB_NAME"
    )
    print(f"  enviando a {ip}{' (en seco)' if args.seco else ''}...", file=sys.stderr)
    r = subprocess.run(
        ["ssh", "-i", llave, "-o", "ConnectTimeout=15", f"ec2-user@{ip}", remoto],
        input=sql.encode(), check=False,
    )
    sys.exit(r.returncode)


if __name__ == "__main__":
    main()
