# Identidad (fase 3): user pool de Cognito, dominio del Hosted UI, scope propio,
# los tres clientes, los grupos, los usuarios de prueba y Google (solo en la
# cuenta con dominio). Reemplaza a tools/crear-cognito.sh.
#
# ⚠️ Reemplazar el pool cambia TODOS los id (cliente del frontend, emisor de los
# tokens) y borra a los usuarios, incluidos los de Google. Si un plan dice que
# aws_cognito_user_pool.pool "must be replaced", NO aplicar.

locals {
  # Identificador logico de nuestra API: no tiene que existir como URL.
  identificador_api = "https://api.cachaelprecio.cl"
  scope_ingesta     = "${local.identificador_api}/ingesta"

  con_google = var.tiene_dominio && var.google_client_id != "" && nonsensitive(var.google_client_secret != "")

  usuarios_prueba = {
    "admin@cachaelprecio.cl"   = "admin"
    "usuario@cachaelprecio.cl" = "usuario"
  }
}

# ── 1. User pool ──────────────────────────────────────────────────────────────
resource "aws_cognito_user_pool" "pool" {
  name = local.nombre

  # La gente entra con su correo. OJO: esto no se puede cambiar despues.
  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]
  mfa_configuration        = "OFF"

  # En la cuenta con dominio viven los usuarios reales de Google: ahi el pool no
  # se puede borrar sin quitar antes esta proteccion a mano.
  deletion_protection = var.tiene_dominio ? "ACTIVE" : "INACTIVE"

  password_policy {
    minimum_length                   = 8
    require_uppercase                = true
    require_lowercase                = true
    require_numbers                  = true
    require_symbols                  = false
    temporary_password_validity_days = 7
  }

  account_recovery_setting {
    recovery_mechanism {
      name     = "verified_email"
      priority = 1
    }
  }

  schema {
    name                = "email"
    attribute_data_type = "String"
    required            = true
    mutable             = true

    string_attribute_constraints {
      min_length = "0"
      max_length = "2048"
    }
  }
}

# ── 2. Dominio del Hosted UI ──────────────────────────────────────────────────
# Lleva el numero de cuenta, asi se sabe de antemano la URL de retorno de Google
# de cada cuenta y se declaran todas en Google Cloud Console de una vez.
resource "aws_cognito_user_pool_domain" "dominio" {
  domain       = "${local.nombre}-${local.cuenta}"
  user_pool_id = aws_cognito_user_pool.pool.id
}

# ── 3. Scope propio: lo que el scraper puede HACER ────────────────────────────
resource "aws_cognito_resource_server" "api" {
  identifier   = local.identificador_api
  name         = "api"
  user_pool_id = aws_cognito_user_pool.pool.id

  scope {
    scope_name        = "ingesta"
    scope_description = "publicar ofertas capturadas"
  }
}

# ── 4. Google (solo en la cuenta con dominio) ─────────────────────────────────
resource "aws_cognito_identity_provider" "google" {
  count = local.con_google ? 1 : 0

  user_pool_id  = aws_cognito_user_pool.pool.id
  provider_name = "Google"
  provider_type = "Google"

  provider_details = {
    client_id        = var.google_client_id
    client_secret    = var.google_client_secret
    authorize_scopes = "openid email profile"

    # Cognito agrega solo estos 6 al crear el proveedor de Google. Si no se
    # declaran, cada plan quiere borrarlos ("1 to change" eterno, visto el
    # 23-11) y AWS los vuelve a poner. Son los valores fijos de Google.
    attributes_url                = "https://people.googleapis.com/v1/people/me?personFields="
    attributes_url_add_attributes = "true"
    authorize_url                 = "https://accounts.google.com/o/oauth2/v2/auth"
    oidc_issuer                   = "https://accounts.google.com"
    token_request_method          = "POST"
    token_url                     = "https://www.googleapis.com/oauth2/v4/token"
  }

  attribute_mapping = {
    email    = "email"
    username = "sub"
  }
}

# ── 5. Cliente del frontend ───────────────────────────────────────────────────
# SIN secreto: el codigo del frontend lo ve cualquiera, por eso es code + PKCE.
# Lleva todas las direcciones de retorno de una vez, las locales y las del sitio.
resource "aws_cognito_user_pool_client" "frontend" {
  name         = "frontend"
  user_pool_id = aws_cognito_user_pool.pool.id

  generate_secret                      = false
  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_flows                  = ["code"]
  allowed_oauth_scopes                 = ["email", "openid", "profile"]
  explicit_auth_flows                  = ["ALLOW_REFRESH_TOKEN_AUTH", "ALLOW_USER_SRP_AUTH"]
  prevent_user_existence_errors        = "ENABLED"
  supported_identity_providers         = concat(["COGNITO"], local.con_google ? ["Google"] : [])

  callback_urls = [
    "http://localhost:3000/callback",
    "http://localhost:5173/auth/google",
    "http://localhost:5173/callback",
    "${var.sitio_publico}/auth/google",
    "${var.sitio_publico}/callback",
  ]
  logout_urls = [
    "http://localhost:3000",
    "http://localhost:5173",
    var.sitio_publico,
  ]

  # El cliente no puede nombrar a Google antes de que exista.
  depends_on = [aws_cognito_identity_provider.google]

  lifecycle {
    # Al importar, Cognito no informa si el cliente tiene secreto, y el proveedor
    # recrearia el cliente (con id nuevo). Se aplica igual al crear.
    ignore_changes = [generate_secret]
  }
}

