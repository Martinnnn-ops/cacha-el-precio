#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Crea el API Gateway de "Cacha el Precio": el API Manager del sistema.
#
# POR QUE ESTE SCRIPT EXISTE
#   El enunciado del EP1 pide "uso del API Gateway" con esas palabras, y el
#   informe se califica por justificar la eleccion del API Manager. Hasta ahora
#   ese rol lo cumplia Caddy, que es un reverse proxy: hace TLS y enruta, pero
#   NO valida el token en el borde, no tiene stages y no tiene cuotas.
#
#   Y existe como script, y no como clicks en la consola, por la misma razon que
#   crear-cognito.sh: la cuenta es de AWS Academy y se puede agotar. Si nos dan
#   otra, esto se vuelve a levantar con un comando en vez de con una tarde de
#   memoria. Ver docs/MIGRACION.md.
#
# QUE CREA
#   · Una HTTP API con CORS de origenes explicitos (nunca "*")
#   · Un JWT Authorizer apuntando a nuestro user pool de Cognito
#   · Las rutas hacia el backend, cada una con su nivel de acceso
#   · Los stages `dev` y `prod`
#
# ES IDEMPOTENTE: si algo ya existe lo reutiliza. Se puede correr las veces que
# haga falta.
#
# Uso:
#   ./crear-api-gateway.sh              # crea lo que falte
#   ./crear-api-gateway.sh --borrar     # borra la API entera
#
# Variables opcionales:
#   BACKEND_URL   a donde apunta el gateway   (por defecto https://api.cacha-el-precio.com)
#   ORIGEN_WEB    origen permitido por CORS   (por defecto https://www.cacha-el-precio.com)
#   REGION        (por defecto la de cognito.env, o us-east-1)
# ---------------------------------------------------------------------------
set -uo pipefail

RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
COGNITO_ENV="$RAIZ/cognito.env"
SALIDA="$RAIZ/api-gateway.env"

NOMBRE_API="cacha-el-precio"
DOMINIO_API="api.cacha-el-precio.com"

# ---------------------------------------------------------------------------
# A donde apunta el API Gateway, y por que NO se puede escribir fijo.
#
# El registro DNS de api.cacha-el-precio.com admite UNA sola IP, y el dominio
# lo administra una sola persona. Quien no sea el dueno del DNS y deje esta
# variable en el dominio, arma un borde que llama a la maquina de OTRO: si esa
# maquina esta apagada, sus rutas dan 503 y parece un fallo propio.
#
# Por eso se decide sola, en este orden:
#   1. Si te la pasan a mano (BACKEND_URL=...), manda esa y no se discute.
#   2. Si el dominio resuelve a la IP de ESTA cuenta, se usa el dominio: hay
#      certificado de verdad y el salto va cifrado.
#   3. Si no, se usa http://<TU-IP>:8080, el bloque sin dominio del Caddyfile.
#      Tambien pasa por el gateway, o sea que el token se valida igual; lo
#      unico que cambia es que el salto interno va sin TLS. El cliente sigue
#      hablando HTTPS, porque el certificado lo pone el API Gateway.
#
# Asi el companero corre los tres scripts sin banderas y le funciona.
# ---------------------------------------------------------------------------
resolver_ip() {
  getent ahostsv4 "$1" 2>/dev/null | awk '{print $1; exit}' ||
  python3 -c "import socket,sys;print(socket.gethostbyname(sys.argv[1]))" "$1" 2>/dev/null
}

if [[ -n "${BACKEND_URL:-}" ]]; then
  ORIGEN_BACKEND="lo pasaste a mano"
