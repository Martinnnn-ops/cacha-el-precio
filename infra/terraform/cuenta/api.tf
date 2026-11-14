# Borde (fase 4): la HTTP API con CORS, el autorizador JWT, las integraciones
# hacia la EC2 con el secreto del borde, POST /ingesta directo a SQS, las rutas
# con su nivel de acceso y los stages dev y prod. Reemplaza a
# tools/crear-api-gateway.sh.

locals {
  dominio_api = "api.cacha-el-precio.com"

  # A donde va el borde. En la cuenta con dominio, al dominio (salto cifrado).
  # En las demas, directo a la IP de la maquina por el bloque :8080 del Caddyfile:
  # el token se valida igual, porque lo valida el API Gateway.
  # Sin IP (maquina apagada y sin conservar_ip) se deja el dominio para que la
  # integracion siga siendo valida; las rutas responden 503 hasta prender.
  backend_url = (
    var.backend_url != "" ? var.backend_url :
    var.tiene_dominio ? "https://${local.dominio_api}" :
    local.con_ip ? "http://${aws_eip.app[0].public_ip}:8080" :
    "https://${local.dominio_api}"
  )

  # Origenes de CORS, siempre explicitos (nunca "*"). El tercero es el sitio
  # servido desde el bucket, la unica forma de verlo sin el dominio.
  origenes = [
    var.sitio_publico,
    "http://localhost:5173",
    "http://${local.bucket_web}.s3-website-us-east-1.amazonaws.com",
  ]

  # Camino en el backend => clave de la integracion.
  integraciones = {
    health           = "/health"
    productos        = "/productos"
    producto         = "/productos/{id}"
    catalogos        = "/catalogos"
    visitas          = "/productos/{id}/visitas"
    seguimiento      = "/seguimiento"
    seguir           = "/seguimiento/{id}"
    yo               = "/api/yo"
    cuenta           = "/mi-cuenta"
    deseado          = "/mi-cuenta/deseados/{id}"
    outfit           = "/mi-cuenta/outfits/{id}"
    admin            = "/api/admin/diagnostico"
    mensajeria       = "/api/admin/mensajeria"
    mensajeria_resto = "/api/admin/mensajeria/{proxy}"
  }

  # Ruta => integracion y nivel de acceso. Esta tabla ES el 200 / 401 / 403:
  #   publica → sin token · jwt → 401 sin token · scope → 403 sin el scope ingesta
  # El catalogo es publico a proposito (ADR-007): un comparador no pide sesion
  # para ver un precio. El 403 por grupo admin lo decide el BFF, no el borde.
  # Metodo por metodo y no ANY: un ANY con autorizador atrapa el OPTIONS del
  # preflight y rompe el CORS.
  rutas = merge(
    {
      "GET /health"                     = { destino = "health", acceso = "publica" }
      "GET /productos"                  = { destino = "productos", acceso = "publica" }
      "GET /productos/{id}"             = { destino = "producto", acceso = "publica" }
      "GET /catalogos"                  = { destino = "catalogos", acceso = "publica" }
      "POST /productos/{id}/visitas"    = { destino = "visitas", acceso = "publica" }
      "GET /seguimiento"                = { destino = "seguimiento", acceso = "jwt" }
      "POST /seguimiento/{id}"          = { destino = "seguir", acceso = "jwt" }
      "DELETE /seguimiento/{id}"        = { destino = "seguir", acceso = "jwt" }
      "GET /api/yo"                     = { destino = "yo", acceso = "jwt" }
      "GET /mi-cuenta"                  = { destino = "cuenta", acceso = "jwt" }
      "PUT /mi-cuenta/deseados/{id}"    = { destino = "deseado", acceso = "jwt" }
      "DELETE /mi-cuenta/deseados/{id}" = { destino = "deseado", acceso = "jwt" }
      "PUT /mi-cuenta/outfits/{id}"     = { destino = "outfit", acceso = "jwt" }
      "DELETE /mi-cuenta/outfits/{id}"  = { destino = "outfit", acceso = "jwt" }
      "GET /api/admin/diagnostico"      = { destino = "admin", acceso = "jwt" }
      "GET /api/admin/mensajeria"       = { destino = "mensajeria", acceso = "jwt" }
      "POST /productos"                 = { destino = "productos", acceso = "scope" }
      "PUT /productos/{id}"             = { destino = "producto", acceso = "scope" }
      "DELETE /productos/{id}"          = { destino = "producto", acceso = "scope" }
      "POST /ingesta"                   = { destino = "ingesta", acceso = "scope" }
    },
    {
      for m in ["GET", "POST", "PUT", "DELETE"] :
      "${m} /api/admin/mensajeria/{proxy+}" => { destino = "mensajeria_resto", acceso = "jwt" }
    },
  )

  base_api = "https://${aws_apigatewayv2_api.api.id}.execute-api.us-east-1.amazonaws.com"
}

data "aws_iam_role" "lab" {
  name = "LabRole"
}

