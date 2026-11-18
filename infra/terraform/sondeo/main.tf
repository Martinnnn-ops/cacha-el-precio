# Sondeo de permisos (fase 0.5).
#
# Crea UNA pieza de cada tipo de recurso que usara infra/terraform/cuenta, con los
# mismos ajustes que los scripts de tools/, para descubrir que niega el Learner Lab
# ANTES de escribir el resto. Todo lleva el prefijo cep-sondeo-, asi que no choca
# con lo que crearon los scripts.
#
#   terraform init -plugin-dir=$HOME/.terraform.d/mirror   (o terraform init)
#   terraform apply  -var cuenta_esperada=<cuenta>
#   terraform destroy -var cuenta_esperada=<cuenta>         <- siempre al terminar
#
# Cuesta centavos: una t3.micro y una IP publica durante unos minutos.

terraform {
  required_version = ">= 1.15.0, < 2.0.0"
  required_providers {
    aws = { source = "hashicorp/aws", version = "~> 6.0" }
  }
}

variable "cuenta_esperada" {
  type = string
}

provider "aws" {
  region              = "us-east-1"
  allowed_account_ids = [var.cuenta_esperada]
  default_tags {
    tags = { proyecto = "cacha-el-precio", gestionado-por = "terraform", proposito = "sondeo" }
  }
}

locals {
  p = "cep-sondeo"
}

# ── Lecturas: lo que en Academy ya existe y no se puede crear ─────────────────
data "aws_caller_identity" "yo" {}

data "aws_iam_role" "lab" {
  name = "LabRole"
}

data "aws_iam_instance_profile" "lab" {
  name = "LabInstanceProfile"
}

data "aws_ssm_parameter" "ami" {
  name = "/aws/service/ami-amazon-linux-latest/al2023-ami-kernel-default-x86_64"
}

# ── S3: bucket web publico (como crear-infra.sh) + bucket con versionado ──────
# El recurso aws_s3_bucket NO sirve en Academy: despues de crear el bucket lo lee
# entero, y una politica de la organizacion (SCP) niega con "explicit deny"
# s3:GetBucketObjectLockConfiguration. El bucket se crea con la CLI desde
# terraform_data, y sus ajustes (versionado, sitio, politica) si los maneja
# Terraform, porque esos recursos no consultan el object lock.
locals {
  cuenta           = data.aws_caller_identity.yo.account_id
  etiquetas_bucket = "TagSet=[{Key=proyecto,Value=cacha-el-precio},{Key=gestionado-por,Value=terraform},{Key=proposito,Value=sondeo}]"
}

resource "terraform_data" "bucket_web" {
  input = "${local.p}-web-${local.cuenta}"
  provisioner "local-exec" {
    command = "aws s3api head-bucket --bucket ${self.input} 2>/dev/null || aws s3api create-bucket --bucket ${self.input} --region us-east-1 && aws s3api put-bucket-tagging --bucket ${self.input} --tagging '${local.etiquetas_bucket}'"
  }
  provisioner "local-exec" {
    when    = destroy
    command = "aws s3 rb s3://${self.input} --force"
  }
}

resource "aws_s3_bucket_public_access_block" "web" {
  bucket                  = terraform_data.bucket_web.output
  block_public_acls       = true
  ignore_public_acls      = true
  block_public_policy     = false
  restrict_public_buckets = false
}

resource "aws_s3_bucket_website_configuration" "web" {
  bucket = terraform_data.bucket_web.output
  index_document {
    suffix = "index.html"
  }
}

resource "aws_s3_bucket_policy" "web" {
  bucket     = terraform_data.bucket_web.output
  depends_on = [aws_s3_bucket_public_access_block.web]
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = "*"
      Action    = "s3:GetObject"
      Resource  = "arn:aws:s3:::${terraform_data.bucket_web.output}/*"
    }]
  })
}

resource "terraform_data" "bucket_respaldos" {
  input = "${local.p}-respaldos-${local.cuenta}"
  provisioner "local-exec" {
    command = "aws s3api head-bucket --bucket ${self.input} 2>/dev/null || aws s3api create-bucket --bucket ${self.input} --region us-east-1 && aws s3api put-bucket-tagging --bucket ${self.input} --tagging '${local.etiquetas_bucket}'"
  }
  provisioner "local-exec" {
    when    = destroy
    command = "aws s3 rb s3://${self.input} --force"
  }
}

