#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Crea la infraestructura de "Cacha el Precio": la maquina y el bucket.
#
# POR QUE ESTE SCRIPT EXISTE
#   Las cuentas son de AWS Academy y tienen credito limitado, asi que tarde o
#   temprano hay que saltar a la siguiente. Cuando eso pase, esto tiene que
#   volver a levantarse con un comando y no con una tarde de memoria.
#
#   crear-cognito.sh ya rehace la identidad y crear-api-gateway.sh rehace el
#   borde. Faltaba la parte de abajo: la maquina donde corre el compose, su
#   direccion fija, y el bucket donde vive el sitio compilado. Eso es esto.
#
#   La idea es que las tres cuentas queden IGUALES. El dominio y el cliente de
#   Google se quedan quietos; lo unico que se mueve al migrar es a donde
#   apuntan.
#
# QUE CREA
#   · Un grupo de seguridad con 22, 80 y 443 abiertos
#   · Una EC2 con Docker y el plugin de compose ya instalados, y con el perfil
#     de instancia puesto desde el arranque (asi sirve SSM y Parameter Store)
#   · Una Elastic IP, asociada a esa maquina (la IP deja de cambiar al apagar)
#   · Un bucket S3 privado para el sitio compilado
#
# QUE **NO** HACE, A PROPOSITO
#   · No despliega la aplicacion: eso necesita el .env con secretos, que no
#     viaja en el repo. El script deja la maquina lista y te dice como seguir.
#   · No toca el DNS: Cloudflare esta fuera de AWS. Es el unico paso manual.
#
# ES IDEMPOTENTE: si algo ya existe lo reutiliza. Se puede correr las veces que
# haga falta.
#
# Uso:
#   ./crear-infra.sh              # crea lo que falte
#   ./crear-infra.sh --borrar     # RESPALDA, se lo baja, y borra la maquina
#   ./crear-infra.sh --borrar --sin-respaldo   # borra sin respaldar (peligroso)
#
# Variables opcionales:
#   REGION        (por defecto us-east-1)
#   TIPO          tipo de instancia   (por defecto t3.micro)
#   BUCKET        nombre del bucket   (por defecto cacha-el-precio-web-<cuenta>)
# ---------------------------------------------------------------------------
set -uo pipefail

RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
SALIDA="$RAIZ/infra.env"

REGION="${REGION:-us-east-1}"
# t3.small (2 GB) y no t3.micro (1 GB). Medido el 10-09 con el sistema en
# marcha: los 5 contenedores usan ~355 MB, o sea que en una micro quedaban 225
# MB libres y ya habia 81 MB en swap sin estar haciendo nada. El que aprieta es
# compilar las imagenes de .NET. La diferencia son 0,0104 USD/hora.
TIPO="${TIPO:-t3.small}"
NOMBRE="cacha-el-precio"
SG_NOMBRE="$NOMBRE-sg"

verde()  { printf '\033[0;32m%s\033[0m\n' "$1"; }
rojo()   { printf '\033[0;31m%s\033[0m\n' "$1"; }
gris()   { printf '\033[0;90m%s\033[0m\n' "$1"; }
titulo() { printf '\n\033[1m== %s ==\033[0m\n' "$1"; }

aws_() { aws --region "$REGION" "$@"; }

# ---------------------------------------------------------------------------
# 0. Comprobaciones
# ---------------------------------------------------------------------------
titulo "0. Comprobaciones"
CUENTA="$(aws_ sts get-caller-identity --query Account --output text 2>/dev/null)"
if [[ -z "$CUENTA" || "$CUENTA" == "None" ]]; then
  rojo "  no hay credenciales validas. Abre el Learner Lab y copia el bloque [default]."
  exit 1
fi
verde "  cuenta $CUENTA, region $REGION"
BUCKET="${BUCKET:-$NOMBRE-web-$CUENTA}"
# Bucket SEPARADO para los respaldos, y separado a proposito.
#
# El del sitio es de lectura publica (lleva la web dentro). Si los volcados de
# la base de datos vivieran ahi, cualquiera con la URL se bajaria los datos.
# Son dos cosas con permisos opuestos, asi que son dos buckets.
BUCKET_RESPALDOS="${BUCKET_RESPALDOS:-$NOMBRE-respaldos-$CUENTA}"