# ── 6. Cliente del scraper (maquina, no persona) ──────────────────────────────
# client_credentials con secreto, porque corre en un servidor.
resource "aws_cognito_user_pool_client" "scraper" {
  name         = "scraper"
  user_pool_id = aws_cognito_user_pool.pool.id

  generate_secret                      = true
  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_flows                  = ["client_credentials"]
  allowed_oauth_scopes                 = [local.scope_ingesta]
  supported_identity_providers         = ["COGNITO"]

  depends_on = [aws_cognito_resource_server.api]

  lifecycle {
    # Al importar, Cognito no informa si el cliente tiene secreto, y el proveedor
    # recrearia el cliente (con id nuevo). Se aplica igual al crear.
    ignore_changes = [generate_secret]
  }
}

# ── 7. Cliente de pruebas ─────────────────────────────────────────────────────
# Solo usuario y clave, sin OAuth: el SRP del frontend no se puede hacer desde
# la CLI, y los tests necesitan sacar tokens sin navegador.
resource "aws_cognito_user_pool_client" "pruebas" {
  name         = "pruebas"
  user_pool_id = aws_cognito_user_pool.pool.id

  generate_secret     = false
  explicit_auth_flows = ["ALLOW_ADMIN_USER_PASSWORD_AUTH", "ALLOW_REFRESH_TOKEN_AUTH"]

  access_token_validity = 1
  token_validity_units {
    access_token = "hours"
  }

  lifecycle {
    # Al importar, Cognito no informa si el cliente tiene secreto, y el proveedor
    # recrearia el cliente (con id nuevo). Se aplica igual al crear.
    ignore_changes = [generate_secret]
  }
}

# ── 8. Grupos ─────────────────────────────────────────────────────────────────
# El API Gateway y el panel leen cognito:groups del token para decidir el 403.
resource "aws_cognito_user_group" "grupo" {
  for_each = toset(["admin", "usuario"])

  name         = each.key
  description  = "grupo ${each.key}"
  user_pool_id = aws_cognito_user_pool.pool.id
}

# ── 9. Usuarios de prueba ─────────────────────────────────────────────────────
# Uno por grupo, para mostrar 200 y 403. La clave queda permanente: sin eso el
# usuario queda en FORCE_CHANGE_PASSWORD y no puede entrar.
resource "aws_cognito_user" "prueba" {
  for_each = local.usuarios_prueba

  user_pool_id   = aws_cognito_user_pool.pool.id
  username       = each.key
  password       = var.clave_prueba
  message_action = "SUPPRESS"

  attributes = {
    email          = each.key
    email_verified = "true"
  }

  lifecycle {
    # Con username_attributes = email, Cognito guarda el usuario con un id
    # interno y no con el correo. Sin esto, cada plan querria recrearlo.
    ignore_changes = [username]
  }
}

resource "aws_cognito_user_in_group" "prueba" {
  for_each = local.usuarios_prueba

  user_pool_id = aws_cognito_user_pool.pool.id
  group_name   = aws_cognito_user_group.grupo[each.value].name
  username     = aws_cognito_user.prueba[each.key].sub
}

# ── 10. Lo que va a cognito.env ───────────────────────────────────────────────
locals {
  emisor = "https://cognito-idp.us-east-1.amazonaws.com/${aws_cognito_user_pool.pool.id}"
}

output "cognito_env" {
  description = "Variables de cognito.env, con los mismos nombres que dejaba tools/crear-cognito.sh."
  sensitive   = true
  value = {
    COGNITO_REGION                = "us-east-1"
    COGNITO_USER_POOL_ID          = aws_cognito_user_pool.pool.id
    COGNITO_DOMINIO               = aws_cognito_user_pool_domain.dominio.domain
    COGNITO_URL_LOGIN             = "https://${aws_cognito_user_pool_domain.dominio.domain}.auth.us-east-1.amazoncognito.com"
    COGNITO_URL_RETORNO_GOOGLE    = "https://${aws_cognito_user_pool_domain.dominio.domain}.auth.us-east-1.amazoncognito.com/oauth2/idpresponse"
    VITE_COGNITO_CLIENT_ID        = aws_cognito_user_pool_client.frontend.id
    VITE_COGNITO_AUTHORITY        = local.emisor
    VITE_COGNITO_REDIRECT_URI     = "http://localhost:5173/callback"
    COGNITO_ISSUER                = local.emisor
    COGNITO_JWKS_URI              = "${local.emisor}/.well-known/jwks.json"
    COGNITO_AUDIENCE              = aws_cognito_user_pool_client.frontend.id
    COGNITO_CLIENT_IDS_VALIDOS    = join(",", [aws_cognito_user_pool_client.frontend.id, aws_cognito_user_pool_client.pruebas.id, aws_cognito_user_pool_client.scraper.id])
    COGNITO_SCRAPER_CLIENT_ID     = aws_cognito_user_pool_client.scraper.id
    COGNITO_SCRAPER_CLIENT_SECRET = aws_cognito_user_pool_client.scraper.client_secret
    COGNITO_SCRAPER_SCOPE         = local.scope_ingesta
    COGNITO_PRUEBAS_CLIENT_ID     = aws_cognito_user_pool_client.pruebas.id
    COGNITO_USUARIO_ADMIN         = "admin@cachaelprecio.cl"
    COGNITO_USUARIO_NORMAL        = "usuario@cachaelprecio.cl"
    COGNITO_CLAVE_PRUEBA          = var.clave_prueba
  }
}