else
  INFRA_IP=""
  [[ -f "$RAIZ/infra.env" ]] && INFRA_IP="$(grep -E '^INFRA_IP=' "$RAIZ/infra.env" | cut -d= -f2)"

  if [[ -z "$INFRA_IP" ]]; then
    BACKEND_URL="https://$DOMINIO_API"
    ORIGEN_BACKEND="no hay infra.env todavia; asumo el dominio"
  elif [[ "$(resolver_ip "$DOMINIO_API")" == "$INFRA_IP" ]]; then
    BACKEND_URL="https://$DOMINIO_API"
    ORIGEN_BACKEND="el DNS apunta a tu maquina"
  else
    BACKEND_URL="http://$INFRA_IP:8080"
    ORIGEN_BACKEND="el DNS apunta a otra maquina; voy directo a la tuya"
  fi
fi

ORIGEN_WEB="${ORIGEN_WEB:-https://www.cacha-el-precio.com}"
ORIGEN_LOCAL="http://localhost:5173"

# Tercer origen: el sitio servido directo desde el bucket de S3.
#
# Para que sirve. Quien no tenga el dominio no puede ver el sitio por
# www.cacha-el-precio.com, asi que su unica forma de enseñarselo a alguien es la
# URL de sitio estatico del bucket. Si ese origen no esta declarado aqui, la
# pagina carga pero el navegador BLOQUEA todas las llamadas a la API por CORS,
# y se ve una web vacia sin ningun error claro.
#
# El nombre sale del bucket, que a su vez sale del numero de cuenta, asi que en
# cada cuenta apunta sola al suyo sin escribir nada.
#
# OJO: es http://, sin cifrar. Sirve para MIRAR el catalogo; el inicio de sesion
# no funciona ahi porque Cognito solo admite retornos https (salvo localhost).
ORIGEN_S3=""
if [[ -f "$RAIZ/infra.env" ]]; then
  BUCKET_WEB="$(grep -E '^INFRA_BUCKET=' "$RAIZ/infra.env" | cut -d= -f2)"
  REGION_WEB="$(grep -E '^INFRA_REGION=' "$RAIZ/infra.env" | cut -d= -f2)"
  [[ -n "$BUCKET_WEB" ]] && ORIGEN_S3="http://$BUCKET_WEB.s3-website-${REGION_WEB:-us-east-1}.amazonaws.com"
fi

ORIGENES="$ORIGEN_WEB,$ORIGEN_LOCAL"
[[ -n "$ORIGEN_S3" ]] && ORIGENES="$ORIGENES,$ORIGEN_S3"

verde()  { printf '\033[0;32m%s\033[0m\n' "$1"; }
rojo()   { printf '\033[0;31m%s\033[0m\n' "$1"; }
gris()   { printf '\033[0;90m%s\033[0m\n' "$1"; }
titulo() { printf '\n\033[1m== %s ==\033[0m\n' "$1"; }

# --------------------------------------------------------------------------
# 0. Comprobaciones previas
# --------------------------------------------------------------------------
titulo "0. Comprobaciones"

if [[ ! -f "$COGNITO_ENV" ]]; then
  rojo "No existe cognito.env. Corre primero ./tools/crear-cognito.sh"
  exit 1
fi

# shellcheck disable=SC1090
set -a; source "$COGNITO_ENV"; set +a

REGION="${REGION:-${COGNITO_REGION:-us-east-1}}"
aws_() { aws --region "$REGION" "$@"; }

if ! CUENTA="$(aws_ sts get-caller-identity --query Account --output text 2>/dev/null)"; then
  rojo "Las credenciales no sirven o vencieron."
  rojo "Learner Lab -> Start Lab -> AWS Details -> AWS CLI: Show,"
  rojo "y pega ese bloque en ~/.aws/credentials reemplazando [default]."
  exit 1
fi
verde "  cuenta $CUENTA, region $REGION"

for v in COGNITO_ISSUER COGNITO_CLIENT_IDS_VALIDOS; do
  if [[ -z "${!v:-}" ]]; then rojo "Falta $v en cognito.env"; exit 1; fi
done
verde "  emisor  $COGNITO_ISSUER"
gris  "  backend $BACKEND_URL"
gris  "          ($ORIGEN_BACKEND)"

