#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Crea (o vuelve a crear) toda la configuracion de Cognito de "Cacha el Precio".
#
# POR QUE ESTE SCRIPT EXISTE
#   El User Pool vive en una cuenta de AWS Academy. Si el laboratorio se
#   resetea, o nos dan otra cuenta, se pierde entero. Hecho a mano en la
#   consola tambien se pierde la CONFIGURACION, y hay que reconstruirla de
#   memoria. Con este archivo, recrear todo es un comando.
#
# ES IDEMPOTENTE: si algo ya existe, lo reutiliza en vez de duplicarlo.
# Se puede correr las veces que haga falta.
#
# Uso:
#   ./crear-cognito.sh                 # crea lo que falte
#   ./crear-cognito.sh --borrar        # borra el pool entero (para empezar de cero)
#
# Variables opcionales:
#   REGION        (por defecto us-east-1, que es la que permite el Learner Lab)
#   CLAVE_PRUEBA  clave de los usuarios de prueba; si no se pasa, se genera una
# ---------------------------------------------------------------------------
set -uo pipefail

REGION="${REGION:-us-east-1}"
NOMBRE_POOL="cacha-el-precio"
IDENTIFICADOR_API="https://api.cachaelprecio.cl"   # identificador logico, no tiene que existir
SALIDA="$(cd "$(dirname "$0")/.." && pwd)/cognito.env"

verde()  { printf '\033[0;32m%s\033[0m\n' "$1"; }
rojo()   { printf '\033[0;31m%s\033[0m\n' "$1"; }
gris()   { printf '\033[0;90m%s\033[0m\n' "$1"; }
titulo() { printf '\n\033[1m== %s ==\033[0m\n' "$1"; }

aws_() { aws --region "$REGION" "$@"; }

# --------------------------------------------------------------------------
# 0. Comprobaciones previas
# --------------------------------------------------------------------------
titulo "0. Comprobaciones"
if ! CUENTA="$(aws_ sts get-caller-identity --query Account --output text 2>/dev/null)"; then
  rojo "Las credenciales no sirven o vencieron."
  rojo "Learner Lab -> Start Lab -> AWS Details -> AWS CLI: Show,"
  rojo "y pega ese bloque en ~/.aws/credentials reemplazando [default]."
  exit 1
fi
verde "  cuenta $CUENTA, region $REGION"

# El dominio del Hosted UI tiene que ser unico en TODA la region de AWS, no solo
# en nuestra cuenta. Pegarle el numero de cuenta lo hace unico y, a la vez,
# reproducible: en otra cuenta da otro nombre sin que haya que editar el script.
DOMINIO="cacha-el-precio-$CUENTA"

# --------------------------------------------------------------------------
# Modo borrar: deja la cuenta limpia para volver a empezar
# --------------------------------------------------------------------------
if [[ "${1:-}" == "--borrar" ]]; then
  titulo "Borrando"
  ID="$(aws_ cognito-idp list-user-pools --max-results 60 \
        --query "UserPools[?Name=='$NOMBRE_POOL'].Id | [0]" --output text)"
  if [[ "$ID" == "None" || -z "$ID" ]]; then
    gris "  no habia nada que borrar"; exit 0
  fi
  aws_ cognito-idp delete-user-pool-domain --domain "$DOMINIO" --user-pool-id "$ID" >/dev/null 2>&1 \
    && verde "  dominio borrado"
  aws_ cognito-idp delete-user-pool --user-pool-id "$ID" >/dev/null 2>&1 \
    && verde "  user pool $ID borrado"
  rm -f "$SALIDA"
  exit 0
fi

# --------------------------------------------------------------------------
# 1. El User Pool
#    Es el directorio de usuarios. Todo lo demas cuelga de aca.
# --------------------------------------------------------------------------
titulo "1. User Pool"
POOL_ID="$(aws_ cognito-idp list-user-pools --max-results 60 \
           --query "UserPools[?Name=='$NOMBRE_POOL'].Id | [0]" --output text)"

if [[ "$POOL_ID" != "None" && -n "$POOL_ID" ]]; then
  verde "  ya existe: $POOL_ID"
