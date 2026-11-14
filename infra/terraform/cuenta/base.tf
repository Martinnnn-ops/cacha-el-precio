# Base (fase 2): grupo de seguridad, EC2, IP elastica y los dos buckets.
# Reemplaza a tools/crear-infra.sh, con los mismos nombres y ajustes.
#
# ⚠️ La base de datos vive en un volumen de Docker sobre el disco raiz de la EC2.
# Si la EC2 se reemplaza, el historial de precios se pierde. Por eso:
#   · la AMI y el user-data van en ignore_changes (una AMI nueva NO recrea la maquina);
#   · bajar la EC2 (encendido = false) se hace con tools/tf.sh apagar, que respalda antes.

locals {
  nombre  = "cacha-el-precio"
  cuenta  = data.aws_caller_identity.yo.account_id
  puertos = toset(["22", "80", "443", "8080"])

  # La IP se reserva mientras la maquina este encendida, o siempre si conservar_ip.
  con_ip = var.encendido || var.conservar_ip

  bucket_web       = "${local.nombre}-web-${local.cuenta}"
  bucket_respaldos = "${local.nombre}-respaldos-${local.cuenta}"

  # default_tags no llega a lo que crea la CLI: las etiquetas del bucket van a mano.
  etiquetas_bucket = "TagSet=[{Key=proyecto,Value=cacha-el-precio},{Key=gestionado-por,Value=terraform}]"
}

data "aws_caller_identity" "yo" {}

# La AMI se pide por su parametro publico: los ids cambian por region y version.
data "aws_ssm_parameter" "ami" {
  name = "/aws/service/ami-amazon-linux-latest/al2023-ami-kernel-default-x86_64"
}

# Viene hecho en el Learner Lab. Con el perfil puesto desde el arranque funciona
# SSM sin llave y la maquina puede leer Parameter Store.
data "aws_iam_instance_profile" "lab" {
  name = "LabInstanceProfile"
}

# ── 1. Grupo de seguridad ─────────────────────────────────────────────────────
# 22 para entrar, 80 y 443 para Caddy, y 8080 para que el API Gateway entre sin
# pasar por el dominio (las cuentas sin dominio no tienen otra puerta).
resource "aws_security_group" "app" {
  name = "${local.nombre}-sg"
  # Cambiar la descripcion obliga a recrear el grupo: se deja igual a la del script.
  description = "Cacha el Precio: SSH y web"
  tags        = { Name = "${local.nombre}-sg" }
}

resource "aws_vpc_security_group_ingress_rule" "entrada" {
  for_each = local.puertos

  security_group_id = aws_security_group.app.id
  ip_protocol       = "tcp"
  from_port         = tonumber(each.key)
  to_port           = tonumber(each.key)
  cidr_ipv4         = "0.0.0.0/0"
}

resource "aws_vpc_security_group_egress_rule" "salida" {
  security_group_id = aws_security_group.app.id
  ip_protocol       = "-1"
  cidr_ipv4         = "0.0.0.0/0"
}

# ── 2. La maquina ─────────────────────────────────────────────────────────────
resource "aws_instance" "app" {
  count = var.encendido ? 1 : 0

  ami                    = data.aws_ssm_parameter.ami.value
  instance_type          = var.tipo_instancia
  key_name               = var.llave_ssh
  iam_instance_profile   = data.aws_iam_instance_profile.lab.name
  vpc_security_group_ids = [aws_security_group.app.id]

  # Deja Docker y compose listos. No despliega: eso lo hace tools/desplegar.sh.
  user_data = file("${path.module}/arranque.sh")

  # 20 GB: con los 8 de la AMI el disco llego al 96 % compilando las imagenes.
  # Agrandarlo se hace en caliente; Amazon Linux estira la particion al arrancar.
  root_block_device {
    volume_size = var.disco_gb
    volume_type = "gp3"
  }

  tags = { Name = local.nombre }

  lifecycle {
    # Una AMI nueva sale cada pocas semanas. Sin esto, el siguiente apply
    # reemplazaria la maquina y con ella la base de datos.
    ignore_changes = [ami, user_data]
  }
}

# ── 3. IP elastica ────────────────────────────────────────────────────────────
# Sin IP fija, la direccion cambia cada vez que el lab reinicia la maquina y el
# DNS de api queda apuntando a una direccion muerta.
resource "aws_eip" "app" {
  count = local.con_ip ? 1 : 0

  domain = "vpc"
  tags   = { Name = local.nombre }
}

