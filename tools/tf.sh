#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Atajo de Terraform para "Cacha el Precio" (fase 5, ADR-033).
#
# POR QUE ESTE SCRIPT EXISTE
#   Terraform (infra/terraform/cuenta/) ya sabe montar la cuenta entera, pero
#   solo no alcanza: los secretos tienen que entrar por variables de entorno,
#   desplegar.sh sigue leyendo los mismos 5 .env que dejaban los scripts, el
#   estado hay que respaldarlo, y apagar la maquina sin respaldar la base es
#   perder el historial de precios (paso de verdad el 10-09). Esto junta todo
#   eso para que ningun cuidado dependa de acordarse.
#
# Uso:
#   ./tools/tf.sh subir  <nombre>   crea o prende todo, escribe los .env y, si la
#                                   maquina es nueva, despliega y restaura
#   ./tools/tf.sh apagar <nombre>   respalda la base al laptop y baja SOLO la EC2
#                                   (y la IP, salvo conservar_ip). Lo demas sigue vivo
#   ./tools/tf.sh estado <nombre>   que hay en la cuenta y si coincide con el codigo
#   ./tools/tf.sh plan   <nombre>   solo mirar el plan, sin aplicar
#
#   <nombre> es el de infra/terraform/cuenta/cuentas/<nombre>.tfvars
#
# Opciones (variables de entorno):
#   LLAVE=~/labsuser.pem   llave de la EC2 (para respaldar y desplegar)
#   RAMA=development       rama que despliega desplegar.sh en una maquina nueva
#   SI=1                   no preguntar "aplico?" (el BORRAR nunca se salta)
#   SIN_RESPALDO=1         apagar sin respaldar. Pierde el historial de precios.
# ---------------------------------------------------------------------------
set -uo pipefail

RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
TF_DIR="$RAIZ/infra/terraform/cuenta"
LLAVE="${LLAVE:-$HOME/labsuser.pem}"
ESPEJO="$HOME/.terraform.d/mirror"

verde()  { printf '\033[0;32m%s\033[0m\n' "$1"; }
rojo()   { printf '\033[0;31m%s\033[0m\n' "$1"; }
gris()   { printf '\033[0;90m%s\033[0m\n' "$1"; }
amar()   { printf '\033[0;33m%s\033[0m\n' "$1"; }
titulo() { printf '\n\033[1m== %s ==\033[0m\n' "$1"; }

ACCION="${1:-}"
NOMBRE="${2:-}"
case "$ACCION" in
  subir|apagar|estado|plan) ;;
  *) sed -n '14,28p' "$0" | sed 's/^# \{0,1\}//'; exit 1 ;;
esac
TFVARS="$TF_DIR/cuentas/$NOMBRE.tfvars"
[[ -n "$NOMBRE" && -f "$TFVARS" ]] || {
  rojo "No existe cuentas/$NOMBRE.tfvars."
  gris "  cp $TF_DIR/cuentas/ejemplo.tfvars.example $TF_DIR/cuentas/<tu-nombre>.tfvars"
  exit 1
}

# Lee una variable de un .env sin ejecutarlo (los .env traen secretos, no codigo).
leer() { [[ -f "$1" ]] && grep -E "^$2=" "$1" | tail -1 | cut -d= -f2- ; }

# ---------------------------------------------------------------------------
# 0. Credenciales y cuenta, ANTES de tocar nada
#    Duran ~4 h. Un apply que se corta a la mitad se retoma, pero es mejor no
#    empezar con credenciales que vencen en 5 minutos.
# ---------------------------------------------------------------------------
titulo "0. Comprobaciones"
CUENTA="$(aws sts get-caller-identity --query Account --output text 2>/dev/null)" || CUENTA=""
if [[ -z "$CUENTA" ]]; then
  rojo "  Credenciales vencidas o ausentes."
  gris "  Learner Lab -> AWS Details -> AWS CLI: Show -> pegar en ~/.aws/credentials"
  exit 1
fi
ESPERADA="$(grep -E '^\s*cuenta_esperada' "$TFVARS" | grep -oE '[0-9]{12}')"
if [[ "$CUENTA" != "$ESPERADA" ]]; then
  rojo "  Las credenciales son de $CUENTA y $NOMBRE.tfvars dice $ESPERADA. No sigo."
  exit 1
fi
verde "  cuenta $CUENTA ($NOMBRE)"