else
  TMP="$(mktemp -d)"
  # UsernameAttributes=email: la gente entra con su correo, no con un usuario
  # aparte. OJO: esto NO se puede cambiar despues de creado el pool.
  cat > "$TMP/pool.json" <<JSON
{
  "PoolName": "$NOMBRE_POOL",
  "UsernameAttributes": ["email"],
  "AutoVerifiedAttributes": ["email"],
  "MfaConfiguration": "OFF",
  "Policies": {
    "PasswordPolicy": {
      "MinimumLength": 8,
      "RequireUppercase": true,
      "RequireLowercase": true,
      "RequireNumbers": true,
      "RequireSymbols": false
    }
  },
  "AccountRecoverySetting": {
    "RecoveryMechanisms": [ { "Priority": 1, "Name": "verified_email" } ]
  },
  "Schema": [
    { "Name": "email", "Required": true, "Mutable": true }
  ]
}
JSON
  POOL_ID="$(aws_ cognito-idp create-user-pool --cli-input-json "file://$TMP/pool.json" \
             --query 'UserPool.Id' --output text)" || { rojo "  fallo al crear el pool"; exit 1; }
  rm -rf "$TMP"
  verde "  creado: $POOL_ID"
fi

# --------------------------------------------------------------------------
# 2. El dominio del Hosted UI
#    Es la pantalla de login que hospeda AWS. Sin dominio no hay pantalla,
#    y sin pantalla no hay flujo Authorization Code.
# --------------------------------------------------------------------------
titulo "2. Dominio del Hosted UI"
EXISTE_DOM="$(aws_ cognito-idp describe-user-pool --user-pool-id "$POOL_ID" \
              --query 'UserPool.Domain' --output text 2>/dev/null)"
if [[ "$EXISTE_DOM" != "None" && -n "$EXISTE_DOM" ]]; then
  DOMINIO="$EXISTE_DOM"; verde "  ya existe: $DOMINIO"
else
  if aws_ cognito-idp create-user-pool-domain --domain "$DOMINIO" \
       --user-pool-id "$POOL_ID" >/dev/null 2>&1; then
    verde "  creado: $DOMINIO"
  else
    rojo "  no se pudo crear el dominio $DOMINIO (quiza el nombre ya lo tomo otra cuenta)"
  fi
fi
URL_LOGIN="https://$DOMINIO.auth.$REGION.amazoncognito.com"

# --------------------------------------------------------------------------
# 3. Resource server: los permisos propios de NUESTRA API
#    Los scopes de OIDC (openid, email, profile) hablan del usuario.
#    Este define un permiso que habla de lo que la aplicacion puede HACER:
#    "ingesta" es el que usa el scraper para publicar ofertas.
# --------------------------------------------------------------------------
titulo "3. Resource server y scope propio"
# Se consulta con list en vez de describe: describe recibe el identificador por
# --identifier, y aws-cli v1 intentaria descargar esa URL en vez de usarla como texto.
YA_ESTA="$(aws_ cognito-idp list-resource-servers --user-pool-id "$POOL_ID" --max-results 50 \
           --query "ResourceServers[?Identifier=='$IDENTIFICADOR_API'].Identifier | [0]" --output text)"
if [[ "$YA_ESTA" != "None" && -n "$YA_ESTA" ]]; then
  verde "  ya existe: $IDENTIFICADOR_API"
else
  TMP="$(mktemp -d)"
  # Va por JSON porque aws-cli v1 intenta DESCARGAR cualquier parametro que
  # empiece con http:// o https://. No es una restriccion de AWS Academy.
  cat > "$TMP/rs.json" <<JSON
{ "UserPoolId": "$POOL_ID", "Identifier": "$IDENTIFICADOR_API", "Name": "api",
  "Scopes": [ {"ScopeName":"ingesta","ScopeDescription":"publicar ofertas capturadas"} ] }
JSON
  aws_ cognito-idp create-resource-server --cli-input-json "file://$TMP/rs.json" >/dev/null \
    && verde "  creado: $IDENTIFICADOR_API/ingesta"
  rm -rf "$TMP"
fi
SCOPE_INGESTA="$IDENTIFICADOR_API/ingesta"

# --------------------------------------------------------------------------
# 4. App client del frontend
#    SIN secreto: un React corre en el navegador y no puede guardar secretos.
#    Se protege con PKCE, que lo pone la libreria del frontend, no el pool.
# --------------------------------------------------------------------------
titulo "4. App client del frontend (SPA)"
CLIENT_ID="$(aws_ cognito-idp list-user-pool-clients --user-pool-id "$POOL_ID" --max-results 60 \
             --query "UserPoolClients[?ClientName=='frontend'].ClientId | [0]" --output text)"
