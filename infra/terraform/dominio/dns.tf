# El registro A de api.cacha-el-precio.com (fase 6).
#
# Es el unico registro que maneja Terraform. Los demas de la zona (www y la raiz
# hacia el bucket S3 de Panditax, MX, SPF, DKIM, DMARC y la verificacion de
# Google) se quedan a mano en Cloudflare: no cambian cuando la API salta de cuenta.
#
# Uso (desde esta carpeta, con la IP que dejo `tools/tf.sh subir <cuenta>` en infra.env):
#   set -a; . ~/cacha-cuentas/cloudflare.env; set +a
#   terraform apply -var api_ip=<IP de la EC2 activa>

variable "zone_id" {
  description = "Zone ID de cacha-el-precio.com en Cloudflare (no es secreto: sale en Overview → API)."
  type        = string
  default     = "57e43f66822c82799f483c382b7c8f66"
}

variable "api_ip" {
  description = "IP publica de la EC2 que atiende la API. Se copia de INFRA_IP en el infra.env de la cuenta activa."
  type        = string

  validation {
    condition     = can(cidrhost("${var.api_ip}/32", 0))
    error_message = "api_ip tiene que ser una IPv4, por ejemplo 44.215.16.200."
  }
}

# El registro ya existia (se creo a mano). Este bloque lo adopta en el estado en
# vez de crear otro con el mismo nombre. Se deja a proposito: el estado de esta
# raiz es local, asi que si otra persona la corre en su laptop (estado vacio),
# vuelve a adoptar el mismo registro en vez de chocar con el.
import {
  to = cloudflare_dns_record.api
  id = "57e43f66822c82799f483c382b7c8f66/138656647c972a685b762e76521b5ee8"
}

resource "cloudflare_dns_record" "api" {
  zone_id = var.zone_id
  name    = "api.cacha-el-precio.com"
  type    = "A"
  content = var.api_ip

  # Nube gris ("DNS only"): Caddy en la EC2 saca su propio certificado y el API
  # Gateway llama directo a la maquina. Con la nube naranja, Cloudflare se pondria
  # en medio y el certificado de Caddy dejaria de renovarse.
  proxied = false

  # 1 = "Auto" en Cloudflare (300 s para registros sin proxy).
  ttl = 1
}

output "api" {
  description = "A donde apunta api.cacha-el-precio.com despues del apply."
  value       = "${cloudflare_dns_record.api.name} → ${cloudflare_dns_record.api.content}"
}