# --------------------------------------------------------------------------
# Modo borrar
# --------------------------------------------------------------------------
buscar_api() {
  aws_ apigatewayv2 get-apis --max-results 200 \
    --query "Items[?Name=='$NOMBRE_API'].ApiId | [0]" --output text 2>/dev/null
}

if [[ "${1:-}" == "--borrar" ]]; then
  titulo "Borrando"
  ID="$(buscar_api)"
  if [[ "$ID" == "None" || -z "$ID" ]]; then gris "  no habia nada que borrar"; exit 0; fi
  aws_ apigatewayv2 delete-api --api-id "$ID" && verde "  API $ID borrada"
  rm -f "$SALIDA"
  exit 0
fi

# --------------------------------------------------------------------------
# 1. La HTTP API
#    Se elige HTTP API y no REST API a proposito: cuesta ~1/3, trae JWT
#    Authorizer nativo (la REST API necesita un Lambda authorizer o Cognito
#    user pools con mas plomeria) y para lo que hacemos no necesitamos nada de
#    lo que la REST API agrega.
#
#    CORS con origenes EXPLICITOS. Es un indicador de la rubrica del EP2 y
#    ademas es lo correcto: con "*" cualquier sitio puede llamar a la API desde
#    el navegador de un usuario que ya inicio sesion.
# --------------------------------------------------------------------------
titulo "1. HTTP API"
API_ID="$(buscar_api)"

if [[ "$API_ID" != "None" && -n "$API_ID" ]]; then
  verde "  ya existe: $API_ID"
else
  API_ID="$(aws_ apigatewayv2 create-api \
    --name "$NOMBRE_API" \
    --protocol-type HTTP \
    --description "API Manager de Cacha el Precio. Creada por tools/crear-api-gateway.sh" \
    --query ApiId --output text)" || { rojo "  no se pudo crear la API"; exit 1; }
  verde "  creada: $API_ID"
fi

aws_ apigatewayv2 update-api --api-id "$API_ID" \
  --cors-configuration \
    "AllowOrigins=$ORIGENES,AllowMethods=GET,POST,PUT,DELETE,OPTIONS,AllowHeaders=Content-Type,Authorization,Version,MaxAge=86400,AllowCredentials=false" \
  >/dev/null && verde "  CORS (sin comodin): $ORIGENES"

# --------------------------------------------------------------------------
# 2. El JWT Authorizer
#    Aca esta el punto del EP1: el token se rechaza EN EL BORDE, antes de que
#    la peticion llegue siquiera a la EC2. Un token vencido o falso no gasta
#    instancia.
#
#    Audience lleva TODOS los client id nuestros. Cognito no manda `aud` en el
#    access_token —manda `client_id`— y el authorizer de API Gateway compara
#    contra los dos claims.
#
#    Son TRES, y el tercero se habia quedado fuera:
#      · frontend  · pruebas  · SCRAPER (el de maquina, client_credentials)
#
#    Sin el scraper en la lista, su token se rechaza con 401 en el borde ANTES
#    de que nadie mire el scope, aunque el token traiga 'ingesta' perfecto.
#    Hoy no se nota porque el scraper escribe directo a la EC2, saltandose el
#    borde; el dia que se cierre esa puerta —que es justo lo que esta pendiente—
#    el scraper dejaria de poder escribir, y el sintoma (401 con un token
#    valido) no apunta para nada a la causa.
#
#    Y la lista se RECONCILIA en cada corrida, no solo al crear. Es el mismo
#    problema que ya mordio con el app client de Cognito y con los puertos del
#    security group: un authorizer creado antes de este arreglo se quedaba con
#    la lista vieja para siempre mientras el script decia "ya existe".
# --------------------------------------------------------------------------
titulo "2. JWT Authorizer"

