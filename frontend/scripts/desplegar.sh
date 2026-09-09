#!/usr/bin/env bash
#
# Publica el sitio en S3 y refresca la caché de CloudFront.
#
# Uso:
#   BUCKET=mi-bucket DISTRIBUCION=E123ABC ./scripts/desplegar.sh
#
# Requiere la AWS CLI configurada (aws configure) con permisos de escritura en
# el bucket y de invalidación en la distribución.

set -euo pipefail

: "${BUCKET:?Define BUCKET con el nombre del bucket de S3}"
: "${DISTRIBUCION:=}"

echo "▸ Compilando…"
npm run build

echo "▸ Comprobando el resultado…"
# Que ads.txt llegue al build es la diferencia entre cobrar y no cobrar: si
# falta, Google deja de considerarte editor autorizado.
for archivo in index.html ads.txt robots.txt sitemap.xml; do
  [ -f "dist/$archivo" ] || { echo "  FALTA dist/$archivo"; exit 1; }
done

# ——————————————————————————————————————————————————————————————
# La caché va en dos grupos, y la diferencia importa:
#
#   assets/  llevan un hash en el nombre, así que un cambio genera un nombre
#            nuevo. Se pueden cachear un año sin riesgo.
#
#   el resto no lleva hash. Si se cachearan igual, una corrección tardaría
#            días en verse, y ads.txt es justo el archivo que Google
#            reconsulta cuando cambias algo en tu cuenta.
# ——————————————————————————————————————————————————————————————

echo "▸ Subiendo assets con caché larga…"
aws s3 sync dist/ "s3://$BUCKET/" \
  --delete \
  --exclude '*' \
  --include 'assets/*' \
  --cache-control 'public, max-age=31536000, immutable'

echo "▸ Subiendo el resto con caché corta…"
aws s3 sync dist/ "s3://$BUCKET/" \
  --delete \
  --exclude 'assets/*' \
  --cache-control 'public, max-age=300, must-revalidate'

# S3 adivina el tipo de contenido por la extensión, pero si alguna vez se sube
# con otra herramienta puede quedar como binary/octet-stream, y entonces Google
# no lo lee. Se fija explícitamente por si acaso.
echo "▸ Fijando el tipo de contenido de ads.txt…"
aws s3 cp "s3://$BUCKET/ads.txt" "s3://$BUCKET/ads.txt" \
  --metadata-directive REPLACE \
  --content-type 'text/plain; charset=utf-8' \
  --cache-control 'public, max-age=300, must-revalidate'

if [ -n "$DISTRIBUCION" ]; then
  echo "▸ Invalidando CloudFront…"
  # Sólo los archivos sin hash: los de assets/ nunca cambian de contenido bajo
  # el mismo nombre, así que invalidarlos sería tirar dinero (las
  # invalidaciones se cobran por ruta).
  aws cloudfront create-invalidation \
    --distribution-id "$DISTRIBUCION" \
    --paths '/index.html' '/ads.txt' '/robots.txt' '/sitemap.xml' \
    --query 'Invalidation.Id' --output text
else
  echo "▸ Sin DISTRIBUCION: no se invalida la caché."
  echo "  Los cambios pueden tardar en verse."
fi

echo "▸ Listo."
