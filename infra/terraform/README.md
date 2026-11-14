# Terraform · la infraestructura de AWS como código

> **Estado (14-11-2026): fases 0 a 4.** `cuenta/` ya maneja **toda la infraestructura de la cuenta**:
> mensajería, base (grupo de seguridad, EC2, IP y buckets), identidad (Cognito) y borde (API Gateway).
> Falta el puente (`tools/tf.sh` y los `.env`), así que por ahora conviven con los scripts, como
> explica [`docs/MONTAR-EN-TU-CUENTA.md`](../../docs/MONTAR-EN-TU-CUENTA.md). La decisión y sus
> costos están en el [ADR-033](../../docs/adr/033-terraform-para-la-infraestructura.md).

## Qué va a reemplazar

Terraform reemplaza lo que **crea** cosas en AWS. Lo que **opera** el sistema se queda.

| Script | Destino |
|---|---|
| `tools/crear-infra.sh` | lo reemplaza `cuenta/` (EC2, IP, grupo de seguridad, buckets) |
| `tools/crear-cognito.sh` | lo reemplaza `cuenta/` (user pool, clientes, scope, grupos, Google) |
| `tools/crear-api-gateway.sh` | lo reemplaza `cuenta/` (HTTP API, rutas, autorizador, `/ingesta`, secreto del borde) |
| `tools/crear-mensajeria.py` | lo reemplaza `cuenta/`, leyendo el mismo `infra/mensajeria/topologia.json` |
| `tools/desplegar.sh`, `respaldar.sh`, `hacer-admin.sh` y las pruebas | **se quedan** |

Al terminar, Terraform escribe los **5 archivos** que hoy dejan los scripts (`infra.env`,
`cognito.env`, `api-gateway.env`, `borde.env` y `mensajeria.env`), con los mismos nombres de
variable. Así `desplegar.sh` funciona sin cambios.

Los scripts no se borran hasta que Terraform monte el sistema completo en dos cuentas. 🚫 **En una
misma cuenta nunca se mezclan**: si un script crea algo que Terraform no anotó, Terraform intenta
crearlo de nuevo y choca por el nombre.

## Estructura

```text
infra/terraform/
├── cuenta/                       lo corre cada persona en SU cuenta
│   ├── versions.tf               Terraform 1.x, proveedor aws ~> 6.0, us-east-1, etiquetas
│   ├── variables.tf              cuenta_esperada, tiene_dominio, encendido, llave, EC2
│   └── cuentas/
│       └── ejemplo.tfvars.example   el único .tfvars que va a git
├── sondeo/                       prueba de permisos: crea una pieza de cada tipo y se destruye
└── dominio/                      (fase 6) solo quien administra Cloudflare
```

## Fases

| Fase | Qué | Estado |
|---|---|---|
| 0 | Estructura, versiones, variables, `.gitignore`, ADR-033 | ✅ |
| 0.5 | Sondeo: un recurso de cada tipo, para ver qué permisos niega el Learner Lab | ✅ |
| 1 | Colas, DLQ y SNS desde `topologia.json` (`cuenta/mensajeria.tf`) | ✅ |
| 2 | Grupo de seguridad, EC2 (disco de 20 GB), IP y buckets (`cuenta/base.tf`) | ✅ |
| 3 | Cognito: pool, clientes, scope, grupos, usuarios de prueba, Google condicional (`cuenta/identidad.tf`) | ✅ |
| 4 | API, rutas, autorizador JWT, `/ingesta` y secreto del borde (`cuenta/api.tf`) | ✅ |
| 5 | Los 5 `.env` y `tools/tf.sh` (`subir`, `apagar` con respaldo, `estado`) | ⬜ |
| 6 | `dominio/` con el proveedor de Cloudflare | ⬜ |
| 7 | Montar en dos cuentas desde cero y retirar los scripts (en un PR aparte) | ⬜ |

## Si `terraform init` se corta