AUDIENCIA="$COGNITO_CLIENT_IDS_VALIDOS"
if [[ -n "${COGNITO_SCRAPER_CLIENT_ID:-}" ]] && [[ ",$AUDIENCIA," != *",$COGNITO_SCRAPER_CLIENT_ID,"* ]]; then
  AUDIENCIA="$AUDIENCIA,$COGNITO_SCRAPER_CLIENT_ID"
fi

AUTH_ID="$(aws_ apigatewayv2 get-authorizers --api-id "$API_ID" \
           --query "Items[?Name=='cognito'].AuthorizerId | [0]" --output text)"

if [[ "$AUTH_ID" != "None" && -n "$AUTH_ID" ]]; then
  ACTUAL="$(aws_ apigatewayv2 get-authorizer --api-id "$API_ID" --authorizer-id "$AUTH_ID" \
            --query 'JwtConfiguration.Audience' --output text 2>/dev/null | tr '\t' ',')"
  ESPERADO="$(tr ',' '\n' <<<"$AUDIENCIA" | sort | paste -sd,)"
  ENCONTRADO="$(tr ',' '\n' <<<"$ACTUAL" | sort | paste -sd,)"
  if [[ "$ESPERADO" == "$ENCONTRADO" ]]; then
    verde "  ya existe: $AUTH_ID (audiencia al dia)"
  else
    aws_ apigatewayv2 update-authorizer --api-id "$API_ID" --authorizer-id "$AUTH_ID" \
      --jwt-configuration "Audience=$AUDIENCIA,Issuer=$COGNITO_ISSUER" >/dev/null \
      && verde "  $AUTH_ID: audiencia corregida" \
      || { rojo "  no se pudo actualizar el authorizer"; exit 1; }
    gris  "    antes: $ENCONTRADO"
    gris  "    ahora: $ESPERADO"
  fi
else
  AUTH_ID="$(aws_ apigatewayv2 create-authorizer \
    --api-id "$API_ID" \
    --name cognito \
    --authorizer-type JWT \
    --identity-source '$request.header.Authorization' \
    --jwt-configuration "Audience=$AUDIENCIA,Issuer=$COGNITO_ISSUER" \
    --query AuthorizerId --output text)" || { rojo "  no se pudo crear el authorizer"; exit 1; }
  verde "  creado: $AUTH_ID"
fi

# --------------------------------------------------------------------------
# 3. Las integraciones
#    HTTP_PROXY contra la URL publica del backend. No hace falta VPC Link
#    porque la EC2 tiene IP publica; el dia que el backend pase a una subred
#    privada esto cambia a VPC Link (ADR-015) y es lo unico que hay que tocar.
# --------------------------------------------------------------------------
titulo "3. Integraciones"

# ---------------------------------------------------------------------------
# El secreto del borde.
#
# El puerto 8080 de la EC2 esta abierto a internet, porque el security group no
# puede limitarse a las direcciones del API Gateway (AWS no publica un rango
# fijo). Sin esto, cualquiera podria llamar a la maquina directamente y saltarse
# el borde entero.
#
# El API Gateway inyecta este encabezado en CADA integracion y el Caddyfile
# rechaza con 403 lo que no lo traiga. Asi el borde deja de ser una costumbre
# del frontend y pasa a ser el unico camino posible.
#
# Se guarda en borde.env y se REUTILIZA: si se regenerara en cada corrida, el
# valor dejaria de coincidir con el que tiene la maquina y todo daria 403 hasta
# volver a desplegar.
# ---------------------------------------------------------------------------
BORDE_ENV="$RAIZ/borde.env"
if [[ -f "$BORDE_ENV" ]]; then
  # shellcheck disable=SC1090
  set -a; source "$BORDE_ENV"; set +a
fi
if [[ -z "${BORDE_SECRETO:-}" ]]; then
  BORDE_SECRETO="$(head -c 32 /dev/urandom | base64 | tr -d '/+=' | head -c 40)"
  cat > "$BORDE_ENV" <<FIN
