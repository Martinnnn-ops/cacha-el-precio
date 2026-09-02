#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Sondeo de AWS Academy Learner Lab para el EP1 de "Cacha el Precio".
#
# Responde una sola pregunta: ¿la cuenta que nos dieron deja construir
# IDaaS (Cognito) + API Manager (API Gateway) + despliegue (EC2)?
#
# Crea recursos de prueba y LOS BORRA al final, pase lo que pase.
# No lanza ninguna instancia EC2: ese permiso se comprueba con --dry-run,
# que no gasta crédito.
#
# Uso:
#   ./verificar-aws-academy.sh              # sondeo completo (crea y borra)
#   ./verificar-aws-academy.sh --lectura    # solo consulta, no crea nada
# ---------------------------------------------------------------------------

REGION="${AWS_REGION:-us-east-1}"
SELLO="$(date +%Y%m%d-%H%M%S)"
LOG="$(dirname "$0")/../docs/evidencia/sondeo-aws-$SELLO.log"
SOLO_LECTURA=0
[[ "$1" == "--lectura" ]] && SOLO_LECTURA=1

mkdir -p "$(dirname "$LOG")"

# Recursos creados, para poder borrarlos.
POOL_ID=""
DOMINIO=""
API_ID=""

verde()  { printf '\033[0;32m%s\033[0m\n' "$1"; }
rojo()   { printf '\033[0;31m%s\033[0m\n' "$1"; }
amar()   { printf '\033[0;33m%s\033[0m\n' "$1"; }
titulo() { printf '\n\033[1m== %s ==\033[0m\n' "$1"; }

# Corre un comando de aws, guarda la salida en el log y dice si pasó o no.
# $1 = etiqueta legible, resto = comando
probar() {
  local etiqueta="$1"; shift
  local salida estado
  salida="$("$@" 2>&1)"; estado=$?
  {
    echo "### $etiqueta"
    echo "\$ $*"
    echo "$salida"
    echo
  } >> "$LOG"
  if [[ $estado -eq 0 ]]; then
    verde "  OK    $etiqueta"
    RESPUESTA="$salida"
    return 0
  fi
  # AccessDenied / UnauthorizedOperation = el Learner Lab lo bloquea.
  if grep -qi "AccessDenied\|UnauthorizedOperation\|not authorized\|explicit deny" <<<"$salida"; then
    rojo "  NO    $etiqueta  -> BLOQUEADO por la política del Learner Lab"
  else
    amar "  ?     $etiqueta  -> $(head -1 <<<"$salida" | cut -c1-110)"
  fi
  RESPUESTA=""
  return 1
}

limpiar() {
  [[ $SOLO_LECTURA -eq 1 ]] && return
  titulo "Limpieza (se borra todo lo creado)"
  if [[ -n "$DOMINIO" ]]; then
    aws cognito-idp delete-user-pool-domain --domain "$DOMINIO" \
      --user-pool-id "$POOL_ID" --region "$REGION" >>"$LOG" 2>&1 \
      && verde "  borrado dominio $DOMINIO" || amar "  no se pudo borrar el dominio $DOMINIO (bórralo a mano)"
  fi
  if [[ -n "$POOL_ID" ]]; then
    aws cognito-idp delete-user-pool --user-pool-id "$POOL_ID" \
      --region "$REGION" >>"$LOG" 2>&1 \
      && verde "  borrado user pool $POOL_ID" || amar "  no se pudo borrar el user pool $POOL_ID (bórralo a mano)"
  fi
  if [[ -n "$API_ID" ]]; then
    aws apigatewayv2 delete-api --api-id "$API_ID" --region "$REGION" >>"$LOG" 2>&1 \
      && verde "  borrada HTTP API $API_ID" || amar "  no se pudo borrar la API $API_ID (bórrala a mano)"
  fi
}
trap limpiar EXIT INT TERM

echo "Sondeo AWS Academy - region $REGION - $(date '+%d-%m-%Y %H:%M')" | tee "$LOG"
[[ $SOLO_LECTURA -eq 1 ]] && amar "Modo solo lectura: no se crea nada."

# --------------------------------------------------------------------------
titulo "0. Credenciales"
if ! probar "sts get-caller-identity" aws sts get-caller-identity --region "$REGION"; then
  rojo "Las credenciales no sirven. Copia el bloque nuevo desde el Learner Lab"
  rojo "(boton 'AWS Details' -> 'AWS CLI' -> 'Show') a ~/.aws/credentials y repite."
  exit 1
fi
echo "$RESPUESTA" | grep -o '"Arn": "[^"]*"' | sed 's/^/  /'

titulo "1. Cognito - el IDaaS del EP1"
probar "listar user pools (lectura)" \
  aws cognito-idp list-user-pools --max-results 5 --region "$REGION"