# ── 1. La HTTP API ────────────────────────────────────────────────────────────
# HTTP API y no REST: cuesta ~1/3 y trae el autorizador JWT de fabrica.
resource "aws_apigatewayv2_api" "api" {
  name          = local.nombre
  protocol_type = "HTTP"
  description   = "API Manager de Cacha el Precio. Creada por tools/crear-api-gateway.sh"

  cors_configuration {
    allow_origins = local.origenes
    allow_methods = ["GET", "POST", "PUT", "DELETE", "OPTIONS"]
    # API Gateway los guarda en minusculas: asi no aparece una diferencia en cada plan.
    allow_headers     = ["content-type", "authorization", "version"]
    max_age           = 86400
    allow_credentials = false
  }
}

# ── 2. Autorizador JWT ────────────────────────────────────────────────────────
# El token se rechaza en el borde, antes de llegar a la EC2. La audiencia lleva
# los TRES clientes: sin el del scraper, su token da 401 aunque traiga el scope.
resource "aws_apigatewayv2_authorizer" "cognito" {
  api_id           = aws_apigatewayv2_api.api.id
  name             = "cognito"
  authorizer_type  = "JWT"
  identity_sources = ["$request.header.Authorization"]

  jwt_configuration {
    audience = [
      aws_cognito_user_pool_client.frontend.id,
      aws_cognito_user_pool_client.pruebas.id,
      aws_cognito_user_pool_client.scraper.id,
    ]
    issuer = local.emisor
  }
}

# ── 3. Integraciones hacia la EC2 ─────────────────────────────────────────────
# El 8080 esta abierto a internet (AWS no publica un rango fijo del API Gateway).
# Por eso el gateway inyecta X-Borde-Secreto y Caddy rechaza con 403 lo que no lo
# traiga. Lo pone el gateway, no el cliente: el navegador no lo ve.
resource "aws_apigatewayv2_integration" "backend" {
  for_each = local.integraciones

  api_id                 = aws_apigatewayv2_api.api.id
  integration_type       = "HTTP_PROXY"
  integration_method     = "ANY"
  integration_uri        = "${local.backend_url}${each.value}"
  payload_format_version = "1.0"

  request_parameters = {
    "overwrite:header.X-Borde-Secreto" = var.borde_secreto
  }
}

# POST /ingesta escribe directo en la cola, asumiendo LabRole (en Academy no se
# pueden crear roles). No pasa por la EC2: si product-service esta caido, las
# ofertas esperan en la cola.
resource "aws_apigatewayv2_integration" "ingesta" {
  api_id                 = aws_apigatewayv2_api.api.id
  integration_type       = "AWS_PROXY"
  integration_subtype    = "SQS-SendMessage"
  credentials_arn        = data.aws_iam_role.lab.arn
  payload_format_version = "1.0"

  request_parameters = {
    QueueUrl    = aws_sqs_queue.cola["ofertas-crudas"].url
    MessageBody = "$request.body"
  }
}

# ── 4. Rutas ──────────────────────────────────────────────────────────────────
resource "aws_apigatewayv2_route" "ruta" {
  for_each = local.rutas

  api_id    = aws_apigatewayv2_api.api.id
  route_key = each.key
  target = "integrations/${each.value.destino == "ingesta"
    ? aws_apigatewayv2_integration.ingesta.id
  : aws_apigatewayv2_integration.backend[each.value.destino].id}"

  authorization_type   = each.value.acceso == "publica" ? "NONE" : "JWT"
  authorizer_id        = each.value.acceso == "publica" ? null : aws_apigatewayv2_authorizer.cognito.id
  authorization_scopes = each.value.acceso == "scope" ? [local.scope_ingesta] : null
}

# ── 5. Stages ─────────────────────────────────────────────────────────────────
# Con auto_deploy un cambio de ruta se publica solo. El limite (50/s, rafaga 100)
# protege el credito del laboratorio de un martilleo, no los datos.
resource "aws_apigatewayv2_stage" "etapa" {
  for_each = toset(["dev", "prod"])

  api_id      = aws_apigatewayv2_api.api.id
  name        = each.key
  description = "Stage ${each.key}"
  auto_deploy = true

  default_route_settings {
    throttling_rate_limit  = 50
    throttling_burst_limit = 100
  }
}

# ── 6. Lo que va a api-gateway.env y borde.env ────────────────────────────────
output "api_gateway_env" {
  description = "Variables de api-gateway.env, con los mismos nombres que dejaba tools/crear-api-gateway.sh."
  value = {
    API_GATEWAY_ID          = aws_apigatewayv2_api.api.id
    API_GATEWAY_URL_DEV     = "${local.base_api}/dev"
    API_GATEWAY_URL_PROD    = "${local.base_api}/prod"
    API_GATEWAY_BACKEND     = local.backend_url
    API_GATEWAY_URL_INGESTA = "${local.base_api}/prod/ingesta"
  }
}