# Secreto que el API Gateway inyecta en cada peticion hacia la EC2, y que Caddy
# exige en el puerto 8080. Generado por tools/crear-api-gateway.sh.
#
# NO se sube al repositorio. Si se pierde, se borra este archivo, se vuelve a
# correr este script y despues tools/desplegar.sh: se genera otro y se sincroniza.
BORDE_SECRETO=$BORDE_SECRETO
FIN
  chmod 600 "$BORDE_ENV"
  verde "  secreto del borde generado -> borde.env"
else
  gris  "  secreto del borde: reutilizando el de borde.env"
fi

# El encabezado va como "parameter mapping" de la integracion: lo pone el API
# Gateway, no el cliente, asi que el navegador nunca lo ve ni lo puede falsear.
PARAM_BORDE="overwrite:header.X-Borde-Secreto=$BORDE_SECRETO"

integracion() {  # $1 = ruta del backend
  local destino="$BACKEND_URL$1"
  local existente
  existente="$(aws_ apigatewayv2 get-integrations --api-id "$API_ID" --max-results 200 \
    --query "Items[?IntegrationUri=='$destino'].IntegrationId | [0]" --output text)"
  if [[ "$existente" != "None" && -n "$existente" ]]; then
    # Reconciliar, no solo crear. Una integracion hecha antes de que existiera el
    # encabezado se quedaria sin el para siempre, y el script diria que todo bien
    # mientras la EC2 le responde 403 a su propio borde.
    aws_ apigatewayv2 update-integration --api-id "$API_ID" --integration-id "$existente" \
      --request-parameters "$PARAM_BORDE" >/dev/null 2>&1
    echo "$existente"; return
  fi
  aws_ apigatewayv2 create-integration \
    --api-id "$API_ID" \
    --integration-type HTTP_PROXY \
    --integration-method ANY \
    --integration-uri "$destino" \
    --payload-format-version 1.0 \
    --request-parameters "$PARAM_BORDE" \
    --query IntegrationId --output text
}

I_HEALTH="$(integracion /health)";                     gris "  /health          -> $I_HEALTH"
I_PRODUCTOS="$(integracion /productos)";               gris "  /productos       -> $I_PRODUCTOS"
I_PRODUCTO="$(integracion '/productos/{id}')";         gris "  /productos/{id}  -> $I_PRODUCTO"
I_CATALOGOS="$(integracion /catalogos)";               gris "  /catalogos       -> $I_CATALOGOS"
I_VISITAS="$(integracion '/productos/{id}/visitas')";  gris "  /productos/{id}/visitas -> $I_VISITAS"
I_SEGUIMIENTO="$(integracion /seguimiento)";           gris "  /seguimiento     -> $I_SEGUIMIENTO"
I_SEGUIR="$(integracion '/seguimiento/{id}')";         gris "  /seguimiento/{id}-> $I_SEGUIR"
I_YO="$(integracion /api/yo)";                         gris "  /api/yo          -> $I_YO"
I_ADMIN="$(integracion /api/admin/diagnostico)";       gris "  /api/admin/...   -> $I_ADMIN"

# --------------------------------------------------------------------------
# 4. Las rutas, cada una con su nivel de acceso
#
#    Esta tabla ES la respuesta a "200 / 401 / 403" de la demo:
#
#    | ruta                   | sin token | con token | con token sin scope |
#    |------------------------|-----------|-----------|---------------------|
#    | GET /productos         | 200       | 200       | 200                 |
#    | GET /seguimiento       | 401       | 200       | 200                 |
#    | POST /productos        | 401       | 403       | 403                 |
#
#    ⚠️ EL CATALOGO ES PUBLICO, Y ES UNA DECISION.
#    ARQUITECTURA.md §8 define `precios:leer` como publico sin token: esto es un
#    comparador de precios y obligar a iniciar sesion para ver un precio seria
#    romper el producto. La primera version de este script puso GET /productos
#    detras del JWT para poder demostrar el 401, y eso contradecia esa decision:
#    con el trafico pasando por aca, el sitio habria dejado de mostrar productos
#    a cualquiera que llegue de Google.
#
#    El 401 sale de /seguimiento, que es privado por su naturaleza —son los
#    productos que una persona eligio seguir— y el 403 de las escrituras, que
#    exigen el scope `ingesta` que solo tiene el client del scraper.
#
titulo "4. Rutas"