if [[ "$CLIENT_ID" != "None" && -n "$CLIENT_ID" ]]; then
  verde "  ya existe: $CLIENT_ID"
else
  TMP="$(mktemp -d)"
  cat > "$TMP/spa.json" <<JSON
{
  "UserPoolId": "$POOL_ID",
  "ClientName": "frontend",
  "GenerateSecret": false,
  "AllowedOAuthFlows": ["code"],
  "AllowedOAuthFlowsUserPoolClient": true,
  "AllowedOAuthScopes": ["openid","email","profile"],
  "CallbackURLs": [
    "http://localhost:5173/callback",
    "http://localhost:3000/callback"
  ],
  "LogoutURLs": [
    "http://localhost:5173",
    "http://localhost:3000"
  ],
  "SupportedIdentityProviders": ["COGNITO"],
  "ExplicitAuthFlows": ["ALLOW_REFRESH_TOKEN_AUTH","ALLOW_USER_SRP_AUTH"],
  "PreventUserExistenceErrors": "ENABLED"
}
JSON
  CLIENT_ID="$(aws_ cognito-idp create-user-pool-client --cli-input-json "file://$TMP/spa.json" \
               --query 'UserPoolClient.ClientId' --output text)"
  rm -rf "$TMP"
  verde "  creado: $CLIENT_ID"
  gris  "  callbacks: localhost 5173 y 3000. Las de CloudFront se agregan en el paso 7 del despliegue."
fi

# --------------------------------------------------------------------------
# 5. App client del scraper (maquina, no persona)
#    client_credentials: no hay usuario detras, es un proceso pidiendo permiso.
#    Este SI lleva secreto, porque corre en un servidor.
# --------------------------------------------------------------------------
titulo "5. App client del scraper (maquina)"
CLIENT_SCRAPER="$(aws_ cognito-idp list-user-pool-clients --user-pool-id "$POOL_ID" --max-results 60 \
                  --query "UserPoolClients[?ClientName=='scraper'].ClientId | [0]" --output text)"
if [[ "$CLIENT_SCRAPER" != "None" && -n "$CLIENT_SCRAPER" ]]; then
  verde "  ya existe: $CLIENT_SCRAPER"
else
  TMP="$(mktemp -d)"
  cat > "$TMP/maq.json" <<JSON
{
  "UserPoolId": "$POOL_ID",
  "ClientName": "scraper",
  "GenerateSecret": true,
  "AllowedOAuthFlows": ["client_credentials"],
  "AllowedOAuthFlowsUserPoolClient": true,
  "AllowedOAuthScopes": ["$SCOPE_INGESTA"],
  "SupportedIdentityProviders": ["COGNITO"]
}
JSON
  CLIENT_SCRAPER="$(aws_ cognito-idp create-user-pool-client --cli-input-json "file://$TMP/maq.json" \
                    --query 'UserPoolClient.ClientId' --output text)"
  rm -rf "$TMP"
  verde "  creado: $CLIENT_SCRAPER"
fi
SECRETO_SCRAPER="$(aws_ cognito-idp describe-user-pool-client --user-pool-id "$POOL_ID" \
                   --client-id "$CLIENT_SCRAPER" --query 'UserPoolClient.ClientSecret' --output text 2>/dev/null)"

# --------------------------------------------------------------------------
# 6. Grupos
#    El grupo viaja dentro del token como "cognito:groups". Es lo que el BFF
#    mira para decidir 403. Sin grupos no se puede demostrar el caso 403.
# --------------------------------------------------------------------------
titulo "6. Grupos"
for g in admin usuario; do
  if aws_ cognito-idp get-group --group-name "$g" --user-pool-id "$POOL_ID" >/dev/null 2>&1; then
    verde "  ya existe: $g"
  else
    aws_ cognito-idp create-group --group-name "$g" --user-pool-id "$POOL_ID" \
      --description "grupo $g" >/dev/null && verde "  creado: $g"
  fi
done