resource "aws_s3_bucket_versioning" "respaldos" {
  bucket = terraform_data.bucket_respaldos.output
  versioning_configuration {
    status = "Enabled"
  }
}

# ── SQS + SNS: cola con DLQ, politica para SNS y suscripcion con filtro ────────
resource "aws_sqs_queue" "dlq" {
  name                      = "${local.p}-dlq"
  message_retention_seconds = 1209600
  sqs_managed_sse_enabled   = true
}

resource "aws_sqs_queue" "cola" {
  name                       = "${local.p}-cola"
  visibility_timeout_seconds = 60
  receive_wait_time_seconds  = 20
  sqs_managed_sse_enabled    = true
  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dlq.arn
    maxReceiveCount     = 5
  })
}

resource "aws_sns_topic" "topico" {
  name = "${local.p}-topico"
}

resource "aws_sqs_queue_policy" "cola" {
  queue_url = aws_sqs_queue.cola.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "sns.amazonaws.com" }
      Action    = "sqs:SendMessage"
      Resource  = aws_sqs_queue.cola.arn
      Condition = { ArnEquals = { "aws:SourceArn" = aws_sns_topic.topico.arn } }
    }]
  })
}

resource "aws_sns_topic_subscription" "cola" {
  topic_arn            = aws_sns_topic.topico.arn
  protocol             = "sqs"
  endpoint             = aws_sqs_queue.cola.arn
  raw_message_delivery = true
  filter_policy        = jsonencode({ type = ["precio.bajo"] })
}

# ── Cognito: pool, dominio, scope propio, 2 clientes, grupo y usuario ─────────
resource "aws_cognito_user_pool" "pool" {
  name                     = "${local.p}-pool"
  deletion_protection      = "INACTIVE"
  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]
}

resource "aws_cognito_user_pool_domain" "dominio" {
  domain       = "${local.p}-${data.aws_caller_identity.yo.account_id}"
  user_pool_id = aws_cognito_user_pool.pool.id
}

resource "aws_cognito_resource_server" "api" {
  identifier   = "${local.p}-api"
  name         = "${local.p}-api"
  user_pool_id = aws_cognito_user_pool.pool.id
  scope {
    scope_name        = "ingesta"
    scope_description = "Publicar ofertas"
  }
}

resource "aws_cognito_user_pool_client" "spa" {
  name                                 = "${local.p}-spa"
  user_pool_id                         = aws_cognito_user_pool.pool.id
  generate_secret                      = false
  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_flows                  = ["code"]
  allowed_oauth_scopes                 = ["openid", "email", "profile"]
  callback_urls                        = ["http://localhost:5173/callback"]
  supported_identity_providers         = ["COGNITO"]
  explicit_auth_flows                  = ["ALLOW_USER_SRP_AUTH", "ALLOW_REFRESH_TOKEN_AUTH"]
}

resource "aws_cognito_user_pool_client" "maquina" {
  name                                 = "${local.p}-maquina"
  user_pool_id                         = aws_cognito_user_pool.pool.id
  generate_secret                      = true
  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_flows                  = ["client_credentials"]
  allowed_oauth_scopes                 = ["${aws_cognito_resource_server.api.identifier}/ingesta"]
}

resource "aws_cognito_user_group" "admin" {
  name         = "admin"
  user_pool_id = aws_cognito_user_pool.pool.id
}

resource "aws_cognito_user" "prueba" {
  user_pool_id   = aws_cognito_user_pool.pool.id
  username       = "sondeo@cachaelprecio.cl"
  message_action = "SUPPRESS"
  attributes     = { email = "sondeo@cachaelprecio.cl", email_verified = "true" }
}

resource "aws_cognito_user_in_group" "prueba" {
  user_pool_id = aws_cognito_user_pool.pool.id
  group_name   = aws_cognito_user_group.admin.name
  username     = aws_cognito_user.prueba.username
}

