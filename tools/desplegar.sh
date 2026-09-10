#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Despliega "Cacha el Precio" en la EC2 y sube el sitio al bucket.
#
# POR QUE ESTE SCRIPT EXISTE
#   crear-infra.sh deja la maquina lista, crear-cognito.sh la identidad y
#   crear-api-gateway.sh el borde. Faltaba el ultimo tramo: meter la aplicacion
#   dentro. Era el unico paso que quedaba "a mano" y por eso era el que se hacia
#   distinto cada vez.
#
#   Con esto, migrar de cuenta son cuatro comandos y un cambio de DNS.
#
# QUE HACE
#   1. Arma el .env del servidor desde cognito.env e infra.env
#   2. Lo copia a la maquina y clona (o actualiza) el repositorio
#   3. Levanta el compose y espera a que el backend responda
#   4. Compila el frontend con los datos de ESTA cuenta y lo sube al bucket
#
# ES IDEMPOTENTE: correrlo de nuevo actualiza el codigo y vuelve a levantar.
#
# Uso:
#   ./desplegar.sh                 # todo
#   ./desplegar.sh --solo-backend  # solo la EC2
#   ./desplegar.sh --solo-front    # solo el bucket
#
# Requiere: infra.env y cognito.env (los generan los otros scripts) y la llave
# privada de la maquina. LLAVE=~/labsuser.pem ./desplegar.sh
# ---------------------------------------------------------------------------
set -uo pipefail

RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
REPO="${REPO:-https://github.com/Martinnnn-ops/cacha-el-precio.git}"
RAMA="${RAMA:-development}"
DESTINO="/opt/cacha-el-precio"

verde()  { printf '\033[0;32m%s\033[0m\n' "$1"; }
rojo()   { printf '\033[0;31m%s\033[0m\n' "$1"; }
gris()   { printf '\033[0;90m%s\033[0m\n' "$1"; }
titulo() { printf '\n\033[1m== %s ==\033[0m\n' "$1"; }

# ---------------------------------------------------------------------------
# 0. Comprobaciones. Se hacen TODAS antes de tocar nada: es preferible fallar
#    aca que a medio desplegar.
# ---------------------------------------------------------------------------
titulo "0. Comprobaciones"

for f in infra.env cognito.env; do
  [[ -f "$RAIZ/$f" ]] || { rojo "  falta $f — corre primero crear-infra.sh y crear-cognito.sh"; exit 1; }
done
set -a; . "$RAIZ/infra.env"; . "$RAIZ/cognito.env"; set +a
verde "  infra.env y cognito.env leidos"

LLAVE="${LLAVE:-$HOME/labsuser.pem}"
[[ -f "$LLAVE" ]] || { rojo "  no encuentro la llave en $LLAVE"; \
                       gris  "  bajala del Learner Lab (AWS Details -> Download PEM) o pasa LLAVE=..."; exit 1; }
[[ "$(stat -c '%a' "$LLAVE")" =~ ^[46]00$ ]] || { rojo "  la llave debe ser 400: chmod 400 $LLAVE"; exit 1; }
verde "  llave: $LLAVE"

SSH="ssh -i $LLAVE -o StrictHostKeyChecking=accept-new -o ConnectTimeout=15 ec2-user@$INFRA_IP"
$SSH true 2>/dev/null || { rojo "  no se puede entrar a $INFRA_IP"; exit 1; }
verde "  conexion con $INFRA_IP"

SOLO="${1:-}"

# ---------------------------------------------------------------------------
# 1. El .env del servidor
#    Se ARMA, no se copia el local: el del computador apunta a otra cuenta y
#    arrastra variables de servicios que ya no existen. Se genera siempre desde
#    cognito.env para que el servidor no pueda quedar apuntando a un pool ajeno,
#    que es justo el fallo que tuvimos.
# ---------------------------------------------------------------------------
if [[ "$SOLO" != "--solo-front" ]]; then
titulo "1. Preparando el .env del servidor"

CLAVE_BD="$($SSH "grep -s '^DB_PASSWORD=' $DESTINO/.env | cut -d= -f2-" 2>/dev/null)"
if [[ -n "$CLAVE_BD" ]]; then
  gris "  conservando la contrasena de la base que ya estaba"
else
  CLAVE_BD="$(tr -dc 'A-Za-z0-9' </dev/urandom | head -c 32)"
  gris "  contrasena de la base generada"
fi

ENVTMP="$(mktemp)"
cat > "$ENVTMP" <<ENV
# Generado por tools/desplegar.sh el $(date -Is)
# Cuenta $INFRA_CUENTA. NO editar a mano: se regenera en cada despliegue.
COGNITO_ISSUER=$COGNITO_ISSUER
COGNITO_CLIENT_IDS_VALIDOS=$COGNITO_CLIENT_IDS_VALIDOS
COGNITO_SCRAPER_SCOPE=$COGNITO_SCRAPER_SCOPE
DB_USER=cachaelprecio
DB_NAME=cachaelprecio
DB_PASSWORD=$CLAVE_BD
CADDY_HTTP_PORT=80
CADDY_HTTPS_PORT=443
APP_ENV=production
AWS_REGION=$INFRA_REGION
ENV
verde "  .env armado (apunta al pool $COGNITO_USER_POOL_ID)"

# -------------------------------------------------------------------------
# 2. Codigo y arranque
#    El swap no es un adorno: una t3.micro tiene 1 GB y compilar las imagenes
#    de .NET se queda sin memoria a la mitad. Con 2 GB de swap termina.
# -------------------------------------------------------------------------
titulo "2. Desplegando en la maquina"