El proveedor de AWS pesa unos 170 MB. Si la conexión se cae a mitad, `init` falla con
`unexpected EOF` y parte de cero en cada intento. Se puede bajar aparte con `curl`, que retoma, y
darle a Terraform la copia local:

```bash
D=~/.terraform.d/mirror/registry.terraform.io/hashicorp/aws
V=6.72.0          # la versión que fija .terraform.lock.hcl
P=linux_amd64     # tu plataforma: windows_amd64, darwin_arm64, ...
mkdir -p "$D" && cd "$D"
curl -fL -C - --retry 10 --retry-all-errors -O \
  https://releases.hashicorp.com/terraform-provider-aws/$V/terraform-provider-aws_${V}_${P}.zip
curl -fLO https://releases.hashicorp.com/terraform-provider-aws/$V/terraform-provider-aws_${V}_SHA256SUMS
sha256sum -c --ignore-missing terraform-provider-aws_${V}_SHA256SUMS   # tiene que decir OK
cd -  # volver a infra/terraform/cuenta
terraform init -plugin-dir=$HOME/.terraform.d/mirror
```

`.terraform.lock.hcl` trae la huella de Linux, Mac y Windows, así que la copia local se valida
igual que una bajada por `init`.

## ⚠️ S3 en AWS Academy: el bucket no se crea con `aws_s3_bucket`

Lo encontró el sondeo el 10-11-2026. Después de crear un bucket, el recurso `aws_s3_bucket` lo
lee entero, incluida su configuración de *object lock*, y una política de la organización de
Academy (SCP) niega `s3:GetBucketObjectLockConfiguration` con *explicit deny*. El bucket se crea
igual, pero Terraform lo marca como dañado y no puede seguir.

La salida, probada en `sondeo/`: el bucket lo crea la CLI desde un `terraform_data` (que también lo
borra al destruir), y Terraform maneja sus ajustes con `aws_s3_bucket_versioning`,
`aws_s3_bucket_website_configuration`, `aws_s3_bucket_policy` y
`aws_s3_bucket_public_access_block`, que no consultan el *object lock*. Como `default_tags` no
llega a lo que crea la CLI, las etiquetas se ponen con `put-bucket-tagging`.

Todo lo demás pasó sin cambios: Cognito (pool, dominio, scope, clientes, grupo y usuario), la HTTP
API con autorizador JWT, la integración `SQS-SendMessage` asumiendo `LabRole`, las colas con DLQ,
SNS con filtro, el grupo de seguridad, la EC2 con disco de 20 GB y la IP. Con el token del
cliente de máquina, `POST /ingesta` respondió 200 y el mensaje llegó a la cola, y sin token, 401.

Para sondear una cuenta nueva:

```bash
cd infra/terraform/sondeo
terraform init                     # o con -plugin-dir, ver arriba
terraform apply   -var cuenta_esperada=$(aws sts get-caller-identity --query Account --output text)
terraform destroy -var cuenta_esperada=$(aws sts get-caller-identity --query Account --output text)
```

## Si los scripts ya crearon las cosas en tu cuenta

No hace falta borrar nada: se **adoptan** con bloques `import`, sin cortar el servicio. Las URL, los
ARN y los id no cambian, así que los `.env` siguen sirviendo. Así se hizo en `731705830984` el
14-11-2026 con la mensajería (fase 1) y con la base (fase 2).