# ---------------------------------------------------------------------------
# 1. Secretos: entran por variable de entorno, nunca por un .tfvars
#    Se reutilizan los que ya existen. Si el del borde cambiara, Caddy le
#    responderia 403 al API Gateway hasta volver a desplegar.
# ---------------------------------------------------------------------------
generar() { head -c 48 /dev/urandom | base64 | tr -d '/+=' | head -c "$1"; }

BORDE="$(leer "$RAIZ/borde.env" BORDE_SECRETO)"
if [[ -z "$BORDE" ]]; then
  BORDE="$(generar 40)"
  # Se guarda YA, no despues del apply: si el apply se corta, el siguiente intento
  # tiene que usar el mismo valor que quedo en las integraciones.
  ( umask 077; cat > "$RAIZ/borde.env" <<FIN
# Secreto que el API Gateway inyecta en cada peticion hacia la EC2, y que Caddy
# exige en el puerto 8080. Generado por tools/tf.sh. NO se sube al repositorio.
BORDE_SECRETO=$BORDE
FIN
  )
  amar "  borde.env no existia: secreto nuevo generado"
fi

CLAVE="$(leer "$RAIZ/cognito.env" COGNITO_CLAVE_PRUEBA)"
[[ -n "$CLAVE" ]] || { CLAVE="Cacha$(generar 10)1"; amar "  clave de prueba nueva (se guarda en cognito.env)"; }

export TF_VAR_borde_secreto="$BORDE" TF_VAR_clave_prueba="$CLAVE"
export TF_VAR_google_client_id="$(leer "$RAIZ/google.env" GOOGLE_CLIENT_ID)"
export TF_VAR_google_client_secret="$(leer "$RAIZ/google.env" GOOGLE_CLIENT_SECRET)"
export CHECKPOINT_DISABLE=1

cd "$TF_DIR" || exit 1
tf() { terraform "$@" -no-color; }

if [[ ! -d .terraform ]]; then
  titulo "terraform init"
  if [[ -d "$ESPEJO/registry.terraform.io/hashicorp/aws" ]]; then
    tf init -input=false -plugin-dir="$ESPEJO" >/dev/null || exit 1
  else
    tf init -input=false >/dev/null || { rojo "  init fallo. Si se corta la descarga, ver infra/terraform/README.md"; exit 1; }
  fi
fi

# ---------------------------------------------------------------------------
# Escribir los 5 .env a partir de los outputs, con los mismos nombres que
# dejaban los scripts: desplegar.sh no se entera del cambio.
# ---------------------------------------------------------------------------
escribir_env() {
  local salida="$1" archivo="$2" json
  json="$(terraform output -json "$salida" 2>/dev/null)" || { rojo "  sin output $salida"; return 1; }
  ( umask 077
    { echo "# Generado por tools/tf.sh ($NOMBRE, cuenta $CUENTA) el $(date '+%d-%m-%Y %H:%M')."
      echo "# NO se sube al repositorio. Se regenera en cada ./tools/tf.sh subir."
      jq -r 'to_entries[] | "\(.key)=\(.value)"' <<<"$json"
    } > "$RAIZ/$archivo.nuevo"
  )
  mv "$RAIZ/$archivo.nuevo" "$RAIZ/$archivo"
  gris "  $archivo"
}

escribir_todos() {
  titulo "Archivos .env"
  local pool_antes; pool_antes="$(leer "$RAIZ/cognito.env" COGNITO_USER_POOL_ID)"
  escribir_env mensajeria_env mensajeria.env
  escribir_env infra_env      infra.env
  escribir_env cognito_env    cognito.env
  escribir_env api_gateway_env api-gateway.env
  # borde.env ya esta escrito (paso 1) y no lo maneja ningun output.
  local pool_ahora; pool_ahora="$(leer "$RAIZ/cognito.env" COGNITO_USER_POOL_ID)"
  if [[ -n "$pool_antes" && "$pool_antes" != "$pool_ahora" ]]; then
    amar "  ⚠️ El user pool CAMBIO ($pool_antes -> $pool_ahora)."
    amar "     Hay que recompilar el frontend (desplegar.sh --solo-front) y volver a"
    amar "     correr ./tools/hacer-admin.sh: los usuarios del pool viejo no existen aqui."
  fi
}

