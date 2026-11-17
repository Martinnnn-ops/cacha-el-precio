# Versiones y proveedor de la raiz dominio/ (fase 6).
#
# Esta raiz es aparte de cuenta/ a proposito: el dominio es lo unico que NO se
# repite en las tres cuentas. Si viviera dentro de cuenta/, cualquiera que hiciera
# apply en su cuenta podria mover api.cacha-el-precio.com sin querer.
#
# El proveedor lee el token de la variable de entorno CLOUDFLARE_API_TOKEN, que
# sale de ~/cacha-cuentas/cloudflare.env (fuera del repo). El token solo tiene
# permiso "DNS: Edit" sobre la zona cacha-el-precio.com.

terraform {
  required_version = ">= 1.15.0, < 2.0.0"

  required_providers {
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 5.0"
    }
  }
}

provider "cloudflare" {}
