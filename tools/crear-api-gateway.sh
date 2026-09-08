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
# A donde apunta el API Gateway. Hoy esa direccion la atiende Caddy, que le
# habla directo a product-service; cuando Caddy pase a apuntar al gateway (BFF),
# esta misma URL empieza a servir tambien /seguimiento y /api/*. Hasta entonces
# esas rutas existen aca pero responden 404 desde el backend.
BACKEND_URL="${BACKEND_URL:-https://api.cacha-el-precio.com}"
ORIGEN_WEB="${ORIGEN_WEB:-https://www.cacha-el-precio.com}"
ORIGEN_LOCAL="http://localhost:5173"

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
    "AllowOrigins=$ORIGEN_WEB,$ORIGEN_LOCAL,AllowMethods=GET,POST,PUT,DELETE,OPTIONS,AllowHeaders=Content-Type,Authorization,X-API-VERSION,MaxAge=86400,AllowCredentials=true" \
  >/dev/null && verde "  CORS: $ORIGEN_WEB y $ORIGEN_LOCAL (sin comodin)"

# --------------------------------------------------------------------------
# 2. El JWT Authorizer
#    Aca esta el punto del EP1: el token se rechaza EN EL BORDE, antes de que
#    la peticion llegue siquiera a la EC2. Un token vencido o falso no gasta
#    instancia.
#
#    Audience lleva los DOS client id nuestros. Cognito no manda `aud` en el
#    access_token —manda `client_id`— y el authorizer de API Gateway compara
#    contra los dos claims. Son dos clients: el del frontend y el de pruebas.
# --------------------------------------------------------------------------
titulo "2. JWT Authorizer"
AUTH_ID="$(aws_ apigatewayv2 get-authorizers --api-id "$API_ID" \
           --query "Items[?Name=='cognito'].AuthorizerId | [0]" --output text)"

if [[ "$AUTH_ID" != "None" && -n "$AUTH_ID" ]]; then
  verde "  ya existe: $AUTH_ID"
else
  AUTH_ID="$(aws_ apigatewayv2 create-authorizer \
    --api-id "$API_ID" \
    --name cognito \
    --authorizer-type JWT \
    --identity-source '$request.header.Authorization' \
    --jwt-configuration "Audience=$COGNITO_CLIENT_IDS_VALIDOS,Issuer=$COGNITO_ISSUER" \
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

integracion() {  # $1 = ruta del backend
  local destino="$BACKEND_URL$1"
  local existente
  existente="$(aws_ apigatewayv2 get-integrations --api-id "$API_ID" --max-results 200 \
    --query "Items[?IntegrationUri=='$destino'].IntegrationId | [0]" --output text)"
  if [[ "$existente" != "None" && -n "$existente" ]]; then
    echo "$existente"; return
  fi
  aws_ apigatewayv2 create-integration \
    --api-id "$API_ID" \
    --integration-type HTTP_PROXY \
    --integration-method ANY \
    --integration-uri "$destino" \
    --payload-format-version 1.0 \
    --query IntegrationId --output text
}

I_HEALTH="$(integracion /health)";                     gris "  /health          -> $I_HEALTH"
I_PRODUCTOS="$(integracion /productos)";               gris "  /productos       -> $I_PRODUCTOS"
I_PRODUCTO="$(integracion '/productos/{id}')";         gris "  /productos/{id}  -> $I_PRODUCTO"
I_VISITAS="$(integracion '/productos/{id}/visitas')";  gris "  .../visitas      -> $I_VISITAS"
I_CATALOGOS="$(integracion /catalogos)";               gris "  /catalogos       -> $I_CATALOGOS"
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
#    Las visitas quedan anonimas a proposito: el frontend cuenta vistas de gente
#    que no inicio sesion. Cerrarlas romperia el contador.

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
gris  "  curl -i $BASE/prod/productos                               # 401"
gris  "  curl -i -H \"Authorization: Bearer \$TOKEN\" \\"
gris  "          -H 'X-API-VERSION: 0.1.0' $BASE/prod/productos     # 200"