# ⚠️ Las rutas se crean con --cli-input-json y no con banderas sueltas, y hay
#    un motivo concreto: la aws-cli v1 (la que trae AWS Academy) interpreta
#    cualquier parametro que empiece con "http://" o "https://" como una URL de
#    la que hay que DESCARGAR el valor, y revienta con "Could not connect to the
#    endpoint URL". Nuestro scope es `https://api.cachaelprecio.cl/ingesta`, asi
#    que muerde justo aca. Pasarlo dentro de un JSON lo evita, y funciona igual
#    en la v1 y en la v2. Es la misma trampa que anoto DESPLIEGUE.md para el
#    resource server de Cognito.
ruta() {  # $1 = "METODO /camino"  $2 = integracion  $3 = "publica" | "jwt" | "scope"
  local clave="$1" destino="$2" acceso="$3" existente cuerpo
  existente="$(aws_ apigatewayv2 get-routes --api-id "$API_ID" --max-results 200 \
    --query "Items[?RouteKey=='$clave'].RouteId | [0]" --output text)"

  cuerpo="$(API_ID="$API_ID" CLAVE="$clave" DESTINO="$destino" ACCESO="$acceso" \
            AUTH_ID="$AUTH_ID" SCOPE="${COGNITO_SCRAPER_SCOPE:-}" RUTA_ID="$existente" \
            python3 - <<'PYJSON'
import json, os

acceso = os.environ["ACCESO"]
d = {"ApiId": os.environ["API_ID"], "Target": "integrations/" + os.environ["DESTINO"]}

ruta_id = os.environ.get("RUTA_ID", "")
if ruta_id and ruta_id != "None":
    d["RouteId"] = ruta_id
else:
    d["RouteKey"] = os.environ["CLAVE"]

if acceso in ("jwt", "scope"):
    d["AuthorizationType"] = "JWT"
    d["AuthorizerId"] = os.environ["AUTH_ID"]
    if acceso == "scope":
        alcance = os.environ.get("SCOPE", "")
        if not alcance:
            raise SystemExit("falta COGNITO_SCRAPER_SCOPE en cognito.env")
        d["AuthorizationScopes"] = [alcance]
else:
    d["AuthorizationType"] = "NONE"

print(json.dumps(d))
PYJSON
  )" || { rojo "  no se pudo armar la ruta $clave"; return 1; }

  local archivo; archivo="$(mktemp)"
  printf '%s' "$cuerpo" > "$archivo"

  if [[ "$existente" != "None" && -n "$existente" ]]; then
    aws_ apigatewayv2 update-route --cli-input-json "file://$archivo" >/dev/null \
      && gris "  ~ $clave ($acceso)" || rojo "  ! $clave ($acceso)"
  else
    aws_ apigatewayv2 create-route --cli-input-json "file://$archivo" >/dev/null \
      && verde "  + $clave ($acceso)" || rojo "  ! $clave ($acceso)"
  fi
  rm -f "$archivo"
}

# Publicas: es lo que se ve al entrar al sitio (ADR-007)
ruta "GET /health"                     "$I_HEALTH"      publica
ruta "GET /productos"                  "$I_PRODUCTOS"   publica
ruta "GET /productos/{id}"             "$I_PRODUCTO"    publica
ruta "GET /catalogos"                  "$I_CATALOGOS"   publica
# Anonima a proposito: pedir sesion para contar una vista dejaria
# «Lo mas visto» midiendo solo a quien inicia sesion (gateway/README.md).
ruta "POST /productos/{id}/visitas"    "$I_VISITAS"     publica