# La asociacion va aparte para poder soltar la maquina sin soltar la IP.
resource "aws_eip_association" "app" {
  count = var.encendido ? 1 : 0

  allocation_id = aws_eip.app[0].id
  instance_id   = aws_instance.app[0].id
}

# ── 4. Buckets ────────────────────────────────────────────────────────────────
# aws_s3_bucket no sirve en Academy (una SCP niega leer el object lock, ver el
# README). El bucket lo crea la CLI y Terraform maneja sus ajustes. Los ajustes
# usan el nombre directo (no terraform_data.output) y depends_on: si no, el
# primer plan los ve "por conocer" y los reemplaza.
#
# A proposito NO hay provisioner de destroy: Terraform nunca borra un bucket. Un
# destroy solo lo olvida, y el siguiente apply lo vuelve a tomar (head-bucket).
# El de respaldos guarda el historial de precios; el del sitio, la web compilada.
resource "terraform_data" "bucket" {
  for_each = {
    web       = local.bucket_web
    respaldos = local.bucket_respaldos
  }

  input = each.value

  provisioner "local-exec" {
    command = "aws s3api head-bucket --bucket ${self.input} 2>/dev/null || aws s3api create-bucket --bucket ${self.input} --region us-east-1 >/dev/null; aws s3api put-bucket-tagging --bucket ${self.input} --tagging '${local.etiquetas_bucket}'"
  }
}

# El del sitio se sirve como pagina web de lectura publica: es la forma de ver el
# despliegue en una cuenta sin dominio. El error tambien es index.html para que
# las rutas del router de Vue (/comparador) no den 404.
resource "aws_s3_bucket_public_access_block" "web" {
  bucket                  = local.bucket_web
  depends_on              = [terraform_data.bucket]
  block_public_acls       = false
  ignore_public_acls      = false
  block_public_policy     = false
  restrict_public_buckets = false
}

resource "aws_s3_bucket_website_configuration" "web" {
  bucket     = local.bucket_web
  depends_on = [terraform_data.bucket]

  index_document {
    suffix = "index.html"
  }
  error_document {
    key = "index.html"
  }
}

resource "aws_s3_bucket_policy" "web" {
  bucket = local.bucket_web
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid       = "LecturaPublicaDelSitio"
      Effect    = "Allow"
      Principal = "*"
      Action    = "s3:GetObject"
      Resource  = "arn:aws:s3:::${local.bucket_web}/*"
    }]
  })

  # Con el bloqueo publico puesto, S3 rechaza una politica publica.
  depends_on = [aws_s3_bucket_public_access_block.web]
}

# El de respaldos nace privado y se queda privado: lleva los volcados de la base.
resource "aws_s3_bucket_public_access_block" "respaldos" {
  bucket                  = local.bucket_respaldos
  depends_on              = [terraform_data.bucket]
  block_public_acls       = true
  ignore_public_acls      = true
  block_public_policy     = true
  restrict_public_buckets = true
}

# Versionado: un volcado corrupto o vacio no pisa al ultimo bueno.
resource "aws_s3_bucket_versioning" "respaldos" {
  bucket     = local.bucket_respaldos
  depends_on = [terraform_data.bucket]

  versioning_configuration {
    status = "Enabled"
  }
}

# ── 5. Lo que va a infra.env ──────────────────────────────────────────────────
# Mismos nombres que escribia crear-infra.sh. Con la maquina apagada, la
# instancia (y la IP si no se conserva) quedan vacias.
output "infra_env" {
  description = "Variables de infra.env, con los mismos nombres que dejaba tools/crear-infra.sh."
  value = {
    INFRA_CUENTA           = local.cuenta
    INFRA_REGION           = "us-east-1"
    INFRA_INSTANCIA        = try(aws_instance.app[0].id, "")
    INFRA_IP               = try(aws_eip.app[0].public_ip, "")
    INFRA_ALLOCATION       = try(aws_eip.app[0].id, "")
    INFRA_SG               = aws_security_group.app.id
    INFRA_LLAVE            = var.llave_ssh
    INFRA_BUCKET           = local.bucket_web
    INFRA_BUCKET_RESPALDOS = local.bucket_respaldos
    INFRA_SITIO_URL        = "http://${local.bucket_web}.s3-website-us-east-1.amazonaws.com"
  }
}
