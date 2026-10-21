#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Agrega (o quita) personas del grupo "admin" de Cognito, buscandolas por
# correo. Es lo que deja ver el panel de mensajeria (/admin/mensajeria).
#
# POR QUE ESTE SCRIPT EXISTE
#   En el sitio solo se entra con Google, y un usuario de Google NO existe en
#   el pool hasta que entra por primera vez. Ademas Cognito no lo nombra por su
#   correo sino como "Google_<numero>", que no se puede adivinar. Por eso
#   crear-cognito.sh no puede meterlo al grupo al crear el pool: todavia no hay
#   a quien meter. Este script lo busca por el correo, que si se conoce.
#
#   El orden obligado es:
#     1. entrar con Google en el sitio      (ahi nace el usuario)
#     2. ./tools/hacer-admin.sh tu@gmail.com
#     3. cerrar sesion y volver a entrar    (el grupo viaja DENTRO del token;
#                                            el que ya tienes no lo trae)
#
# ES IDEMPOTENTE: agregar a alguien que ya es admin no da error.
#
# Uso:
#   ./hacer-admin.sh correo@gmail.com [otro@gmail.com ...]
#   ./hacer-admin.sh --quitar correo@gmail.com
#   ./hacer-admin.sh --listar
#
# Lee el pool de cognito.env (lo escribe crear-cognito.sh).
#
# Codigos de salida: 0 todo bien · 1 error de AWS o de uso ·
#                    2 algun correo todavia no existe en el pool
# ---------------------------------------------------------------------------
set -uo pipefail

RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
GRUPO="admin"

verde()  { printf '\033[0;32m%s\033[0m\n' "$1"; }
rojo()   { printf '\033[0;31m%s\033[0m\n' "$1"; }
amar()   { printf '\033[0;33m%s\033[0m\n' "$1"; }
gris()   { printf '\033[0;90m%s\033[0m\n' "$1"; }

uso() {
  gris "  uso: ./tools/hacer-admin.sh correo@gmail.com [otro@gmail.com ...]"
  gris "       ./tools/hacer-admin.sh --quitar correo@gmail.com"
  gris "       ./tools/hacer-admin.sh --listar"
}

# --------------------------------------------------------------------------
# El pool: de cognito.env, que es de ESTA cuenta
# --------------------------------------------------------------------------
[[ -f "$RAIZ/cognito.env" ]] && . "$RAIZ/cognito.env"
REGION="${REGION:-${COGNITO_REGION:-us-east-1}}"
POOL_ID="${COGNITO_USER_POOL_ID:-}"
if [[ -z "$POOL_ID" ]]; then
  rojo "  No encuentro cognito.env. Corre antes ./tools/crear-cognito.sh"
  exit 1
fi

aws_() { aws --region "$REGION" "$@"; }

if ! CUENTA="$(aws_ sts get-caller-identity --query Account --output text 2>/dev/null)"; then
  rojo "  Las credenciales no sirven o vencieron."
  rojo "  Learner Lab -> Start Lab -> AWS Details -> AWS CLI: Show,"
  rojo "  y pega ese bloque en ~/.aws/credentials reemplazando [default]."
  exit 1
fi

# cognito.env puede haber quedado de OTRA cuenta (se cambio de laboratorio y no
# se volvio a correr crear-cognito.sh). Sin esta comprobacion el error seria un
# "ResourceNotFoundException" que no dice nada.
if ! aws_ cognito-idp describe-user-pool --user-pool-id "$POOL_ID" >/dev/null 2>&1; then
  rojo "  El pool $POOL_ID de cognito.env no existe en la cuenta $CUENTA."
  rojo "  Corre ./tools/crear-cognito.sh en esta cuenta y vuelve a intentarlo."
  exit 1
fi
gris "  cuenta $CUENTA, pool $POOL_ID"

# --------------------------------------------------------------------------
# --listar: quien es admin hoy
# --------------------------------------------------------------------------
if [[ "${1:-}" == "--listar" ]]; then
  aws_ cognito-idp list-users-in-group --user-pool-id "$POOL_ID" --group-name "$GRUPO" \
    --query "Users[].[Username, Attributes[?Name=='email'].Value | [0]]" --output text \
    | while read -r usuario correo; do
        printf '  %-45s %s\n' "$correo" "$usuario"
      done
  exit 0
fi

ACCION="agregar"
if [[ "${1:-}" == "--quitar" ]]; then
  ACCION="quitar"; shift
fi
if [[ $# -eq 0 ]]; then
  rojo "  Falta el correo."; uso; exit 1
fi

# --------------------------------------------------------------------------
# Un correo puede corresponder a MAS de un usuario: el de Google
# (Google_<numero>) y uno nativo creado con ese mismo correo. Se aplica a
# todos, porque no se sabe con cual va a entrar la persona.
# --------------------------------------------------------------------------
FALTAN=0
CAMBIOS=0
for CORREO in "$@"; do
  # Google entrega el correo en minusculas, y el filtro de Cognito distingue
  # mayusculas. Ademas va entre comillas dentro del filtro: una comilla en el
  # correo lo romperia, asi que se rechaza.
  CORREO="$(printf '%s' "$CORREO" | tr '[:upper:]' '[:lower:]')"
  if [[ ! "$CORREO" =~ ^[^[:space:]\"]+@[^[:space:]\"]+$ ]]; then
    rojo "  \"$CORREO\" no parece un correo; lo salto"
    FALTAN=1; continue
  fi

  USUARIOS="$(aws_ cognito-idp list-users --user-pool-id "$POOL_ID" \
              --filter "email = \"$CORREO\"" --query 'Users[].Username' --output text)" \
    || { rojo "  no pude buscar $CORREO en el pool"; exit 1; }

  if [[ -z "$USUARIOS" || "$USUARIOS" == "None" ]]; then
    amar "  $CORREO todavia no existe en el pool."
    gris "    Tiene que entrar UNA vez con Google en el sitio y despues"
    gris "    volver a correr esto. Recien ahi Cognito lo crea."
    FALTAN=1; continue
  fi

  for USUARIO in $USUARIOS; do
    if [[ "$ACCION" == "agregar" ]]; then
      aws_ cognito-idp admin-add-user-to-group --user-pool-id "$POOL_ID" \
        --username "$USUARIO" --group-name "$GRUPO" >/dev/null \
        && { verde "  $CORREO ahora es $GRUPO ($USUARIO)"; CAMBIOS=1; } \
        || { rojo "  no pude agregar a $USUARIO al grupo $GRUPO"; exit 1; }
    else
      aws_ cognito-idp admin-remove-user-from-group --user-pool-id "$POOL_ID" \
        --username "$USUARIO" --group-name "$GRUPO" >/dev/null \
        && { verde "  $CORREO ya no es $GRUPO ($USUARIO)"; CAMBIOS=1; } \
        || { rojo "  no pude quitar a $USUARIO del grupo $GRUPO"; exit 1; }
    fi
  done
done

(( CAMBIOS )) \
  && gris "  El cambio se ve en el PROXIMO token: cierra sesion en el sitio y vuelve a entrar."
exit $(( FALTAN ? 2 : 0 ))