# ---------------------------------------------------------------------------
# --borrar: soltar la maquina y la IP. El bucket NO se borra solo: tiene el
# sitio dentro y borrarlo por accidente es caro de deshacer.
# ---------------------------------------------------------------------------
if [[ "${1:-}" == "--borrar" ]]; then
  titulo "Borrando"
  ID="$(aws_ ec2 describe-instances \
        --filters "Name=tag:Name,Values=$NOMBRE" "Name=instance-state-name,Values=pending,running,stopped,stopping" \
        --query 'Reservations[0].Instances[0].InstanceId' --output text 2>/dev/null)"

  # -------------------------------------------------------------------------
  # RESPALDAR ANTES DE DESTRUIR. Esto se anadio el 10-09 despues de perder de
  # verdad 221 productos con su historial.
  #
  # Que fallo: respaldar.sh se ejecuta DENTRO de la EC2 (necesita el docker
  # compose) y este script se ejecuta en el portatil. Eran dos programas que no
  # se conocian: el que destruye nunca llamaba al que protege, no avisaba, y no
  # comprobaba nada. Bastaba con olvidarse una vez.
  #
  # La base de datos vive en un volumen de Docker sobre el disco raiz de la
  # instancia, y ese disco se borra junto con la instancia. El catalogo lo
  # repone el scraper, pero EL HISTORIAL DE PRECIOS NO: es una serie en el
  # tiempo, no un calculo. Los dias perdidos no vuelven.
  #
  # Ahora: se respalda solo, se baja al portatil, y si algo falla NO se borra
  # nada. Para saltarselo hay que pedirlo a mano con --sin-respaldo.
  # -------------------------------------------------------------------------
  if [[ -n "$ID" && "$ID" != "None" && "${2:-}" != "--sin-respaldo" ]]; then
    titulo "Respaldando antes de borrar"
    IP_ACTUAL="$(aws_ ec2 describe-instances --instance-ids "$ID" \
                 --query 'Reservations[0].Instances[0].PublicIpAddress' --output text 2>/dev/null)"
    LLAVE_SSH="${LLAVE:-$HOME/labsuser.pem}"

    if [[ ! -f "$LLAVE_SSH" ]]; then
      rojo "  No encuentro la llave $LLAVE_SSH y sin ella no puedo respaldar."
      rojo "  NO borro nada. Pasa LLAVE=/ruta/a.pem, o --sin-respaldo si de verdad"
      rojo "  no te importan los datos."
      exit 1
    fi

    SSH_R="ssh -i $LLAVE_SSH -o StrictHostKeyChecking=accept-new -o ConnectTimeout=15 ec2-user@$IP_ACTUAL"
    if ! $SSH_R 'cd /opt/cacha-el-precio && ./tools/respaldar.sh' 2>&1 | sed 's/^/    /'; then
      rojo "  El respaldo fallo. NO borro nada."
      rojo "  Si de verdad quieres perder los datos: $0 --borrar --sin-respaldo"
      exit 1
    fi

    LOCAL="$RAIZ/respaldos"
    mkdir -p "$LOCAL"
    REMOTA="$($SSH_R 'ls -1dt /opt/cacha-el-precio/respaldos/*/ 2>/dev/null | head -1' 2>/dev/null)"
    if [[ -z "$REMOTA" ]]; then
      rojo "  El respaldo no dejo ninguna carpeta. NO borro nada."
      exit 1
    fi

    # Bajarlo al portatil es la mitad que importa: un respaldo guardado DENTRO
    # de la maquina que estas a punto de terminar no es un respaldo.
    if ! scp -i "$LLAVE_SSH" -o StrictHostKeyChecking=accept-new -q -r \
           "ec2-user@$IP_ACTUAL:$REMOTA" "$LOCAL/"; then
      rojo "  No pude bajar el respaldo al portatil. NO borro nada."
      exit 1
    fi

    BAJADA="$LOCAL/$(basename "$REMOTA")"
    if [[ ! -s "$BAJADA/database.sql" ]]; then
      rojo "  El respaldo bajo vacio ($BAJADA/database.sql). NO borro nada."
      exit 1
    fi
    verde "  respaldo a salvo en $BAJADA  ($(du -sh "$BAJADA" | cut -f1))"
    gris  "  restaurar: ./tools/respaldar.sh --restaurar $BAJADA"
  elif [[ "${2:-}" == "--sin-respaldo" ]]; then
    rojo "  --sin-respaldo: borrando SIN guardar los datos. El historial se pierde."
  fi

  if [[ -n "$ID" && "$ID" != "None" ]]; then
    aws_ ec2 terminate-instances --instance-ids "$ID" >/dev/null && verde "  instancia $ID terminada"
  else
    gris "  no habia instancia"
  fi
  ALLOC="$(aws_ ec2 describe-addresses --filters "Name=tag:Name,Values=$NOMBRE" \
           --query 'Addresses[0].AllocationId' --output text 2>/dev/null)"
  if [[ -n "$ALLOC" && "$ALLOC" != "None" ]]; then
    aws_ ec2 release-address --allocation-id "$ALLOC" >/dev/null && verde "  IP elastica liberada"
  fi
  gris "  el bucket $BUCKET NO se borra: tiene el sitio dentro"
  exit 0