# Copia del estado despues de cada apply. Se queda con las 5 ultimas.
respaldar_estado() {
  local dest="$HOME/cacha-cuentas/$CUENTA/estado"
  mkdir -p "$dest" && chmod 700 "$dest"
  [[ -f terraform.tfstate ]] || return 0
  cp terraform.tfstate "$dest/terraform.tfstate.$(date +%F_%H%M%S)"
  chmod 600 "$dest"/terraform.tfstate.*
  ls -1t "$dest"/terraform.tfstate.* | tail -n +6 | xargs -r rm
  gris "  estado copiado a $dest"
}

# ---------------------------------------------------------------------------
# Plan + barrera + apply. Aplica EXACTAMENTE el plan revisado, no uno nuevo.
# ---------------------------------------------------------------------------
planear_y_aplicar() {
  titulo "Plan"
  tf plan -input=false -var-file="$TFVARS" "$@" -out=plan.bin > plan.txt 2>&1
  local rc=$?
  if [[ $rc -ne 0 ]]; then rojo "  el plan fallo:"; tail -20 plan.txt; rm -f plan.bin; return 1; fi
  grep -E '^\s+# .* (will|must)|^Plan:|^No changes' plan.txt | sed 's/^ *//' | sed 's/^/  /'
  if grep -q '^No changes' plan.txt; then rm -f plan.bin plan.txt; verde "  nada que cambiar"; return 0; fi

  # Barrera: lo que se BORRA o se REEMPLAZA, fuera de la EC2 y su IP.
  local peligro
  peligro="$(terraform show -json plan.bin | jq -r '
    .resource_changes[]
    | select(.change.actions | index("delete"))
    | select(.type != "aws_instance" and .type != "aws_eip" and .type != "aws_eip_association")
    | .address')"
  if [[ -n "$peligro" ]]; then
    rojo "  ⚠️ Este plan BORRA o REEMPLAZA (fuera de la EC2 y la IP):"
    sed 's/^/     /' <<<"$peligro"
    rojo "     Un pool de Cognito o un cliente nuevo cambian de id; las colas pierden sus mensajes."
    read -r -p "  Escribe BORRAR para seguir: " r
    [[ "$r" == "BORRAR" ]] || { gris "  cancelado"; rm -f plan.bin; return 1; }
  elif [[ "${SI:-0}" != "1" ]]; then
    read -r -p "  ¿Aplico este plan? [s/N] " r
    [[ "$r" == "s" || "$r" == "S" ]] || { gris "  cancelado"; rm -f plan.bin; return 1; }
  fi

  titulo "Apply"
  tf apply -input=false plan.bin 2>&1 | grep -E 'Apply complete|Error|error' | sed 's/^/  /'
  rc=${PIPESTATUS[0]}
  rm -f plan.bin plan.txt
  respaldar_estado
  [[ $rc -eq 0 ]] || { rojo "  el apply fallo. Si fue ExpiredToken: credenciales nuevas y otra vez."; return 1; }
}

ssh_ec2() { ssh -i "$LLAVE" -o StrictHostKeyChecking=accept-new -o ConnectTimeout=15 -o LogLevel=ERROR "ec2-user@$1" "${@:2}"; }

# Para mirar (plan, estado) se respeta como esta la maquina: si esta apagada,
# un plan con encendido = true la mostraria como "cambio pendiente".
# La lista va a una variable y no a un pipe con grep -q: con pipefail, grep -q
# corta el pipe, terraform muere por SIGPIPE y la condicion da falso.
RECURSOS="$(terraform state list 2>/dev/null)"
if grep -q '^aws_instance\.app\[' <<< "$RECURSOS"; then ENCENDIDO=true
elif [[ -n "$RECURSOS" ]]; then ENCENDIDO=false
else ENCENDIDO=true; fi

# ---------------------------------------------------------------------------
case "$ACCION" in
# ---------------------------------------------------------------------------
plan)
  tf plan -input=false -var-file="$TFVARS" -var encendido=$ENCENDIDO
  ;;

