# Versiones y proveedor. Este archivo es lo primero que lee `terraform init`.
#
# La version de Terraform se deja abierta dentro de la 1.x: Martin tiene la 1.15.9
# (la de los repos de Arch) y el resto puede tener una mas nueva. Lo que de verdad
# fija el resultado es la version del proveedor, que queda anotada en
# .terraform.lock.hcl (ese archivo SI va a git).

terraform {
  required_version = ">= 1.15.0, < 2.0.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  # AWS Academy solo deja trabajar en us-east-1.
  region = "us-east-1"

  # Barrera contra la cuenta equivocada: si las credenciales pegadas en
  # ~/.aws/credentials son de otra cuenta, Terraform se niega antes de tocar nada.
  allowed_account_ids = [var.cuenta_esperada]

  # Todo lo que crea Terraform lleva estas etiquetas. Sirven para saber, desde la
  # consola, que un recurso es de Terraform y no de los scripts de tools/.
  default_tags {
    tags = {
      proyecto       = "cacha-el-precio"
      gestionado-por = "terraform"
    }
  }
}