fi

# ---------------------------------------------------------------------------
# 1. Grupo de seguridad
#    22 para entrar, 80 y 443 para que Caddy resuelva el certificado y sirva,
#    y 8080 para que el API Gateway pueda entrar SIN pasar por el dominio.
#
#    Por que hace falta el 8080. Caddy solo atiende el nombre
#    api.cacha-el-precio.com: si le hablas por IP responde 308 y te manda a una
#    direccion sin certificado. El registro DNS admite UNA sola IP, asi que en
#    cualquier cuenta que no sea la duena del dominio el borde no tiene por
#    donde entrar. El bloque ":8080" del Caddyfile existe justo para eso y
#    tambien pasa por el gateway, o sea que el token se sigue validando igual.
#
#    Los puertos se declaran en CADA corrida, no solo al crear. Es el mismo
#    problema que tenia el app client de Cognito: un grupo creado antes de que
#    existiera esta linea se quedaba sin el puerto nuevo para siempre, y el
#    script informaba "ya existe" tan contento. Reconciliar es barato; el
#    authorize de un puerto que ya esta da error y se ignora.
# ---------------------------------------------------------------------------
titulo "1. Grupo de seguridad"
SG_ID="$(aws_ ec2 describe-security-groups --filters "Name=group-name,Values=$SG_NOMBRE" \
         --query 'SecurityGroups[0].GroupId' --output text 2>/dev/null)"
if [[ -n "$SG_ID" && "$SG_ID" != "None" ]]; then
  verde "  ya existe: $SG_ID"
else
  SG_ID="$(aws_ ec2 create-security-group --group-name "$SG_NOMBRE" \
           --description "Cacha el Precio: SSH y web" --query GroupId --output text)"
  verde "  creado: $SG_ID"
fi

FALTABAN=""
for PUERTO in 22 80 443 8080; do
  if aws_ ec2 authorize-security-group-ingress --group-id "$SG_ID" \
       --protocol tcp --port "$PUERTO" --cidr 0.0.0.0/0 >/dev/null 2>&1; then
    FALTABAN="$FALTABAN $PUERTO"
  fi
done
if [[ -n "$FALTABAN" ]]; then
  verde "  puertos abiertos ahora:$FALTABAN"
else
  gris  "  22, 80, 443 y 8080 ya estaban abiertos"
fi

# ---------------------------------------------------------------------------
# 2. Par de llaves
#    El Learner Lab trae "vockey" hecha. Si no esta, se crea una y se guarda
#    fuera del repositorio.
# ---------------------------------------------------------------------------
titulo "2. Par de llaves"
if aws_ ec2 describe-key-pairs --key-names vockey >/dev/null 2>&1; then
  LLAVE="vockey"
  verde "  usando la del laboratorio: vockey"
elif aws_ ec2 describe-key-pairs --key-names "$NOMBRE" >/dev/null 2>&1; then
  LLAVE="$NOMBRE"
  verde "  ya existe: $NOMBRE"
else
  LLAVE="$NOMBRE"
  aws_ ec2 create-key-pair --key-name "$LLAVE" --query KeyMaterial --output text > "$RAIZ/$LLAVE.pem"
  chmod 600 "$RAIZ/$LLAVE.pem"
  verde "  creada: $LLAVE.pem (en la raiz, ignorada por git)"
fi

# ---------------------------------------------------------------------------
# 3. La instancia
#    La AMI se pide por su parametro publico en vez de escribir un id a mano:
#    los ids de AMI cambian por region y con cada version, y un id fijo es lo
#    primero que se pudre en un script como este.
#
#    El user-data deja Docker y el plugin de compose instalados y andando. No
#    despliega la aplicacion porque para eso hace falta el .env con secretos.
# ---------------------------------------------------------------------------
titulo "3. Instancia EC2"
ID="$(aws_ ec2 describe-instances \
      --filters "Name=tag:Name,Values=$NOMBRE" "Name=instance-state-name,Values=pending,running,stopped" \
      --query 'Reservations[0].Instances[0].InstanceId' --output text 2>/dev/null)"

if [[ -n "$ID" && "$ID" != "None" ]]; then
  verde "  ya existe: $ID"
