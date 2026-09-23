#!/usr/bin/env python3
"""Audita y elimina prendas infantiles con respaldo completo previo (sin cambiar esquema).

Usa DATABASE_URL del entorno. Ejecutar con el Python del scraper (psycopg).
Por defecto solo muestra candidatos. --apply requiere --backup /ruta/respaldo.sql.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys

import psycopg

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scraper-service/src"))
from scraper.domain.catalog_policy import motivo_infantil  # noqa: E402


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--backup", type=Path)
    args = parser.parse_args()
    if args.apply and not args.backup:
        parser.error("--apply exige --backup; nunca se sobrescribe un archivo existente")
    url = os.environ.get("DATABASE_URL")
    if not url:
        parser.error("Define DATABASE_URL sin escribir la contraseña en el historial del terminal")
    with psycopg.connect(url, connect_timeout=10) as conn:
        with conn.cursor() as cur:
            # Mantiene estable el conjunto auditado durante respaldo y eliminación.
            if args.apply:
                cur.execute('LOCK TABLE product."Products", product."ProductOffers", '
                            'scraper.products, scraper.price_history IN SHARE ROW EXCLUSIVE MODE')
            cur.execute('SELECT "ProductId", "ProductName", "ProductCategory", "Gender" '
                        'FROM product."Products"')
            catalogo = [dict(id=i, nombre=n, motivo=motivo_infantil(n, f"{c} {g}"))
                        for i, n, c, g in cur.fetchall() if motivo_infantil(n, f"{c} {g}")]
            cur.execute('SELECT "Store", "ExternalId" FROM product."ProductOffers" '
                        'WHERE "ProductId" = ANY(%s)', ([p["id"] for p in catalogo],))
            ofertas_infantiles = set(cur.fetchall())
            cur.execute('SELECT store, external_id, name FROM scraper.products')
            captura = [dict(tienda=s, id=i, nombre=n,
                            motivo=motivo_infantil(n) or "oferta infantil del catálogo")
                       for s, i, n in cur.fetchall()
                       if motivo_infantil(n) or (s, i) in ofertas_infantiles]
            print(json.dumps({"product": catalogo, "scraper": captura}, ensure_ascii=False, indent=2))
            if not args.apply:
                print("Solo auditoría. No se modificó la base.")
                return
            if not catalogo and not captura:
                print("Sin candidatos; no se modificó la base.")
                return
            # pg_dump en otra conexión toma locks de lectura compatibles. Fallo = rollback.
            fd = os.open(args.backup, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
            with os.fdopen(fd, "wb") as backup:
                result = subprocess.run(["pg_dump", "--no-owner", "--no-acl"],
                    env={**os.environ, "PGDATABASE": url}, stdout=backup, stderr=subprocess.PIPE)
                backup.flush()
                os.fsync(backup.fileno())
            if result.returncode:
                raise RuntimeError("Falló pg_dump: no se eliminó nada; el archivo parcial no es un respaldo válido.")
            for p in catalogo:
                # Las ofertas tienen FK con cascada. Las referencias personales quedan como no disponibles.
                cur.execute('DELETE FROM product."Products" WHERE "ProductId" = %s', (p["id"],))
            for p in captura:
                cur.execute('DELETE FROM scraper.price_history WHERE store=%s AND external_id=%s',
                            (p["tienda"], p["id"]))
                cur.execute('DELETE FROM scraper.products WHERE store=%s AND external_id=%s',
                            (p["tienda"], p["id"]))
        conn.commit()
        print(f"Eliminados: {len(catalogo)} productos canónicos y {len(captura)} capturas. Respaldo: {args.backup}")


if __name__ == "__main__":
    main()