# Requieren sesion: son de cada persona. De aca sale el 401 de la demo.
ruta "GET /seguimiento"                "$I_SEGUIMIENTO" jwt
ruta "POST /seguimiento/{id}"          "$I_SEGUIR"      jwt
ruta "DELETE /seguimiento/{id}"        "$I_SEGUIR"      jwt
ruta "GET /api/yo"                     "$I_YO"          jwt

# El 403 por rol lo resuelve el BFF leyendo cognito:groups; el API Gateway solo
# comprueba que haya un token valido. Son dos capas distintas a proposito.
ruta "GET /api/admin/diagnostico"      "$I_ADMIN"       jwt

# Escrituras: exigen el scope `ingesta`, que solo tiene el client del scraper.
# De aca sale el 403 de la demo, con un token perfectamente valido.
ruta "POST /productos"                 "$I_PRODUCTOS"   scope
ruta "PUT /productos/{id}"             "$I_PRODUCTO"    scope
ruta "DELETE /productos/{id}"          "$I_PRODUCTO"    scope

# --------------------------------------------------------------------------
# 5. Los stages
#    Dos, y con eso se cumple el indicador de la rubrica. `prod` va con
#    auto-deploy para que un cambio de ruta se publique solo.
# --------------------------------------------------------------------------
titulo "5. Stages"
for etapa in dev prod; do
  if aws_ apigatewayv2 get-stage --api-id "$API_ID" --stage-name "$etapa" >/dev/null 2>&1; then
    verde "  $etapa ya existe"
  else
    aws_ apigatewayv2 create-stage --api-id "$API_ID" --stage-name "$etapa" \
      --auto-deploy --description "Stage $etapa" >/dev/null && verde "  $etapa creado"
  fi
done


# Limite de peticiones, en los dos stages.
#
# Sin esto el unico tope es el de la cuenta (10.000 por segundo), que para este
# proyecto no es un tope: es una via para que alguien agote el credito del
# laboratorio a base de peticiones y tumbe la demo el dia de la entrega. No
# protege datos —esos ya los cuida el JWT— protege la cuenta.
#
# 50 por segundo con rafagas de 100 es holgado para un catalogo que sirve una
# sola pagina, y corta en seco cualquier martilleo.
for STAGE in dev prod; do
  aws_ apigatewayv2 update-stage --api-id "$API_ID" --stage-name "$STAGE" \
    --default-route-settings "ThrottlingRateLimit=50,ThrottlingBurstLimit=100" >/dev/null 2>&1 \
    && gris "  $STAGE: limite 50/s, rafaga 100"
done
# --------------------------------------------------------------------------
# 6. Salida
# --------------------------------------------------------------------------
BASE="https://$API_ID.execute-api.$REGION.amazonaws.com"

cat > "$SALIDA" <<EOF
# Generado por tools/crear-api-gateway.sh el $(date '+%d-%m-%Y %H:%M')
# NO se versiona: cambia con cada cuenta de AWS.
API_GATEWAY_ID=$API_ID
API_GATEWAY_URL_DEV=$BASE/dev
API_GATEWAY_URL_PROD=$BASE/prod
API_GATEWAY_BACKEND=$BACKEND_URL
EOF
chmod 600 "$SALIDA"

titulo "Listo"
verde "  dev   $BASE/dev"
verde "  prod  $BASE/prod"
gris  "  guardado en api-gateway.env"
echo
gris  "Probar (necesita un token; ver docs/IDENTIDAD.md):"
gris  "  curl -i $BASE/prod/health                                  # 200"
gris  "  curl -i $BASE/prod/productos                               # 200 publico"
gris  "  curl -i -H \"Authorization: Bearer \$TOKEN\" \\"
gris  "          -H 'Version: 1.0' $BASE/prod/productos             # 200"
