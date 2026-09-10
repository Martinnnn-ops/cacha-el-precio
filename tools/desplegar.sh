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
set -a; . "$RAIZ/infra.env"; . "$RAIZ/cognito.env"
# api-gateway.env es opcional aqui: solo hace falta para compilar el frontend,
# y --solo-backend no lo necesita. Si falta, el paso 4 avisa y para.
[ -f "$RAIZ/api-gateway.env" ] && . "$RAIZ/api-gateway.env"
# El secreto que Caddy exige en el 8080. Lo genera crear-api-gateway.sh.
[ -f "$RAIZ/borde.env" ] && . "$RAIZ/borde.env"
set +a
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
if [[ "$SOLO" != "--solo-front" && -z "${BORDE_SECRETO:-}" ]]; then
  rojo "  Falta borde.env, y sin el Caddy respondera 403 a todo el trafico del borde."
  rojo "  Corre antes: ./tools/crear-api-gateway.sh"
  exit 1
fi

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
BORDE_SECRETO=$BORDE_SECRETO
ENV
verde "  .env armado (apunta al pool $COGNITO_USER_POOL_ID)"

# -------------------------------------------------------------------------
# 2. Codigo y arranque
#    El swap no es un adorno: compilar las imagenes de .NET es el momento de
#    mas presion de memoria. Con 2 GB de swap termina aunque la RAM se llene.
# -------------------------------------------------------------------------
titulo "2. Desplegando en la maquina"

scp -i "$LLAVE" -o StrictHostKeyChecking=accept-new -q "$ENVTMP" "ec2-user@$INFRA_IP:/tmp/.env.nuevo" \
  && verde "  .env copiado" || { rojo "  no se pudo copiar el .env"; rm -f "$ENVTMP"; exit 1; }
rm -f "$ENVTMP"

$SSH "bash -s" <<REMOTO
set -e

# Sin swap, compilar las imagenes de .NET se queda sin memoria en una maquina
# chica. Desde el 10-09 la instancia es t3.small (2 GB) y en marcha no lo toca,
# pero compilar sigue siendo el momento de mas presion: el swap se queda como
# red de seguridad.
#
# Ojo con dos trampas, las dos encontradas probando esto de verdad:
#   1. el mkswap de AL2023 no acepta -q
#   2. swapon dura hasta el proximo reinicio. Sin la linea en /etc/fstab el
#      swap desaparece al reiniciar y NADIE se entera, porque el script ya
#      dijo "activado" en su momento. Pasa justo cuando cambias el tipo de
#      instancia, que obliga a apagar y encender.
if ! sudo swapon --show | grep -q .; then
  sudo dd if=/dev/zero of=/swapfile bs=1M count=2048 status=none
  sudo chmod 600 /swapfile
  sudo mkswap /swapfile >/dev/null
  sudo swapon /swapfile
  sudo swapon --show | grep -q . && echo "  swap de 2 GB activado" || { echo "  ERROR: el swap no se activo"; exit 1; }
else
  echo "  swap ya activo"
fi

# Que sobreviva al reinicio. Idempotente: solo la escribe si no esta.
if ! grep -q '^/swapfile' /etc/fstab; then
  echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab >/dev/null
  echo "  swap anotado en /etc/fstab (ahora si sobrevive al reinicio)"
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
# Se comprueba el 8080, NO el 80.
#
# El puerto 80 lo atiende Caddy, que solo conoce el nombre
# api.cacha-el-precio.com: a cualquier otra cosa le responde 308 redirigiendo a
# https. O sea que preguntarle por localhost SIEMPRE da 308, este el sistema
# sano o muerto, y este mismo script llego a gritar "no respondio" con los
# cinco contenedores arriba y el borde devolviendo 200.
#
# El 8080 es el bloque sin dominio del Caddyfile y tambien pasa por el gateway,
# asi que un 200 ahi dice lo que de verdad se quiere saber: que la peticion
# cruzo Caddy, cruzo el gateway y volvio.
OK=""
for i in $(seq 1 20); do
  # Con el encabezado: sin el, Caddy responde 403 y la comprobacion diria que el
  # sistema esta caido cuando en realidad esta bien cerrado.
  CODIGO="$($SSH "curl -s -o /dev/null -w '%{http_code}' -H 'X-Borde-Secreto: $BORDE_SECRETO' http://localhost:8080/health" 2>/dev/null)"
  [[ "$CODIGO" == "200" ]] && { OK="si"; break; }
  sleep 10
