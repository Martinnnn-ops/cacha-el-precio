# Lo poco que cambia entre una cuenta y otra. Los valores de cada persona van en
# cuentas/<nombre>.tfvars (fuera de git); el modelo es cuentas/ejemplo.tfvars.example.

variable "cuenta_esperada" {
  description = "Numero de la cuenta de AWS donde se monta. Sale de `aws sts get-caller-identity`."
  type        = string

  validation {
    condition     = can(regex("^[0-9]{12}$", var.cuenta_esperada))
    error_message = "cuenta_esperada debe ser el numero de cuenta de AWS: 12 digitos."
  }
}

variable "tiene_dominio" {
  description = "true solo en la cuenta a la que apunta cacha-el-precio.com: activa Google, las URLs de www y la proteccion del user pool."
  type        = bool
  default     = false
}

variable "encendido" {
  description = "false baja solo lo que cobra por hora (la EC2 y su IP). Cognito, la API, las colas y los buckets quedan vivos."
  type        = bool
  default     = true
}

variable "llave_ssh" {
  description = "Par de llaves de la EC2. En AWS Academy es vockey (la llave es labsuser.pem)."
  type        = string
  default     = "vockey"
}

variable "tipo_instancia" {
  description = "Tipo de la EC2. t3.small porque en t3.micro los 5 contenedores dejaban 225 MB libres (medido el 10-09-2026)."
  type        = string
  default     = "t3.small"
}

variable "disco_gb" {
  description = "Disco raiz de la EC2, en GB. Con los 8 GB de la AMI el disco llego al 96% al compilar las imagenes."
  type        = number
  default     = 20

  validation {
    condition     = var.disco_gb >= 20
    error_message = "Con menos de 20 GB el build de las imagenes vuelve a llenar el disco."
  }
}

variable "conservar_ip" {
  description = "true deja reservada la IP elastica aunque encendido = false (unos USD 3,6 al mes). Sirve en la cuenta con dominio, para que el registro DNS de api no quede apuntando a una IP muerta."
  type        = bool
  default     = false
}

variable "sitio_publico" {
  description = "URL del sitio publico. Va en las direcciones de retorno del cliente frontend."
  type        = string
  default     = "https://www.cacha-el-precio.com"
}

variable "google_client_id" {
  description = "Client id de Google. Solo se usa con tiene_dominio = true. Entra como TF_VAR_google_client_id (tools/tf.sh lo saca de google.env)."
  type        = string
  default     = ""
}

variable "google_client_secret" {
  description = "Client secret de Google. NUNCA en un .tfvars: entra como TF_VAR_google_client_secret."
  type        = string
  default     = ""
  sensitive   = true
}

variable "clave_prueba" {
  description = "Clave de admin@ y usuario@cachaelprecio.cl. Entra como TF_VAR_clave_prueba (tools/tf.sh la saca de cognito.env o la genera)."
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.clave_prueba) >= 8 && can(regex("[A-Z]", var.clave_prueba)) && can(regex("[a-z]", var.clave_prueba)) && can(regex("[0-9]", var.clave_prueba))
    error_message = "La clave de prueba debe cumplir la politica del pool: 8 caracteres, mayuscula, minuscula y numero."
  }
}