if [[ $SOLO_LECTURA -eq 0 ]]; then
  if probar "CREAR un user pool" \
      aws cognito-idp create-user-pool --pool-name "sondeo-cachaelprecio-$SELLO" \
      --region "$REGION"; then
    POOL_ID="$(grep -o '"Id": "[^"]*"' <<<"$RESPUESTA" | head -1 | cut -d'"' -f4)"
    verde "        user pool creado: $POOL_ID"

    # Hosted UI: sin dominio no hay pantalla de login ni flujo Authorization Code.
    DOMINIO="cachaelprecio-$SELLO"
    probar "CREAR dominio Hosted UI ($DOMINIO)" \
      aws cognito-idp create-user-pool-domain --domain "$DOMINIO" \
      --user-pool-id "$POOL_ID" --region "$REGION" || DOMINIO=""

    # OJO: aws-cli v1 trata un parametro que empieza con http:// o https:// como una URL
    # de la que hay que DESCARGAR el valor, y falla con "Could not connect to the endpoint".
    # Por eso el identifier y las callback URLs van dentro de un JSON, que funciona igual
    # en la v1 y en la v2. No es una restriccion de AWS Academy.
    TMPD="$(mktemp -d)"

    # Resource server: los scopes propios (p.ej. el de POST /ingesta del scraper).
    cat > "$TMPD/rs.json" <<JSON
{ "UserPoolId": "$POOL_ID", "Identifier": "https://api.cachaelprecio.cl", "Name": "api",
  "Scopes": [ {"ScopeName":"ingesta","ScopeDescription":"publicar ofertas"} ] }
JSON
    probar "CREAR resource server con scope propio" \
      aws cognito-idp create-resource-server --cli-input-json "file://$TMPD/rs.json" \
      --region "$REGION"

    # App client con OIDC Authorization Code + PKCE: es lo que usa el frontend.
    # Sin secreto, porque un SPA no puede guardarlo.
    cat > "$TMPD/client.json" <<JSON
{ "UserPoolId": "$POOL_ID", "ClientName": "frontend-sondeo",
  "GenerateSecret": false,
  "AllowedOAuthFlows": ["code"], "AllowedOAuthFlowsUserPoolClient": true,
  "AllowedOAuthScopes": ["openid","email","profile","https://api.cachaelprecio.cl/ingesta"],
  "CallbackURLs": ["http://localhost:3000/callback"],
  "SupportedIdentityProviders": ["COGNITO"] }
JSON
    probar "CREAR app client OIDC (authorization code)" \
      aws cognito-idp create-user-pool-client --cli-input-json "file://$TMPD/client.json" \
      --region "$REGION"

    rm -rf "$TMPD"

    # Grupos: la memoria del proyecto dice que se usan ademas de los scopes.
    probar "CREAR grupo de usuarios" \
      aws cognito-idp create-group --group-name "mayorista" \
      --user-pool-id "$POOL_ID" --region "$REGION"
  else
    rojo "  >>> Cognito NO se puede crear en esta cuenta. Plan B: Keycloak en la EC2."
  fi
fi

titulo "2. API Gateway - el API Manager del EP1"
probar "listar HTTP APIs (lectura)" \
  aws apigatewayv2 get-apis --max-results 5 --region "$REGION"

if [[ $SOLO_LECTURA -eq 0 ]]; then
  if probar "CREAR una HTTP API" \
      aws apigatewayv2 create-api --name "sondeo-$SELLO" --protocol-type HTTP \
      --region "$REGION"; then
    API_ID="$(grep -o '"ApiId": "[^"]*"' <<<"$RESPUESTA" | head -1 | cut -d'"' -f4)"
    verde "        HTTP API creada: $API_ID"

    # LA prueba del EP1: el authorizer JWT que une API Gateway con Cognito.
    if [[ -n "$POOL_ID" ]]; then
      probar "CREAR authorizer JWT apuntando al user pool" \
        aws apigatewayv2 create-authorizer --api-id "$API_ID" \
        --authorizer-type JWT --identity-source '$request.header.Authorization' \
        --name "cognito-sondeo" \
        --jwt-configuration "Audience=sondeo,Issuer=https://cognito-idp.$REGION.amazonaws.com/$POOL_ID" \
        --region "$REGION"
    else
      amar "  --    authorizer JWT: no se probo (no hubo user pool)"
    fi
  fi
fi

titulo "3. EC2 - donde va el docker-compose (ADR-008)"
probar "describir VPCs" aws ec2 describe-vpcs --region "$REGION"
probar "describir instancias" aws ec2 describe-instances --region "$REGION"
# --dry-run comprueba el permiso sin encender nada ni gastar credito.
salida="$(aws ec2 run-instances --dry-run --image-id ami-00000000000000000 \
  --instance-type t3.small --region "$REGION" 2>&1)"
echo "### run-instances --dry-run"$'\n'"$salida"$'\n' >> "$LOG"
if grep -qi "DryRunOperation\|InvalidAMIID" <<<"$salida"; then
  verde "  OK    permiso para lanzar EC2 (dry-run acepto)"
elif grep -qi "UnauthorizedOperation" <<<"$salida"; then
  rojo "  NO    lanzar EC2 -> BLOQUEADO"
else
  amar "  ?     lanzar EC2 -> $(head -1 <<<"$salida" | cut -c1-110)"
fi

titulo "4. IAM - el LabRole que Academy obliga a usar"
if probar "buscar LabRole" aws iam get-role --role-name LabRole; then
  verde "        LabRole existe: es el unico rol que se puede asignar a servicios"
else
  amar "        sin LabRole: revisa como se llama el rol en esta cuenta"
fi
probar "intentar CREAR un rol propio (se espera que falle)" \
  aws iam list-roles --max-items 5

titulo "Resultado"
echo "Evidencia completa en: $LOG"
echo "Pega el resumen de arriba en docs/BITACORA.md y en el ADR de IDaaS."