else
  AMI="$(aws_ ssm get-parameters \
         --names /aws/service/ami-amazon-linux-latest/al2023-ami-kernel-default-x86_64 \
         --query 'Parameters[0].Value' --output text 2>/dev/null)"
  if [[ -z "$AMI" || "$AMI" == "None" ]]; then
    rojo "  no se pudo resolver la AMI de Amazon Linux 2023"; exit 1
  fi
  gris "  AMI: $AMI"

  # Perfil de instancia. Va al CREAR y no despues, y esa diferencia importa:
  # el agente SSM pide credenciales al arrancar y no vuelve a intentarlo, asi
  # que si se asocia mas tarde hay que reiniciar la maquina para que sirva.
  # Con el perfil puesto desde el principio se puede entrar por SSM sin llave
  # —util cuando el .pem esta en la consola del laboratorio y no en el disco—
  # y ademas la maquina puede leer Parameter Store.
  PERFIL="${PERFIL:-LabInstanceProfile}"
  ARG_PERFIL=()
  if aws_ iam get-instance-profile --instance-profile-name "$PERFIL" >/dev/null 2>&1; then
    ARG_PERFIL=(--iam-instance-profile "Name=$PERFIL")
    gris "  perfil: $PERFIL"
  else
    gris "  sin perfil de instancia: no habra SSM (se entra solo por SSH)"
  fi

  INIT="$(mktemp)"
  cat > "$INIT" <<'CLOUDINIT'
#!/bin/bash
# Deja la maquina lista para "docker compose up". No despliega nada todavia:
# eso necesita el .env con secretos, que no viaja en el repositorio.
dnf -y update
dnf -y install docker git
systemctl enable --now docker
usermod -aG docker ec2-user
mkdir -p /usr/local/lib/docker/cli-plugins
curl -sSL https://github.com/docker/compose/releases/latest/download/docker-compose-linux-x86_64 \
  -o /usr/local/lib/docker/cli-plugins/docker-compose
chmod +x /usr/local/lib/docker/cli-plugins/docker-compose
# buildx: el paquete de Docker de la AMI trae 0.12.1, y el plugin de compose
# nuevo exige 0.17 o mas para construir imagenes. Se pone una version fija en
# /usr/local/lib, que tiene precedencia sobre la de /usr/libexec.
curl -fsSL https://github.com/docker/buildx/releases/download/v0.17.1/buildx-v0.17.1.linux-amd64 \
  -o /usr/local/lib/docker/cli-plugins/docker-buildx
chmod +x /usr/local/lib/docker/cli-plugins/docker-buildx
mkdir -p /opt/cacha-el-precio && chown ec2-user:ec2-user /opt/cacha-el-precio
# El agente SSM viene en la AMI, pero pide credenciales al arrancar. Se
# reinicia al final para que tome las del perfil ya asociado.
systemctl restart amazon-ssm-agent 2>/dev/null || true
touch /var/log/cacha-listo
CLOUDINIT

  ID="$(aws_ ec2 run-instances \
        --image-id "$AMI" --instance-type "$TIPO" --key-name "$LLAVE" \
        --security-group-ids "$SG_ID" --user-data "file://$INIT" \
        "${ARG_PERFIL[@]}" \
        --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=$NOMBRE}]" \
        --query 'Instances[0].InstanceId' --output text)"
  rm -f "$INIT"
  verde "  creada: $ID ($TIPO)"
  gris  "  esperando a que arranque..."
  aws_ ec2 wait instance-running --instance-ids "$ID" 2>/dev/null
fi

# ---------------------------------------------------------------------------
# 4. Elastic IP
#    Sin esto la direccion cambia cada vez que el laboratorio reinicia la
#    maquina, y el DNS de Cloudflare queda apuntando a una direccion muerta:
#    el sitio sigue abriendo (Cloudflare lo tiene cacheado) pero la API no
#    responde, que es el fallo mas confuso de diagnosticar.
# ---------------------------------------------------------------------------
titulo "4. Elastic IP"
ALLOC="$(aws_ ec2 describe-addresses --filters "Name=tag:Name,Values=$NOMBRE" \
         --query 'Addresses[0].AllocationId' --output text 2>/dev/null)"
if [[ -z "$ALLOC" || "$ALLOC" == "None" ]]; then
  ALLOC="$(aws_ ec2 allocate-address --domain vpc \
           --tag-specifications "ResourceType=elastic-ip,Tags=[{Key=Name,Value=$NOMBRE}]" \
           --query AllocationId --output text)"
  verde "  reservada: $ALLOC"
else
  verde "  ya existe: $ALLOC"
fi
aws_ ec2 associate-address --instance-id "$ID" --allocation-id "$ALLOC" >/dev/null 2>&1 \
  && verde "  asociada a $ID" || gris "  ya estaba asociada"
