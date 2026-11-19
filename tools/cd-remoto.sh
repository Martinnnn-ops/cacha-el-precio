#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Lo que el CD (.github/workflows/cd.yml) corre DENTRO de la EC2, por SSH.
#
# POR QUE ES UN ARCHIVO Y NO VA ESCRITO EN EL YAML
#   Se puede leer, probar y revisar como cualquier script, y el YAML queda
#   corto. El CD lo manda por SSH: ssh ... "bash -s -- <commit>" < este archivo
#
# QUE HACE
#   1. Comprueba que la maquina ya fue desplegada alguna vez (.env presente).
#      Una maquina nueva la monta tools/tf.sh subir, no el CD.
#   2. Deja el repo en el commit que se despliega (compose, Caddyfile, init SQL).
#   3. Baja de GHCR las 4 imagenes de ese commit. La EC2 NO compila.
#   4. Levanta el compose sin compilar y espera /health a traves de Caddy.
#   5. Si /health no responde, vuelve a la version anterior (rollback).
#
# El login a GHCR lo hace el CD antes, en una llamada SSH aparte, para que el
# token no viaje dentro de este script.
#
# Uso (lo hace el CD):  bash -s -- <commit> < tools/cd-remoto.sh
# ---------------------------------------------------------------------------
set -uo pipefail

COMMIT="${1:?falta el commit a desplegar}"
DESTINO="/opt/cacha-el-precio"
SERVICIOS=(gateway product-service notification-service scraper-api)

# 1. Una maquina nunca desplegada no tiene .env: ese lo arma desplegar.sh con los
#    datos de Terraform, que GitHub no tiene.
if [[ ! -f "$DESTINO/.env" ]]; then
  echo "::error::La EC2 no tiene $DESTINO/.env. Una maquina nueva se monta con ./tools/tf.sh subir <nombre>."
  exit 1
fi
cd "$DESTINO"

# Lo que corre ahora, para poder volver atras.
COMMIT_ANTERIOR="$(git rev-parse HEAD)"
TAG_ANTERIOR="$(cat .imagen-tag 2>/dev/null || echo local)"
echo "  ahora corre: commit ${COMMIT_ANTERIOR:0:7}, imagenes :${TAG_ANTERIOR:0:12}"

# 2. Codigo: solo hace falta por los archivos que se montan (compose, Caddyfile,
#    init SQL). El .env y .imagen-tag no estan en git y no se tocan.
git fetch -q origin "$COMMIT" && git checkout -q --detach "$COMMIT" || {
  echo "::error::No se pudo dejar el repo en $COMMIT"; exit 1; }
echo "  repo en ${COMMIT:0:7}"

# 3 y 4. Bajar y levantar.
levantar() {
  IMAGEN_TAG="$1" docker compose pull --quiet "${SERVICIOS[@]}" &&
  IMAGEN_TAG="$1" docker compose up -d --no-build --remove-orphans
}

# /health a traves de Caddy, con el encabezado del borde, igual que desplegar.sh.
sano() {
  local secreto codigo
  secreto="$(grep -E '^BORDE_SECRETO=' .env | cut -d= -f2-)"
  for _ in $(seq 1 24); do
    codigo="$(curl -s -o /dev/null -w '%{http_code}' -H "X-Borde-Secreto: $secreto" \
      http://localhost:8080/health || true)"
    [[ "$codigo" == "200" ]] && return 0
    sleep 5
  done
  echo "  ultimo codigo de /health: ${codigo:-sin respuesta}"
  return 1
}

echo "  bajando imagenes :${COMMIT:0:12} y levantando..."
if levantar "$COMMIT" && sano; then
  echo "$COMMIT" > .imagen-tag
  docker compose ps --format '  {{.Service}}: {{.State}}'
  # Imagenes viejas sin usar: el disco de la EC2 es chico. Se dejan las de la
  # ultima semana para poder volver atras a mano.
  docker image prune -af --filter "until=168h" >/dev/null
  echo "  ✅ desplegado ${COMMIT:0:7} y /health responde 200"
  exit 0
fi

# 5. Rollback.
echo "::error::El despliegue de ${COMMIT:0:7} no quedo sano. Volviendo a ${COMMIT_ANTERIOR:0:7}."
docker compose logs --tail=30 "${SERVICIOS[@]}" || true
git checkout -q --detach "$COMMIT_ANTERIOR"
# "local" = lo anterior lo levanto desplegar.sh compilando en la maquina: esas
# imagenes no estan en GHCR, asi que se vuelve a compilar (lento, pero seguro).
if [[ "$TAG_ANTERIOR" == "local" ]]; then
  VOLVER=(docker compose up -d --build --remove-orphans)
else
  VOLVER=(docker compose up -d --no-build --remove-orphans)
fi
if IMAGEN_TAG="$TAG_ANTERIOR" "${VOLVER[@]}" && sano; then
  echo "::warning::Rollback hecho: sigue corriendo ${COMMIT_ANTERIOR:0:7}."
else
  echo "::error::El rollback tampoco quedo sano. Revisar a mano: docker compose logs"
fi
exit 1