1. Crear `cuenta/importar.tf` (temporal, no va a git) con un bloque por recurso:

   ```hcl
   import {
     to = aws_sqs_queue.cola["ofertas-crudas"]     # también: dlq["..."], las otras colas
     id = "https://sqs.us-east-1.amazonaws.com/<cuenta>/cep-ofertas-crudas"
   }
   import {
     to = aws_sns_topic.topico["cambios-oferta"]
     id = "arn:aws:sns:us-east-1:<cuenta>:cep-cambios-oferta"
   }
   import {
     to = aws_sqs_queue_policy.desde_sns["notificaciones"]   # id = URL de la cola
     id = "https://sqs.us-east-1.amazonaws.com/<cuenta>/cep-notificaciones"
   }
   import {
     to = aws_sns_topic_subscription.suscripcion["cambios-oferta->notificaciones"]
     id = "<ARN de la suscripción>"
   }
   ```

   Los ARN de las suscripciones salen de
   `aws sns list-subscriptions-by-topic --topic-arn arn:aws:sns:us-east-1:<cuenta>:cep-cambios-oferta`.
2. `terraform plan -var-file=cuentas/<tu-nombre>.tfvars`. Tiene que decir **0 to add, 0 to destroy**;
   los cambios en sitio son solo las etiquetas `gestionado-por` y `proyecto`.
3. `terraform apply`, borrar `importar.tf` y correr `plan` otra vez: tiene que decir *No changes*.

Para la base, los id salen de `infra.env` (`INFRA_INSTANCIA`, `INFRA_ALLOCATION`, `INFRA_SG`), y los
de las reglas del grupo, de `aws ec2 describe-security-group-rules --filters Name=group-id,Values=<sg>`.
La asociación de la IP se importa con su `eipassoc-…` y los ajustes de cada bucket con el nombre del
bucket. Los buckets en sí no se importan: el `terraform_data` ve que ya existen y solo los etiqueta.

⚠️ **El `plan` de la base tiene que decir 0 to destroy.** Lo único que cambia en la EC2 es el disco
(8 → 20 GB, en caliente). Si dice que la instancia *must be replaced*, NO aplicar: la base de datos
vive en ese disco.

Para Cognito, los id salen de `cognito.env`. El `import` de un usuario en un grupo usa comas y el
id interno del usuario (`<pool>,<grupo>,<sub>`, el `sub` sale de `aws cognito-idp list-users`), y el
de un usuario, `<pool>/<correo>`. La clave de prueba entra como `TF_VAR_clave_prueba` (la de
`cognito.env`); sin ella el `plan` no corre.

⚠️ **El `plan` de Cognito tiene que decir 0 to destroy.** Si un cliente *must be replaced*, su id
cambia y hay que recompilar el frontend; si es el pool, se pierden los usuarios. `generate_secret`
va en `ignore_changes` porque Cognito no lo informa al importar y el proveedor recreaba los 3 clientes.

Para la API, los id salen de `aws apigatewayv2 get-integrations` y `get-routes`: cada integración
se reconoce por su URI y cada ruta por su clave (`GET /productos`). El secreto del borde entra como
`TF_VAR_borde_secreto` (el de `borde.env`): si fuera otro, Caddy respondería 403 hasta redesplegar.

⚠️ `max_message_size` va fijo en 1 MiB: es lo que trae hoy una cola nueva de SQS, pero el proveedor
asume 256 KiB (el límite antiguo) y sin el ajuste achicaría las colas.

## Reglas

- **El estado (`terraform.tfstate`) nunca va a git**: guarda el secreto del borde y el del cliente
  del scraper en texto plano. Es uno por cuenta, vive en el laptop de quien la maneja y se respalda
  en `~/cacha-cuentas/<cuenta>/estado/`.
- **`.terraform.lock.hcl` sí va a git**: fija la versión exacta del proveedor para los tres.
- Cada fase se prueba con `apply`, `destroy` y otra vez `apply` antes de darla por buena. La
  excepción es la EC2: su `destroy` borra la base, así que se prueba en la fase 5 con
  `tools/tf.sh apagar`, que respalda antes.
- **Terraform nunca borra un bucket.** Un `destroy` solo lo olvida y el siguiente `apply` lo retoma.
- Las credenciales del Learner Lab vencen cada ~4 horas. Si un `apply` se corta con
  `ExpiredToken`, se pegan las nuevas y se vuelve a correr: retoma lo que faltó.