IP="$(aws_ ec2 describe-addresses --allocation-ids "$ALLOC" --query 'Addresses[0].PublicIp' --output text)"

# ---------------------------------------------------------------------------
# 5. Bucket del sitio
#    Privado. Cloudflare va delante y es quien pone el HTTPS y el cacheado.
#    (El README de frontend/deploy hablaba de CloudFront: en estas cuentas
#    CloudFront esta bloqueado, comprobado con credenciales reales.)
# ---------------------------------------------------------------------------
titulo "5. Buckets"
if aws_ s3api head-bucket --bucket "$BUCKET" >/dev/null 2>&1; then
  verde "  ya existe: $BUCKET"
else
  if [[ "$REGION" == "us-east-1" ]]; then
    aws_ s3api create-bucket --bucket "$BUCKET" >/dev/null
  else
    aws_ s3api create-bucket --bucket "$BUCKET" \
      --create-bucket-configuration "LocationConstraint=$REGION" >/dev/null
  fi
  verde "  creado: $BUCKET"
fi

# El de respaldos NO lleva sitio web ni politica publica: nace privado y se
# queda privado. Aqui van los volcados de la base, que sobreviven a que la
# instancia se termine, se caiga o el laboratorio caduque.
if aws_ s3api head-bucket --bucket "$BUCKET_RESPALDOS" >/dev/null 2>&1; then
  verde "  ya existe: $BUCKET_RESPALDOS (privado)"
else
  if [[ "$REGION" == "us-east-1" ]]; then
    aws_ s3api create-bucket --bucket "$BUCKET_RESPALDOS" >/dev/null
  else
    aws_ s3api create-bucket --bucket "$BUCKET_RESPALDOS" \
      --create-bucket-configuration "LocationConstraint=$REGION" >/dev/null
  fi
  aws_ s3api put-public-access-block --bucket "$BUCKET_RESPALDOS" \
    --public-access-block-configuration \
    "BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true" >/dev/null 2>&1
  verde "  creado: $BUCKET_RESPALDOS (privado, con el acceso publico bloqueado)"
fi

# Versionado: cada volcado nuevo no pisa al anterior, guarda una version.
# Protege del caso feo de verdad: un respaldo que sale corrupto o vacio y
# sobrescribe al ultimo bueno. Sin esto, el respaldo automatico puede acabar
# destruyendo justo lo que venia a salvar. Se declara en cada corrida porque
# es idempotente y cuesta una llamada.
aws_ s3api put-bucket-versioning --bucket "$BUCKET_RESPALDOS" \
  --versioning-configuration Status=Enabled >/dev/null 2>&1 \
  && gris "  versionado activo en $BUCKET_RESPALDOS"

# ---------------------------------------------------------------------------
# 6. Archivo de configuracion
# ---------------------------------------------------------------------------
titulo "6. Archivo de configuracion"
cat > "$SALIDA" <<ENV
# Generado por tools/crear-infra.sh el $(date -Is)
# Cuenta $CUENTA, region $REGION. NO se sube al repositorio.
INFRA_CUENTA=$CUENTA
INFRA_REGION=$REGION
INFRA_INSTANCIA=$ID
INFRA_IP=$IP
INFRA_ALLOCATION=$ALLOC
INFRA_SG=$SG_ID
INFRA_LLAVE=$LLAVE
INFRA_BUCKET=$BUCKET
INFRA_BUCKET_RESPALDOS=$BUCKET_RESPALDOS
ENV
verde "  escrito: $SALIDA"

# ---------------------------------------------------------------------------
titulo "Listo"
echo "  Instancia   $ID"
echo "  IP fija     $IP"
echo "  Bucket      $BUCKET"
echo ""
gris "Lo que falta, y no lo hace este script:"
echo "  1. En Cloudflare, apuntar el registro A de 'api' a $IP"
echo "  2. Copiar el .env a la maquina y levantar el compose:"
echo "       scp -i <llave>.pem .env ec2-user@$IP:/opt/cacha-el-precio/"
echo "       ssh -i <llave>.pem ec2-user@$IP"
echo "       cd /opt/cacha-el-precio && git clone <repo> . && docker compose up -d"
echo "  3. Compilar el frontend con los datos de esta cuenta y subirlo:"
echo "       npm run build && aws s3 sync dist/ s3://$BUCKET/ --delete"
echo "  4. En Cloudflare, apuntar 'www' a este bucket"
echo ""
gris "Los otros dos scripts van despues: crear-cognito.sh y crear-api-gateway.sh"