done
if [[ -n "$OK" ]]; then
  verde "  /health responde 200 dentro de la maquina (via Caddy y el gateway)"
else
  rojo  "  /health no respondio (ultimo codigo: ${CODIGO:-sin respuesta}). Mira los registros:"
  gris  "  ssh -i $LLAVE ec2-user@$INFRA_IP 'cd $DESTINO && docker compose logs --tail=40'"
fi
fi

# ---------------------------------------------------------------------------
# 3.4 Si la base quedo vacia y hay respaldo, restaurarlo
#
#    Al terminar una EC2 se va su volumen de Docker, asi que al remontar la base
#    nace VACIA. El sistema arranca perfecto, responde 200 y no da ningun error:
#    simplemente no hay datos, y es facil no darse cuenta hasta la demo.
#
#    Se restaura SOLO si la base esta en cero. Esa condicion es la que lo hace
#    seguro: si no hay ni una fila, no se puede pisar nada. Con datos dentro no
#    se toca nada y solo se informa — restaurar encima de datos buenos es
#    justo el accidente que este script existe para evitar.
#
#    SIN_RESTAURAR=1 lo salta.
# ---------------------------------------------------------------------------
if [[ "$SOLO" != "--solo-front" ]]; then
titulo "3.4 Datos"

PSQL_REMOTO="cd $DESTINO && set -a; source .env; set +a; docker compose exec -T postgres psql -U \$DB_USER -d \$DB_NAME"
FILAS="$($SSH "$PSQL_REMOTO -t -A -c 'select count(*) from scraper.products' 2>/dev/null" 2>/dev/null | tr -d '[:space:]')"

if [[ "$FILAS" =~ ^[0-9]+$ ]] && [[ "$FILAS" -gt 0 ]]; then
  verde "  la base trae $FILAS productos"
elif [[ -z "${INFRA_BUCKET_RESPALDOS:-}" ]]; then
  gris  "  la base esta vacia y no se donde buscar respaldos (falta INFRA_BUCKET_RESPALDOS)"
else
  ULTIMO="$(aws --region "$INFRA_REGION" s3 ls "s3://$INFRA_BUCKET_RESPALDOS/db/" 2>/dev/null | sort | tail -1 | awk '{print $4}')"
  if [[ -z "$ULTIMO" ]]; then
    gris "  la base esta vacia y no hay respaldos todavia"
    gris "  llenala con el scraper:"
    gris "    ssh -i $LLAVE ec2-user@$INFRA_IP 'curl -s -X POST \"http://127.0.0.1:8000/scrape/paris?limite=250\"'"
  elif [[ "${SIN_RESTAURAR:-}" == "1" ]]; then
    gris "  la base esta vacia; hay respaldo ($ULTIMO) pero SIN_RESTAURAR=1"
  else
    gris  "  la base esta vacia. Restaurando el ultimo respaldo: $ULTIMO"
    if aws --region "$INFRA_REGION" s3 cp "s3://$INFRA_BUCKET_RESPALDOS/db/$ULTIMO" - 2>/dev/null \
         | gunzip | $SSH "$PSQL_REMOTO -q -v ON_ERROR_STOP=1" >/dev/null 2>&1; then
      NUEVAS="$($SSH "$PSQL_REMOTO -t -A -c 'select count(*) from scraper.products' 2>/dev/null" 2>/dev/null | tr -d '[:space:]')"
      if [[ "$NUEVAS" =~ ^[0-9]+$ ]] && [[ "$NUEVAS" -gt 0 ]]; then
        verde "  restaurado: $NUEVAS productos, con su historial"
      else
        rojo "  el respaldo se aplico pero la base sigue vacia. Revisala a mano."
      fi
    else
      rojo "  no se pudo restaurar $ULTIMO. El sistema queda arriba pero SIN datos."
      gris "  a mano:"
      gris "    aws s3 cp s3://$INFRA_BUCKET_RESPALDOS/db/$ULTIMO - | gunzip | ssh -i $LLAVE ec2-user@$INFRA_IP '$PSQL_REMOTO'"
    fi
  fi
