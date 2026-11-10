# Terraform · la infraestructura de AWS como código

> **Estado (10-11-2026): fase 0.** Están la estructura, las versiones y las variables, pero
> **todavía no crea nada**. El sistema se sigue montando con los scripts de `tools/`, como
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
└── dominio/                      (fase 6) solo quien administra Cloudflare
```

## Fases

| Fase | Qué | Estado |
|---|---|---|
| 0 | Estructura, versiones, variables, `.gitignore`, ADR-033 | ✅ |
| 0.5 | Sondeo: un recurso de cada tipo, para ver qué permisos niega el Learner Lab | ⬜ |
| 1 | Colas, DLQ y SNS desde `topologia.json` | ⬜ |
| 2 | Grupo de seguridad, EC2 (disco de 20 GB), IP y buckets | ⬜ |
| 3 | Cognito: pool, clientes, scope, grupos, usuarios de prueba, Google condicional | ⬜ |
| 4 | API, rutas, autorizador JWT, `/ingesta` y secreto del borde | ⬜ |
| 5 | Los 5 `.env` y `tools/tf.sh` (`subir`, `apagar` con respaldo, `estado`) | ⬜ |
| 6 | `dominio/` con el proveedor de Cloudflare | ⬜ |
| 7 | Montar en dos cuentas desde cero y retirar los scripts (en un PR aparte) | ⬜ |

## Reglas

- **El estado (`terraform.tfstate`) nunca va a git**: guarda el secreto del borde y el del cliente
  del scraper en texto plano. Es uno por cuenta, vive en el laptop de quien la maneja y se respalda
  en `~/cacha-cuentas/<cuenta>/estado/`.
- **`.terraform.lock.hcl` sí va a git**: fija la versión exacta del proveedor para los tres.
- Cada fase se prueba con `apply`, `destroy` y otra vez `apply` antes de darla por buena.
- Las credenciales del Learner Lab vencen cada ~4 horas. Si un `apply` se corta con
  `ExpiredToken`, se pegan las nuevas y se vuelve a correr: retoma lo que faltó.
