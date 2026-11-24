# Contexto: cómo está todo hoy

> **Qué es.** El estado actual del proyecto en una página: qué corre, qué está decidido, qué está
> abierto y dónde leer cada cosa. Es lo primero que lee quien llega nuevo, persona o IA.
> **Para quién.** Quien empieza una sesión de trabajo, y los agentes de IA (lo pide `AGENTS.md`).
> **Última revisión:** 23-11-2026. **Se actualiza en el mismo PR** que cambia algo de lo que dice.

> 📍 **En 10 segundos:** Cacha el Precio compara precios de ropa y calzado entre 6 tiendas
> chilenas. Ya pasaron el EP1, el EP2 y el EP3; **lo que sigue es la EFT, el miércoles 2-dic a las
> 17:00**. La infraestructura de AWS está entera en **Terraform** (`tools/tf.sh`).
> Desde el 19-11 hay **CI/CD**: solo `main` despliega.
> La EC2 **no está prendida 24/7**: que la API no responda es lo normal fuera de
> las sesiones de trabajo.

**Leyenda:** ✅ funciona · 🟡 a medias · ⬜ falta · 🔎 por confirmar · 🚫 no se hace

---

## Qué corre

| Pieza | Estado | Dónde mirar |
|---|---|---|
| Sitio `www.cacha-el-precio.com` | ✅ Worker de Cloudflare (cuenta de Panditax); su repo copia `frontend/` del monorepo y Cloudflare publica solo | [CICD](CICD.md#cd-del-frontend-el-repo-de-panditax-y-cloudflare) |
| API | ✅ API Gateway → Caddy → gateway, solo con la EC2 prendida. El sitio usa el de la cuenta `731705830984` desde el 19-11 | [ARQUITECTURA](ARQUITECTURA.md) |
| Login con Google y correo | ✅ Cognito, un pool por cuenta | [ARQUITECTURA §6](ARQUITECTURA.md#6--seguridad-e-identidad) |
| Catálogo, armario y avisos por correo | ✅ | [EL-PROYECTO](EL-PROYECTO.md) |
| Mensajería SQS + SNS + DLQ | ✅ dos flujos; la cola de auditoría **nadie la lee** | [MENSAJERIA](MENSAJERIA.md) |
| Panel de mensajería | ✅ **solo mira** desde el 17-11 | [ADR-032](adr/032-administracion-de-mensajeria.md) |
| Scraper | ✅ 6 tiendas en vivo, 2 con parser listo; **se lanza a mano** | [scraper-service](../scraper-service/README.md) |
| Infraestructura | ✅ Terraform, fases 0 a 7; `tools/tf.sh subir` y `apagar` | [OPERAR-EN-AWS](OPERAR-EN-AWS.md) |
| CI/CD | ✅ CI en cada PR; CD del backend (GHCR + SSH + *rollback*) y aviso al front, **solo desde `main`** | [CICD](CICD.md) · [ADR-034](adr/034-cicd-con-github-actions.md) |
| correlationId, Kinesis, Grafana | ⬜ | [PLAN-EFT](PLAN-EFT.md) |

**Las cuentas de AWS:** el sitio publicado usa la cuenta `731705830984` desde el 19-11, aunque su
`.tfvars` todavía dice `tiene_dominio = false` (ver la deuda abierta). Las otras dos lo montan sin dominio. Terraform se probó desde cero en una cuenta vacía el 17-11.

---

## Las ramas

| Rama | Qué tiene |
|---|---|
| `main` | lo publicado. Protegida; nada entra directo |
| `development` | la rama de pruebas; ahí llegan todos los PR, desde una rama `feature/*` por tarea |

> ⚠️ **El front publicado sale del repo de Panditax**, que copia `frontend/` del monorepo sola
> ([CICD](CICD.md#cd-del-frontend-el-repo-de-panditax-y-cloudflare)). Lo que se cambia en ese repo
> a mano se pierde en la siguiente copia: el front se edita **aquí**.

---

## Decisiones vigentes que más se olvidan

| Decisión | Desde | Dónde |
|---|---|---|
| **Kafka descartado**: la EFT va solo con **Kinesis** | 17-11 | [PLAN-EFT](PLAN-EFT.md) |
| La EC2 se queda en **`t3.small`** | 17-11 | [PLAN-EFT §8](PLAN-EFT.md#8--la-ram-y-el-monitoreo) |
| El CD despliega **solo desde `main`**; el front también | 19-11 | [PLAN-EFT §2](PLAN-EFT.md#2--cicd-con-github-actions) |
| Colas, tópicos y reglas: **solo Terraform**; el panel solo mira | 17-11 | [ADR-032](adr/032-administracion-de-mensajeria.md) |
| 🅐 o 🅑 lo decide **`tiene_dominio`** en el `.tfvars`, no el DNS | 17-11 | [OPERAR-EN-AWS §0](OPERAR-EN-AWS.md#0--lo-primero-el-dominio) |
| Los admins se dan con **`tools/hacer-admin.sh`**; `ADMINS=` ya no hace nada | 17-11 | [OPERAR-EN-AWS §4](OPERAR-EN-AWS.md#el-panel-de-mensajería) |
| **No hay red privada ni RDS**: EC2 pública con encabezado del borde | 10-09 | [ARQUITECTURA §7](ARQUITECTURA.md#7--despliegue-lo-que-hay-y-por-qué-difiere-del-diseño) |
| No entra ropa infantil | 23-09 | [ADR-028](adr/028-armario-personal-y-politica-catalogo.md) |
| Las tareas **no tienen dueño**: el que puede, la toma | 18-11 | [TAREAS](TAREAS.md) |

---

## Deuda abierta

Lo que el código y los documentos saben que falta, fuera de las tareas nuevas de la EFT. Al
cerrarlo, se borra de aquí y se anota en la [bitácora](BITACORA.md).

| | Qué | Por qué importa |
|---|---|---|
| 🟠 | El panel **ya no crea ni borra** colas, y la pauta del EP3 pedía «crear, modificar y eliminar» | hay que poder explicarlo en la defensa: lo hace Terraform, con plan antes de aplicar |
| 🟡 | Los métodos de crear y borrar de `ServicioAdministracion` y `ValidadorSolicitudes` quedaron **sin ruta** | código muerto: se borra o se justifica para el modo memoria |
| 🟡 | `tools/respaldar.sh` **no vuelca el esquema `notification`** | quien sigue una prenda la deja de seguir al migrar de cuenta |
| 🟡 | El front publicado (repo de Panditax) **todavía muestra «Administrar»** | esos botones reciben 404/405; falta el `subtree` |
| 🟡 | La cola `cep-auditoria-precios` no tiene lector | SQS borra sus mensajes a los 4 días |
| 🟡 | El scraper **no corre solo** y guarda **un precio**, no tres | el historial se llena poco y la ficha no muestra el tachado |
| 🟡 | Secretos en `.env` en la EC2, sin gestor de secretos | calidad; no entra en la EFT |
| 🟠 | **Puerto 22 abierto a todo internet** para el CD (solo con llave) | en una cuenta real sería OIDC + SSM; hay que saber defenderlo ([ADR-034](adr/034-cicd-con-github-actions.md)) |
| 🟡 | El repo del frontend copia desde `development` y no revisa el bundle | lo de `development` sale a `www`; un build sin Cognito se publica igual ([CICD](CICD.md#pendiente)) |
| 🟡 | La CSP de `_headers` está en *Report-Only* | falta revisar la consola y pasarla a modo real |
| 🟢 | El token de Cloudflare no tiene vencimiento | rotarlo después del 2-dic |

> **Regla:** cuando el código contradiga un documento, no se arregla en silencio. Se anota aquí o
> se escribe el ADR que falta.

---

## Qué leer según la tarea

| Si vas a… | Lee |
|---|---|
| Tomar una tarea | [TAREAS](TAREAS.md) y la sección de [PLAN-EFT](PLAN-EFT.md) que la explica |
| Prender o apagar el sistema, o cambiar de cuenta | [OPERAR-EN-AWS](OPERAR-EN-AWS.md) |
| Programar en local o preparar un PR | [DESARROLLO](DESARROLLO.md) |
| Entender cómo encaja todo | [ARQUITECTURA](ARQUITECTURA.md) |
| Tocar una cola o un evento | [MENSAJERIA](MENSAJERIA.md) |
| Tocar un servicio | su README: [gateway](../gateway/README.md) · [product-service](../product-service/README.md) · [notification-service](../notification-service/README.md) · [scraper-service](../scraper-service/README.md) · [frontend](../frontend/README.md) |
| Tocar Terraform | [`infra/terraform/README.md`](../infra/terraform/README.md) |
| Saber por qué algo es así | el [índice de ADR](adr/README.md) |
| Preparar una presentación | [EVALUACIONES](EVALUACIONES.md) y [EL-PROYECTO](EL-PROYECTO.md) |
| Saber qué pasó un día | [BITACORA](BITACORA.md) |

[HISTORIA](HISTORIA.md) resume los documentos viejos y lo que cambió. **No se mantiene**: léelo
como historia, no como la verdad de hoy.