fi
fi

# ---------------------------------------------------------------------------
# 3.5 Respaldo automatico diario
#
#    POR QUE. El 10-09 se perdieron 221 productos con su historial al terminar
#    la instancia. crear-infra.sh --borrar ya respalda antes de destruir, pero
#    eso solo cubre el borrado DELIBERADO. No cubre que el laboratorio caduque,
#    que la maquina se caiga, ni que alguien la termine desde la consola.
#
#    Esta es la segunda capa: un volcado diario al bucket de respaldos, que es
#    privado y SOBREVIVE a la instancia. El catalogo lo repone el scraper; el
#    historial de precios no vuelve, y por eso se protege aparte.
#
#    Va como timer de systemd y no como cron porque systemd recupera la
#    ejecucion perdida (Persistent=true): si la maquina estuvo apagada a la
#    hora que tocaba, respalda al encender en vez de saltarse el turno.
#
#    POR HORA, no por dia, y la razon es el patron de uso real. El laboratorio
#    no esta encendido 24/7: se prende un rato y se apaga. Con un timer diario,
#    Persistent hace que dispare AL ARRANCAR —o sea que respalda el estado de la
#    sesion ANTERIOR— y despues no vuelve a correr en las horas que de verdad se
#    trabaja. Todo lo hecho en la sesion se perdia igual.
#
#    El volcado comprimido pesa ~100 KB. Hacerlo cada hora no cuesta nada y baja
#    la ventana de perdida de un dia entero a sesenta minutos.
# ---------------------------------------------------------------------------
if [[ "$SOLO" != "--solo-front" ]]; then
titulo "3.5 Respaldo automatico diario"

if [[ -z "${INFRA_BUCKET_RESPALDOS:-}" ]]; then
  gris "  no hay INFRA_BUCKET_RESPALDOS en infra.env; vuelve a correr crear-infra.sh"
else
  $SSH "sudo bash -s" <<REMOTO2 >/dev/null 2>&1
set -e
cat > /usr/local/bin/cep-respaldo <<'GUION'
#!/usr/bin/env bash
set -uo pipefail
cd /opt/cacha-el-precio || exit 1
./tools/respaldar.sh || exit 1
CARPETA="\$(ls -1dt /opt/cacha-el-precio/respaldos/*/ 2>/dev/null | head -1)"
[ -z "\$CARPETA" ] && exit 1
[ -s "\$CARPETA/database.sql" ] || exit 1
SELLO="\$(basename "\$CARPETA")"
gzip -c "\$CARPETA/database.sql" > "/tmp/\$SELLO.sql.gz"
aws s3 cp "/tmp/\$SELLO.sql.gz" "s3://$INFRA_BUCKET_RESPALDOS/db/\$SELLO.sql.gz" --region $INFRA_REGION
rm -f "/tmp/\$SELLO.sql.gz"
# El disco de la instancia es chico: solo se guardan 7 dias en local.
# La copia que importa ya esta en S3, que sobrevive a la maquina.
find /opt/cacha-el-precio/respaldos -maxdepth 1 -type d -mtime +7 -exec rm -rf {} + 2>/dev/null
GUION
chmod +x /usr/local/bin/cep-respaldo

cat > /etc/systemd/system/cep-respaldo.service <<'UNIDAD'
[Unit]
Description=Respaldo de la base de Cacha el Precio hacia S3
[Service]
Type=oneshot
ExecStart=/usr/local/bin/cep-respaldo
UNIDAD

cat > /etc/systemd/system/cep-respaldo.timer <<'TEMPO'
[Unit]
Description=Respaldo por hora de Cacha el Precio
[Timer]
OnCalendar=hourly
Persistent=true
[Install]
WantedBy=timers.target
TEMPO

