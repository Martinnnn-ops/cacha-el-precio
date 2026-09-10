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
#   · Una EC2 con Docker y el plugin de compose ya instalados
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
#   ./crear-infra.sh --borrar     # borra la maquina y libera la IP
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
TIPO="${TIPO:-t3.micro}"
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

# ---------------------------------------------------------------------------
# --borrar: soltar la maquina y la IP. El bucket NO se borra solo: tiene el
# sitio dentro y borrarlo por accidente es caro de deshacer.
# ---------------------------------------------------------------------------
if [[ "${1:-}" == "--borrar" ]]; then
  titulo "Borrando"
  ID="$(aws_ ec2 describe-instances \
        --filters "Name=tag:Name,Values=$NOMBRE" "Name=instance-state-name,Values=pending,running,stopped,stopping" \
        --query 'Reservations[0].Instances[0].InstanceId' --output text 2>/dev/null)"
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
#    22 para entrar, 80 y 443 para que Caddy resuelva el certificado y sirva.
#    Cuando el borde este delante, el 80/443 se puede cerrar a todo lo que no
#    venga del API Gateway; hoy sigue abierto porque el trafico entra directo.
# ---------------------------------------------------------------------------
titulo "1. Grupo de seguridad"
SG_ID="$(aws_ ec2 describe-security-groups --filters "Name=group-name,Values=$SG_NOMBRE" \
         --query 'SecurityGroups[0].GroupId' --output text 2>/dev/null)"
if [[ -n "$SG_ID" && "$SG_ID" != "None" ]]; then
  verde "  ya existe: $SG_ID"
else
  SG_ID="$(aws_ ec2 create-security-group --group-name "$SG_NOMBRE" \
           --description "Cacha el Precio: SSH y web" --query GroupId --output text)"
  for PUERTO in 22 80 443; do
    aws_ ec2 authorize-security-group-ingress --group-id "$SG_ID" \
      --protocol tcp --port "$PUERTO" --cidr 0.0.0.0/0 >/dev/null 2>&1
  done
  verde "  creado: $SG_ID (22, 80, 443)"
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
mkdir -p /opt/cacha-el-precio && chown ec2-user:ec2-user /opt/cacha-el-precio
touch /var/log/cacha-listo
CLOUDINIT

  ID="$(aws_ ec2 run-instances \
        --image-id "$AMI" --instance-type "$TIPO" --key-name "$LLAVE" \
        --security-group-ids "$SG_ID" --user-data "file://$INIT" \
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
titulo "5. Bucket del sitio"
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