scp -i "$LLAVE" -o StrictHostKeyChecking=accept-new -q "$ENVTMP" "ec2-user@$INFRA_IP:/tmp/.env.nuevo" \
  && verde "  .env copiado" || { rojo "  no se pudo copiar el .env"; rm -f "$ENVTMP"; exit 1; }
rm -f "$ENVTMP"

$SSH "bash -s" <<REMOTO
set -e

# Sin swap, compilar las imagenes de .NET se queda sin memoria en una t3.micro
# (1 GB). Ojo: el mkswap de AL2023 no acepta -q.
if ! sudo swapon --show | grep -q .; then
  sudo dd if=/dev/zero of=/swapfile bs=1M count=2048 status=none
  sudo chmod 600 /swapfile
  sudo mkswap /swapfile >/dev/null
  sudo swapon /swapfile
  sudo swapon --show | grep -q . && echo "  swap de 2 GB activado" || { echo "  ERROR: el swap no se activo"; exit 1; }
else
  echo "  swap ya activo"
fi

# El plugin de compose nuevo exige buildx >= 0.17 y la AMI trae 0.12.1 en
# /usr/libexec, asi que no basta con comprobar que exista: hay que mirar la
# version. La nueva va a /usr/local/lib, que tiene precedencia.
BUILDX_VER="\$(docker buildx version 2>/dev/null | grep -oE 'v[0-9]+\.[0-9]+' | head -1 | cut -d. -f2)"
if [ -z "\$BUILDX_VER" ] || [ "\$BUILDX_VER" -lt 17 ]; then
  echo "  buildx viejo o ausente, instalando v0.17.1..."
  sudo mkdir -p /usr/local/lib/docker/cli-plugins
  sudo curl -fsSL https://github.com/docker/buildx/releases/download/v0.17.1/buildx-v0.17.1.linux-amd64 \
    -o /usr/local/lib/docker/cli-plugins/docker-buildx
  sudo chmod +x /usr/local/lib/docker/cli-plugins/docker-buildx
  echo "  buildx ahora: \$(docker buildx version | head -1)"
else
  echo "  buildx ya sirve: \$(docker buildx version | head -1)"
fi

if [ -d "$DESTINO/.git" ]; then
  cd "$DESTINO" && git fetch -q origin && git reset -q --hard "origin/$RAMA"
  echo "  repositorio actualizado a $RAMA"
else
  sudo rm -rf "$DESTINO" && sudo mkdir -p "$DESTINO" && sudo chown ec2-user:ec2-user "$DESTINO"
  git clone -q --branch "$RAMA" "$REPO" "$DESTINO"
  echo "  repositorio clonado ($RAMA)"
fi

mv /tmp/.env.nuevo "$DESTINO/.env"
cd "$DESTINO"
echo "  levantando el compose (la primera vez compila y tarda)..."
docker compose up -d --build 2>&1 | tail -8
docker compose ps --format '  {{.Service}}: {{.State}}'
REMOTO
fi

# ---------------------------------------------------------------------------
# 3. Comprobar que el backend responde
# ---------------------------------------------------------------------------
if [[ "$SOLO" != "--solo-front" ]]; then
titulo "3. Comprobando el backend"
OK=""
for i in $(seq 1 20); do
  CODIGO="$($SSH "curl -s -o /dev/null -w '%{http_code}' http://localhost/health" 2>/dev/null)"
  [[ "$CODIGO" == "200" ]] && { OK="si"; break; }
  sleep 10
done
if [[ -n "$OK" ]]; then
  verde "  /health responde 200 dentro de la maquina"
else
  rojo  "  /health no respondio. Mira los registros:"
  gris  "  ssh -i $LLAVE ec2-user@$INFRA_IP 'cd $DESTINO && docker compose logs --tail=40'"
fi
fi

# ---------------------------------------------------------------------------
# 4. El frontend
#    Se compila con los datos de ESTA cuenta. Es el paso que obliga a
#    recompilar al migrar: el client id y el dominio de Cognito quedan dentro
#    del bundle.
# ---------------------------------------------------------------------------
if [[ "$SOLO" != "--solo-backend" ]]; then
titulo "4. Compilando y subiendo el frontend"
cd "$RAIZ/frontend" || { rojo "  no encuentro frontend/"; exit 1; }

API_PUB="${API_PUBLICA:-https://api.cacha-el-precio.com}"
cat > .env.produccion.local <<FRONT
VITE_API_BASE_URL=$API_PUB
VITE_API_VERSION=1.0
VITE_COGNITO_CLIENT_ID=$VITE_COGNITO_CLIENT_ID
VITE_COGNITO_DOMAIN=$COGNITO_URL_LOGIN
FRONT
gris "  compilando contra $API_PUB y el pool de esta cuenta"

npm ci --silent 2>&1 | tail -2
if npm run build -- --mode produccion.local >/dev/null 2>&1 || npm run build >/dev/null 2>&1; then
  verde "  compilado"
else
  rojo "  fallo la compilacion del frontend"; exit 1
fi

aws --region "$INFRA_REGION" s3 sync dist/ "s3://$INFRA_BUCKET/" --delete --only-show-errors \
  && verde "  subido a s3://$INFRA_BUCKET/" || rojo "  fallo la subida"
fi

# ---------------------------------------------------------------------------
titulo "Listo"
echo "  Maquina    $INFRA_IP"
echo "  Bucket     $INFRA_BUCKET"
echo ""
gris "Lo unico que queda, y esta fuera de AWS:"
echo "  En Cloudflare: 'api' -> $INFRA_IP   y   'www' -> el bucket"
echo ""
gris "Mientras tanto, para comprobarlo sin DNS:"
echo "  ssh -i $LLAVE ec2-user@$INFRA_IP 'curl -s localhost/health'"