# ── EC2: grupo de seguridad, maquina con disco de 20 GB e IP fija ──────────────
resource "aws_security_group" "sg" {
  name        = "${local.p}-sg"
  description = "Sondeo de permisos de Terraform"
}

resource "aws_vpc_security_group_ingress_rule" "https" {
  security_group_id = aws_security_group.sg.id
  ip_protocol       = "tcp"
  from_port         = 443
  to_port           = 443
  cidr_ipv4         = "0.0.0.0/0"
}

resource "aws_vpc_security_group_egress_rule" "todo" {
  security_group_id = aws_security_group.sg.id
  ip_protocol       = "-1"
  cidr_ipv4         = "0.0.0.0/0"
}

resource "aws_instance" "vm" {
  ami                    = data.aws_ssm_parameter.ami.value
  instance_type          = "t3.micro"
  key_name               = "vockey"
  iam_instance_profile   = data.aws_iam_instance_profile.lab.name
  vpc_security_group_ids = [aws_security_group.sg.id]
  root_block_device {
    volume_size = 20
    volume_type = "gp3"
  }
  tags = { Name = "${local.p}-vm" }
}

resource "aws_eip" "ip" {
  domain   = "vpc"
  instance = aws_instance.vm.id
}

# ── API Gateway: HTTP API con CORS, autorizador JWT, borde y SQS con LabRole ──
resource "aws_apigatewayv2_api" "api" {
  name          = "${local.p}-api"
  protocol_type = "HTTP"
  cors_configuration {
    allow_origins = ["http://localhost:5173"]
    allow_methods = ["GET", "POST"]
    allow_headers = ["Content-Type", "Authorization", "Version"]
    max_age       = 86400
  }
}

resource "aws_apigatewayv2_authorizer" "jwt" {
  api_id           = aws_apigatewayv2_api.api.id
  name             = "${local.p}-jwt"
  authorizer_type  = "JWT"
  identity_sources = ["$request.header.Authorization"]
  jwt_configuration {
    audience = [aws_cognito_user_pool_client.spa.id, aws_cognito_user_pool_client.maquina.id]
    issuer   = "https://${aws_cognito_user_pool.pool.endpoint}"
  }
}

resource "aws_apigatewayv2_integration" "borde" {
  api_id             = aws_apigatewayv2_api.api.id
  integration_type   = "HTTP_PROXY"
  integration_method = "ANY"
  integration_uri    = "http://${aws_eip.ip.public_ip}:8080/health"
  request_parameters = { "overwrite:header.X-Borde-Secreto" = "sondeo" }
}

resource "aws_apigatewayv2_integration" "sqs" {
  api_id              = aws_apigatewayv2_api.api.id
  integration_type    = "AWS_PROXY"
  integration_subtype = "SQS-SendMessage"
  credentials_arn     = data.aws_iam_role.lab.arn
  request_parameters = {
    QueueUrl    = aws_sqs_queue.cola.url
    MessageBody = "$request.body"
  }
}

resource "aws_apigatewayv2_route" "health" {
  api_id    = aws_apigatewayv2_api.api.id
  route_key = "GET /health"
  target    = "integrations/${aws_apigatewayv2_integration.borde.id}"
}

resource "aws_apigatewayv2_route" "ingesta" {
  api_id               = aws_apigatewayv2_api.api.id
  route_key            = "POST /ingesta"
  target               = "integrations/${aws_apigatewayv2_integration.sqs.id}"
  authorization_type   = "JWT"
  authorizer_id        = aws_apigatewayv2_authorizer.jwt.id
  authorization_scopes = ["${aws_cognito_resource_server.api.identifier}/ingesta"]
}

resource "aws_apigatewayv2_stage" "prod" {
  api_id      = aws_apigatewayv2_api.api.id
  name        = "$default"
  auto_deploy = true
  default_route_settings {
    throttling_rate_limit  = 50
    throttling_burst_limit = 100
  }
}

output "api_url" {
  value = aws_apigatewayv2_stage.prod.invoke_url
}

output "cola_url" {
  value = aws_sqs_queue.cola.url
}