# ---------------------------------------------------------------------------
estado)
  titulo "Lo que maneja Terraform"
  printf '  %s recursos en el estado\n' "$(terraform state list 2>/dev/null | wc -l)"
  ID="$(leer "$RAIZ/infra.env" INFRA_INSTANCIA)"
  if [[ -n "$ID" ]]; then
    printf '  EC2 %s: %s\n' "$ID" "$(aws ec2 describe-instances --instance-ids "$ID" \
      --query 'Reservations[0].Instances[0].State.Name' --output text 2>/dev/null)"
  else
    gris "  EC2 apagada (encendido = false)"
  fi
  titulo "¿Coincide AWS con el codigo?"
  tf plan -input=false -var-file="$TFVARS" -var encendido=$ENCENDIDO -detailed-exitcode > /dev/null 2>&1
  case $? in
    0) verde "  si: no hay cambios pendientes" ;;
    2) amar  "  no: hay cambios pendientes (./tools/tf.sh plan $NOMBRE para verlos)" ;;
    *) rojo  "  el plan fallo (./tools/tf.sh plan $NOMBRE)" ;;
  esac
  ;;

# ---------------------------------------------------------------------------
subir)
  ID_ANTES="$(terraform output -json infra_env 2>/dev/null | jq -r '.INFRA_INSTANCIA // ""')"
  planear_y_aplicar -var encendido=true || exit 1
  escribir_todos
  ID_AHORA="$(leer "$RAIZ/infra.env" INFRA_INSTANCIA)"
  IP="$(leer "$RAIZ/infra.env" INFRA_IP)"

  if [[ -n "$ID_AHORA" && "$ID_AHORA" != "$ID_ANTES" ]]; then
    # Maquina nueva: esperar al cloud-init (Docker y compose) y desplegar.
    titulo "Maquina nueva: despliegue"
    gris "  esperando a que termine el arranque (Docker y compose)..."
    for _ in $(seq 1 60); do
      ssh_ec2 "$IP" 'test -f /var/log/cacha-listo' 2>/dev/null && break
      sleep 10
    done
    ssh_ec2 "$IP" 'test -f /var/log/cacha-listo' || { rojo "  la maquina no termino de arrancar en 10 min"; exit 1; }
    RAMA="${RAMA:-development}" LLAVE="$LLAVE" "$RAIZ/tools/desplegar.sh" || exit 1

    # La base nace vacia. Si hay un respaldo en el laptop, se ofrece restaurarlo.
    ULTIMO="$(ls -1dt "$RAIZ"/respaldos/*/ 2>/dev/null | head -1)"
    if [[ -n "$ULTIMO" && -s "$ULTIMO/database.sql" ]]; then
      titulo "Restaurar el ultimo respaldo"
      gris "  $ULTIMO ($(du -h "$ULTIMO/database.sql" | cut -f1))"
      r="s"; [[ "${SI:-0}" == "1" ]] || read -r -p "  ¿Lo restauro en la maquina nueva? [S/n] " r
      if [[ "$r" != "n" && "$r" != "N" ]]; then
        # Se sube a /tmp: respaldos/ en la EC2 es de root (lo crea el respaldo horario).
        DIR_TMP="/tmp/restaurar-$(basename "$ULTIMO")"
        # Se reintenta y se revisa el resultado. El 17-11, en la prueba desde cero,
        # la copia se corto una vez ("Connection closed by remote host") y el script
        # siguio como si nada: terminaba en 0 con la base vacia. Al reintentar a mano
        # funciono, asi que el corte es pasajero.
        # Antes de cada intento se borra DIR_TMP: si quedo a medias, `scp -r` copiaria
        # el respaldo DENTRO de la carpeta vieja y la restauracion no lo encontraria.
        restaurado=0
        for intento in 1 2 3; do
          if ssh_ec2 "$IP" "rm -rf $DIR_TMP" \
             && scp -i "$LLAVE" -o LogLevel=ERROR -q -r "$ULTIMO" "ec2-user@$IP:$DIR_TMP" \
             && ssh_ec2 "$IP" "cd /opt/cacha-el-precio && echo RESTAURAR | sudo ./tools/respaldar.sh --restaurar $DIR_TMP" \
                | sed 's/^/  /'; then
            restaurado=1; break
          fi
          (( intento < 3 )) && { amar "  intento $intento fallo; reintento en 15 s"; sleep 15; }
        done
        if (( restaurado == 0 )); then
          rojo "  NO se pudo restaurar el respaldo: la base quedo VACIA."
          gris "  a mano, desde la raiz del repo:"
          gris "    scp -i $LLAVE -r $ULTIMO ec2-user@$IP:$DIR_TMP"
          gris "    ssh -i $LLAVE ec2-user@$IP 'cd /opt/cacha-el-precio && sudo ./tools/respaldar.sh --restaurar $DIR_TMP'"
          FALLO_RESTAURAR=1
        fi
      fi
    else
      amar "  no hay respaldo en respaldos/: el catalogo parte vacio (correr el scraper)"
    fi
  fi

  titulo "Listo"
  API="$(leer "$RAIZ/api-gateway.env" API_GATEWAY_URL_PROD)"
  echo "  API   $API"
  echo "  Sitio $(leer "$RAIZ/infra.env" INFRA_SITIO_URL)"
  [[ -n "$IP" ]] && echo "  EC2   $IP"
  gris "  comprobar: curl -s -o /dev/null -w '%{http_code}\\n' $API/health   # 200"
  # Todo lo demas quedo arriba, pero sin los datos no es un "listo": se sale con error.
  [[ "${FALLO_RESTAURAR:-0}" == "1" ]] && { rojo "  OJO: la base quedo vacia (ver arriba)."; exit 1; }
  ;;