systemctl daemon-reload
systemctl enable --now cep-respaldo.timer
REMOTO2

  if $SSH 'systemctl is-active cep-respaldo.timer' >/dev/null 2>&1; then
    PROX="$($SSH "systemctl list-timers cep-respaldo.timer --no-pager --no-legend | awk '{print \$1, \$2, \$3}'" 2>/dev/null)"
    verde "  timer activo — proximo respaldo: ${PROX:-cada hora}"
    gris  "  destino: s3://$INFRA_BUCKET_RESPALDOS/db/"
  else
    rojo  "  el timer no quedo activo. Comprueba con:"
    gris  "  ssh -i $LLAVE ec2-user@$INFRA_IP 'systemctl status cep-respaldo.timer'"
  fi
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

# A donde llama el frontend. Por defecto, al API GATEWAY, no a la EC2.
#
# Esto era https://api.cacha-el-precio.com, o sea la maquina directa, y ese era
# "el borde esta fuera del camino": el API Gateway existia, estaba bien armado y
# no lo cruzaba nadie. De el dependen tres indicadores del EP2 (validar el JWT
# en las rutas, enrutar hacia los servicios, y el CORS con origenes explicitos),
# y un borde que nadie atraviesa no demuestra ninguno de los tres.
#
# Se lee de api-gateway.env, que lo escribe crear-api-gateway.sh, asi que cada
# cuenta apunta sola a SU propio borde sin editar nada.
if [[ -n "${API_PUBLICA:-}" ]]; then
  API_PUB="$API_PUBLICA"
elif [[ -n "${API_GATEWAY_URL_PROD:-}" ]]; then
  API_PUB="$API_GATEWAY_URL_PROD"
else
  rojo "  No encuentro api-gateway.env. Corre antes ./tools/crear-api-gateway.sh"
  rojo "  (o pasa API_PUBLICA=... a mano si sabes lo que haces)"
  exit 1
fi
cat > .env.produccion.local <<FRONT
VITE_API_BASE_URL=$API_PUB
VITE_API_VERSION=1.0
VITE_COGNITO_CLIENT_ID=$VITE_COGNITO_CLIENT_ID
VITE_COGNITO_DOMAIN=$COGNITO_URL_LOGIN
FRONT
gris "  compilando contra $API_PUB y el pool de esta cuenta"

npm ci --silent 2>&1 | tail -2

# NO hay respaldo a "npm run build" a secas, y es a proposito.
#
# Antes esta linea era:  npm run build -- --mode produccion.local || npm run build
# con las dos salidas mandadas a /dev/null. Si la primera fallaba, la segunda
# compilaba contra frontend/.env.production, que todavia apunta al user pool de
# OTRA cuenta. Resultado: un sitio que se sube perfecto, no da ningun error, y
# manda a la gente a iniciar sesion en un Cognito que no es el nuestro. Un
# respaldo silencioso a la configuracion equivocada es peor que un fallo.
#
# Si falla, falla y se ve.
if ! npm run build -- --mode produccion.local > /tmp/build-front.log 2>&1; then
  rojo "  fallo la compilacion del frontend"
  gris "  ultimas lineas:"
  tail -15 /tmp/build-front.log
  exit 1
fi
verde "  compilado contra el pool $COGNITO_USER_POOL_ID"

# Cinturon: mirar DENTRO del bundle antes de subirlo.
#
# Se comprueba el client id y la URL de la API, no el user pool id: el pool id
# no viaja al navegador (el frontend usa el dominio del Hosted UI y el client
# id), asi que buscarlo daba una falsa alarma.
#
# Esto agarra el fallo que no se ve: un sitio que compila, sube y funciona a
# medias porque quedo apuntando al Cognito o a la API de otra cuenta. Sin esta
# comprobacion solo se descubre cuando alguien intenta entrar.
FALLO=""
grep -rqF "$VITE_COGNITO_CLIENT_ID" dist/assets/*.js 2>/dev/null \
  || FALLO="$FALLO\n  - no encuentro el client id $VITE_COGNITO_CLIENT_ID"
grep -rqF "$API_PUB" dist/assets/*.js 2>/dev/null \
  || FALLO="$FALLO\n  - no encuentro la API $API_PUB"

if [[ -n "$FALLO" ]]; then
  rojo  "  El bundle no lleva la configuracion de esta cuenta:"
  printf "%b\n" "$FALLO"
  rojo  "  No lo subo. Revisa frontend/.env.production, que apunta a otra cuenta."
  exit 1
fi
verde "  verificado dentro del bundle: client id y API de esta cuenta"

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