# --------------------------------------------------------------------------
# 7. Usuarios de prueba
#    Uno por grupo, para poder mostrar 200 y 403 en la demo.
# --------------------------------------------------------------------------
titulo "7. Usuarios de prueba"
if [[ -z "${CLAVE_PRUEBA:-}" ]]; then
  CLAVE_PRUEBA="Cacha$(tr -dc 'A-Za-z0-9' </dev/urandom | head -c 10)1"
  NUEVA_CLAVE=1
fi
crear_usuario() {
  local correo="$1" grupo="$2"
  if aws_ cognito-idp admin-get-user --user-pool-id "$POOL_ID" --username "$correo" >/dev/null 2>&1; then
    verde "  ya existe: $correo ($grupo)"
  else
    aws_ cognito-idp admin-create-user --user-pool-id "$POOL_ID" --username "$correo" \
      --user-attributes Name=email,Value="$correo" Name=email_verified,Value=true \
      --message-action SUPPRESS >/dev/null || return 1
    # Clave definitiva: sin esto el usuario queda en FORCE_CHANGE_PASSWORD y no
    # puede entrar por el Hosted UI sin pasar por el cambio obligatorio.
    aws_ cognito-idp admin-set-user-password --user-pool-id "$POOL_ID" --username "$correo" \
      --password "$CLAVE_PRUEBA" --permanent >/dev/null
    aws_ cognito-idp admin-add-user-to-group --user-pool-id "$POOL_ID" --username "$correo" \
      --group-name "$grupo" >/dev/null
    verde "  creado: $correo ($grupo)"
  fi
}
crear_usuario "admin@cachaelprecio.cl"   admin
crear_usuario "usuario@cachaelprecio.cl" usuario

# --------------------------------------------------------------------------
# 8. Dejar los datos donde el frontend y el BFF los puedan leer
# --------------------------------------------------------------------------
titulo "8. Archivo de configuracion"
EMISOR="https://cognito-idp.$REGION.amazonaws.com/$POOL_ID"
cat > "$SALIDA" <<JSON
# Generado por tools/crear-cognito.sh el $(date '+%d-%m-%Y %H:%M')
# NO se sube al repo: cambia con cada cuenta. Se regenera corriendo el script.

COGNITO_REGION=$REGION
COGNITO_USER_POOL_ID=$POOL_ID
COGNITO_DOMINIO=$DOMINIO
COGNITO_URL_LOGIN=$URL_LOGIN

# --- frontend (publico, va compilado en el bundle) ---
VITE_COGNITO_CLIENT_ID=$CLIENT_ID
VITE_COGNITO_AUTHORITY=$EMISOR
VITE_COGNITO_REDIRECT_URI=http://localhost:5173/callback

# --- BFF: con esto valida el token ---
COGNITO_ISSUER=$EMISOR
COGNITO_JWKS_URI=$EMISOR/.well-known/jwks.json
COGNITO_AUDIENCE=$CLIENT_ID

# --- scraper (maquina) ---
COGNITO_SCRAPER_CLIENT_ID=$CLIENT_SCRAPER
COGNITO_SCRAPER_CLIENT_SECRET=$SECRETO_SCRAPER
COGNITO_SCRAPER_SCOPE=$SCOPE_INGESTA

# --- usuarios de prueba ---
COGNITO_USUARIO_ADMIN=admin@cachaelprecio.cl
COGNITO_USUARIO_NORMAL=usuario@cachaelprecio.cl
COGNITO_CLAVE_PRUEBA=$CLAVE_PRUEBA
JSON
chmod 600 "$SALIDA"
verde "  escrito: $SALIDA"

titulo "Listo"
cat <<RESUMEN
  User Pool     $POOL_ID
  Hosted UI     $URL_LOGIN
  Client SPA    $CLIENT_ID
  Client maquina$( printf '%s' "  $CLIENT_SCRAPER" )
  Emisor        $EMISOR
  JWKS          $EMISOR/.well-known/jwks.json

  Probar el login a mano (pegar en el navegador):
  $URL_LOGIN/login?client_id=$CLIENT_ID&response_type=code&scope=openid+email+profile&redirect_uri=http://localhost:5173/callback

RESUMEN
[[ "${NUEVA_CLAVE:-0}" == "1" ]] && gris "  La clave de los usuarios de prueba se genero sola y quedo en cognito.env"
gris "  cognito.env NO se sube al repo. Para compartirlo con el equipo, mandalo por otro medio."