# ---------------------------------------------------------------------------
apagar)
  ID="$(terraform output -json infra_env 2>/dev/null | jq -r '.INFRA_INSTANCIA // ""')"
  IP="$(terraform output -json infra_env 2>/dev/null | jq -r '.INFRA_IP // ""')"
  if [[ -z "$ID" ]]; then
    verde "  la maquina ya esta apagada"
  elif [[ "${SIN_RESPALDO:-0}" == "1" ]]; then
    rojo "  SIN_RESPALDO=1: se apaga SIN guardar la base. El historial se pierde."
  else
    # -----------------------------------------------------------------------
    # RESPALDAR ANTES DE DESTRUIR (regla del 10-09, cuando se perdieron 221
    # productos con su historial). La base vive en el disco de la EC2 y ese
    # disco se va con ella. Si algo falla, NO se apaga nada.
    # -----------------------------------------------------------------------
    titulo "Respaldo antes de apagar"
    [[ -f "$LLAVE" ]] || { rojo "  No encuentro la llave $LLAVE. NO apago nada."; exit 1; }
    ESTADO="$(aws ec2 describe-instances --instance-ids "$ID" --query 'Reservations[0].Instances[0].State.Name' --output text)"
    if [[ "$ESTADO" != "running" ]]; then
      amar "  la EC2 esta $ESTADO: la prendo para poder respaldar"
      aws ec2 start-instances --instance-ids "$ID" >/dev/null && aws ec2 wait instance-status-ok --instance-ids "$ID" \
        || { rojo "  no pude prenderla. NO apago nada."; exit 1; }
      sleep 20   # que el compose termine de levantar Postgres
    fi
    # Con sudo, como el respaldo automatico de cada hora: respaldos/ es de root.
    ssh_ec2 "$IP" 'cd /opt/cacha-el-precio && sudo ./tools/respaldar.sh' 2>&1 | sed 's/^/    /'
    [[ ${PIPESTATUS[0]} -eq 0 ]] || { rojo "  El respaldo fallo. NO apago nada."; exit 1; }
    REMOTA="$(ssh_ec2 "$IP" 'ls -1dt /opt/cacha-el-precio/respaldos/*/ 2>/dev/null | head -1')"
    [[ -n "$REMOTA" ]] || { rojo "  El respaldo no dejo carpeta. NO apago nada."; exit 1; }
    # Un respaldo guardado DENTRO de la maquina que se va a borrar no es un respaldo.
    mkdir -p "$RAIZ/respaldos"
    scp -i "$LLAVE" -o LogLevel=ERROR -q -r "ec2-user@$IP:$REMOTA" "$RAIZ/respaldos/" \
      || { rojo "  No pude bajar el respaldo. NO apago nada."; exit 1; }
    BAJADA="$RAIZ/respaldos/$(basename "$REMOTA")"
    [[ -s "$BAJADA/database.sql" ]] || { rojo "  El respaldo bajo vacio. NO apago nada."; exit 1; }
    verde "  respaldo a salvo en $BAJADA ($(du -h "$BAJADA/database.sql" | cut -f1))"
  fi

  planear_y_aplicar -var encendido=false || exit 1
  escribir_todos
  titulo "Listo"
  echo "  Quedan vivos Cognito, la API, las colas y los buckets."
  echo "  Para volver: ./tools/tf.sh subir $NOMBRE"
  ;;
esac
