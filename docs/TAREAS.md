# Tareas del proyecto

> **Acá está lo que hay que hacer. Si tienes un rato libre, abre la tabla de la semana y toma
> cualquier tarea que esté 🟢 libre.**
>
> Última revisión: **10-09-2026**

---

## 🧭 Cómo funciona este documento

**No hay carriles con dueño fijo.** Hay una bolsa de tareas pendientes y cada uno avanza lo que
puede, cuando puede. Nadie espera a que le asignen algo.

**Las cuatro reglas, y son todas:**

1. **Tomas una tarea** poniendo tu nombre en la columna *Quién* de la tabla de la semana, y la
   subes de inmediato. Eso es lo que evita que dos personas hagan lo mismo.
2. **Trabajas en tu propia rama.** `feature/loquesea`. Nunca directo sobre `development`.
3. **Miras la columna *Toca* antes de empezar.** Ahí dice qué archivos se van a mover. Si otra
   tarea en curso toca los mismos, hablas con esa persona primero — eso es *no pisar el trabajo
   del otro*.
4. **Marcas `✅` cuando termina** y anotas dos líneas en [BITACORA.md](BITACORA.md). En septiembre
   esa bitácora **es el informe, ya escrito**.

**Lo que se atrasa no se borra.** Baja a [Lo que se corrió de fecha](#-lo-que-se-corrió-de-fecha),
que está ordenado por **fecha límite**, no por importancia. Una tarea sin fecha límite es una
tarea que no se va a hacer nunca.

**Otros documentos:** la idea y el alcance en [PLAN.md](PLAN.md) · el porqué técnico en
[ARQUITECTURA.md](ARQUITECTURA.md) · lo que evalúa el ramo en [EVALUACIONES.md](EVALUACIONES.md)
· los 15 registros de decisión en [`adr/`](adr/).

---

## 📋 Índice

- [La semana en curso](#-la-semana-en-curso--913-sep--cierre-del-ep1)
- [Los cuatro innegociables](#-los-cuatro-innegociables-del-ep1)
- [Lo que se califica](#-lo-que-se-califica)
- [Dónde estamos hoy](#-dónde-estamos-hoy--10-09-2026)
- [Lo que se corrió de fecha](#-lo-que-se-corrió-de-fecha)
- [v2 · después del freeze](#-v2--después-del-freeze)
- [v3 · más adelante](#-v3--más-adelante)
- [Decisiones abiertas](#-decisiones-abiertas)
- [Modo de trabajo](#️-modo-de-trabajo)
- [Checklist final](#-checklist-final)

---

## 🔥 La semana en curso · 9–13 sep · Cierre del EP1

🟢 libre · 🟡 en curso · ✅ hecho · 🔴 bloqueado

| | Tarea | Quién | Toca | Límite |
|---|---|---|---|---|
| 🔴 | **Pasar el *client secret* de Google** y declarar la URL de retorno de Cognito | Panditax | Google Cloud Console | **ya** |
| ✅ | ~~Encender el laboratorio~~ — se montó todo en la cuenta de Martín (`116813910999`) | Martín | — | 10-sep |
| ✅ | ~~Elastic IP~~ — `52.200.101.67`, la crea `crear-infra.sh` | Martín | `tools/` | 10-sep |
| ✅ | ~~En qué cuenta vive el sistema~~ — en la de Martín; los otros dos la montan sin dominio | Martín | `tools/` | 10-sep |
| ✅ | ~~El frontend llama al API Gateway~~ — verificado **dentro del bundle**, no en la config | Martín | `tools/desplegar.sh` | 10-sep |
| ✅ | ~~Caddy exige el encabezado secreto~~ — [ADR-026](adr/026-encabezado-secreto-del-borde.md); el 8080 da 403 sin él | Martín | `caddy/Caddyfile` | 10-sep |
| ✅ | ~~CORS solo en el API Manager~~ — 3 orígenes explícitos, `AllowCredentials=false` | Martín | `tools/crear-api-gateway.sh` | 10-sep |
| ✅ | ~~Declarar `POST /productos/{id}/visitas`~~ — 13 rutas en el borde | Martín | `tools/crear-api-gateway.sh` | 10-sep |
| ✅ | ~~Respaldo programado~~ — cada hora a un bucket privado con versionado | Martín | `tools/desplegar.sh` | 10-sep |
| 🟢 | **Mostrar en pantalla los roles y scopes del token** — punto explícito de la rúbrica | | `frontend/src/modules/cuenta/` | 11-sep |
| 🟢 | **Volver a medir 200 / 401 / 403 a través del borde** + el preflight en el navegador | | `docs/evidencia/` | 11-sep |
| 🟢 | **Informe ejecutivo de 5 páginas** | | fuera del repo | 12-sep |
| 🟢 | **Cerrar quién presenta cada indicador del EP2** | los 3 | — | 12-sep |
| 🟢 | **Ensayo cronometrado**, 5 a 10 minutos | los 3 | — | 12-sep |
| 🟢 | **Entregar**: enlaces de GitHub a AVA + copia al correo | | — | **13-sep 23:59** |

> ✅ **El riesgo del freeze se despejó el 10-09.** Los tres indicadores del API Manager ya se
> pueden demostrar: el borde está en el camino, valida el JWT, enruta hacia los servicios y es
> el único dueño del CORS. La evidencia medida a través del borde está en
> [`evidencia/`](evidencia/).
>
> ⚠️ **Lo que sigue bloqueado es el login con Google**, y no depende de código: falta el
> *client secret*. Sin él, la demo del inicio de sesión con Google no existe en ninguna cuenta.

> 🎥 **El video de respaldo salió de la lista**: no hay que entregarlo. Si la demo en vivo te
> pone nervioso, grábalo igual — pero no es una tarea del proyecto.

---

## 🚧 Los cuatro innegociables del EP1

Se confirmaron en clase el 26-08. **Sin cualquiera de ellos no hay entrega**, por bueno que esté
el resto.

| | Innegociable | Estado al 10-09 |
|---|---|---|
| 1 | **IDaaS** (Cognito) | 🟡 Funciona, pero **cada uno levantó su pool en su propia cuenta**: el frontend habla con uno y el BFF confía en otro. Por eso da 401 |
| 2 | **API Manager** (API Gateway) | 🟡 Creado y probado, **pero no lo atraviesa ni una petición**. Es la tarea que manda esta semana |
| 3 | **Sistema desplegado en internet** | 🟡 El sitio responde; **la API se cae cuando el laboratorio ajeno está apagado** |
| 4 | **Informe ejecutivo de 5 páginas** | 🔴 Sin empezar. Sale de los ADR, y ya hay **15 escritos** |

> 🔴 **El número 2 es el que más duele**, porque el enunciado nombra el API Gateway textualmente
> y el informe se califica justamente por justificar esa elección.

---

## 🎯 Lo que se califica

| | Pieza | Peso | Estado |
|---|---|---|---|
| 🟢 | **Frontend con OIDC** | **60% EP1** | Vue 3 desplegado con PKCE real. Registro, guard e interceptor hechos. **Falta mostrar los roles del token en pantalla** |
| 🟢 | **Validación del JWT en el BFF** | **40% EP1** | ASP.NET Core: firma contra el JWKS, `iss`, vigencia, `client_id`, `token_use`, scopes y grupos |
| 🟡 | **200 / 401 / 403 demostrables** | 20% EP2 | Medidos, pero **contra la EC2**. Hay que repetirlos a través del borde |
| 🔴 | **Rutas del API Manager → microservicios** | 13% EP2 | El borde no está en el camino |
| 🔴 | **CORS en el API Manager** | 7% EP2 | Declarado en dos sitios a la vez |

---

## 📍 Dónde estamos hoy · 10-09-2026

Verificado contra el repositorio **y contra la cuenta de AWS con credenciales reales**, no de
memoria.

| | Pieza | Detalle |
|---|---|---|
| 🟢 | Catálogo | **221 productos reales con imagen** en PostgreSQL, agrupados por producto canónico con varias ofertas |
| 🟢 | Scraper | Corriendo por horario desde el 27-08. Lee JSON-LD de listados públicos, sin navegador headless |
| 🟢 | PostgreSQL | Migrado el 09-09 (ADR-025), **antes de lo previsto**. Esquemas `product` y `scraper` en un solo servidor |
| 🟢 | Respaldo | `tools/respaldar.sh` vuelca los dos esquemas y **se verifica solo** |
| 🟢 | Pruebas del frontend | `npm run humo` en **0 fallos** — venía de 55 |
| 🟡 | Escrituras | El gateway exige el scope `ingesta`; falta cerrar la ruta directa a la EC2 |
| 🔴 | Red privada (VPC, NAT) | No existe: la EC2 tiene IP pública directa. Decisión consciente, cuesta por hora |
| 🔴 | CI en GitHub Actions | **0 workflows.** Necesita runner propio, no es añadir un YAML |
| 🔴 | Pruebas del backend C# | No existen |

### Lo que sabemos de AWS, ya sin suposiciones

| | Servicio | |
|---|---|---|
| 🟢 | Cognito · API Gateway · S3 · CloudWatch Logs · Parameter Store · RDS · EC2 | permitidos |
| 🟢 | **Elastic IP** | permitida — `allocate-address --dry-run` responde *«Request would have succeeded»* |
| 🔴 | **CloudFront** | **bloqueado**, ni siquiera deja leer. Esto cierra el ADR-023 con evidencia |

> 🔑 **El hallazgo que reordena la tarea 1:** el «problema de los dos user pools» no es de pools,
> **es de cuentas**. En la cuenta de Martín hay un pool (`us-east-1_cH76LiA02`) y cero instancias
> EC2; el frontend desplegado apunta a `us-east-1_ji5w1jelx`, que **no existe ahí**. La máquina y
> el pool que usa el sitio están en la cuenta de Panditax. Por eso la API cae cuando él apaga.

---

## 📅 Lo que se corrió de fecha

Ordenado **por fecha límite**, no por importancia. Nada se borra: se le pone fecha o se decide
que no se hace.

| Tarea | Por qué se corrió | Nueva fecha límite |
|---|---|---|
| **Persistir la lista de seguimiento** — hoy vive en memoria y se pierde en cada despliegue | No bloquea nota del EP1 | 20-sep |
| **Secretos a Parameter Store** — hoy en archivos de texto plano en la instancia | Calidad, no rúbrica | 20-sep |
| **Validación de entrada en `product-service`** — acepta cualquier precio y cualquier largo | Calidad, no rúbrica | 20-sep |
| **Registros a CloudWatch y una alarma de salud** — hoy nadie se entera si se cae | Calidad, no rúbrica | 27-sep |
| **Respaldo programado** en vez de manual | Ya existe el script, falta el temporizador | 27-sep |
| **Revisar dependencias de .NET** (las de npm dan 0) | Calidad | 27-sep |
| **Pruebas del backend C#** | Carril de Orion, sin empezar | 30-sep |
| **CI en GitHub Actions** | Necesita runner propio. Arrastrado desde la Semana 1 | 30-sep |
| **Adaptador de Hites** | Su endpoint devuelve 500 desde el 27-08 | sin fecha — se decide si se descarta |

---

## 🔵 v2 · Después del freeze

**El objetivo de la v2 es dejar de depender de una sola cuenta.** Es el riesgo más grande que
tiene el proyecto y no se arregla con código.

- **Réplica en las otras dos cuentas** y un simulacro de salto de dominio, hecho a propósito un
  día tranquilo. *Un plan de respaldo que nunca se probó no es un plan.*
- ~~Habilitar `pg_trgm` y `unaccent`~~ ✅ 10-sep en `infra/postgres/init/` — hoy solo crea los esquemas.
  Es una línea de SQL y destraba lo siguiente.
- **Emparejamiento entre tiendas** ([ADR-022](adr/022-identidad-de-producto-entre-tiendas.md)).
  Ya no lo frena la base de datos: falta la identidad de producto, porque el `externalId` es el
  código interno de cada tienda y agrupar por él no junta nada.
- Todo el bloque de calidad de la tabla de arriba.
- **Medir el emparejamiento a mano sobre 30 productos** y anotar el porcentaje. Da un número real
  para el informe: *«24 de 30 = 80%»*.

---

## ⚪ v3 · Más adelante

Deliberadamente difuso: esto se planea cuando la v2 esté andando, no ahora.

- **Mensajería**, cuando alguna de las tres señales del
  [ARQUITECTURA.md](ARQUITECTURA.md#comunicación-síncrona-y-mensajería) se cumpla. La primera que
  va a llegar es el segundo consumidor del cambio de precio.
- **Kafka** y analítica por ventanas de tiempo — son EP5 y EP6.
- **Avisos de reposición por talla**, que es lo que le da sentido a la lista de seguimiento.
- **Red privada y base de datos gestionada**, cuando el producto lo justifique y haya con qué
  pagarlas.
- **Más tiendas**, y ampliar de calzado a vestuario.

---

## ❓ Decisiones abiertas

Las cerradas están en [`adr/`](adr/). Estas siguen en el aire, y **ninguna se decide sola**.

| Decisión | Estado | Cuándo se decide |
|---|---|---|
| **¿En qué cuenta de AWS vive el sistema?** | 🔴 Bloquea la tarea 1 de esta semana | **hoy** |
| **Mensajería: ¿vuelve RabbitMQ, entra Kafka, o los dos?** | 🟡 Abierta a propósito. Se retiró porque no tenía consumidores *todavía*, no porque se descartara | Después del EP1 |
| **Dominio único en vez de CORS** | 🔴 Descartada por ahora: necesita CloudFront y **está bloqueado** en la cuenta. Además el CORS es un indicador evaluado | Con cuenta propia |
| **¿Se descarta Hites?** | 🟡 Su endpoint lleva caído desde el 27-08 | v2 |
| **Quién presenta cada indicador del EP2** | 🔴 Es nota individual | **12-sep** |

---

## 🛠️ Modo de trabajo

### Las ramas

```
feature/loquesea  ──PR──▶  development  ──PR──▶  main
                          (acá se prueba)      (producción real)
```

- Una **rama por tarea**, y el nombre dice qué hace.
- Todo PR llega primero a **`development`**. A `main` solo entra lo que ya funciona.

| Rama | Regla |
|---|---|
| `main` | Protegida: **1 revisión**, sin force-push, sin borrado |
| `development` | **Libre.** Se mergea sin esperar aprobación de GitHub |

> `development` se liberó a propósito para no trabarse. **El acuerdo de que nadie mergea su propio
> PR sigue en pie** — ahora es un acuerdo entre nosotros, no una regla que GitHub imponga.
> Ya se rompió una vez (PR #19) y quedó anotado en la bitácora sin drama, pero conviene no repetirlo.

### Los acuerdos

| Acuerdo | Por qué |
|---|---|
| **Cada uno commitea lo suyo** | Con nota individual, un historial donde commitea una sola persona perjudica al resto |
| **Nadie mergea su propio PR** | Un segundo par de ojos, y deja evidencia de revisión cruzada |
| **Pones tu nombre en la tabla antes de empezar** | Es lo único que evita el trabajo duplicado |
| **Cero secretos en el repo** | Un secreto subido queda en el historial para siempre |

### Lo que debería revisar el CI

⚠️ **Todavía no existe: 0 workflows.** Está en la lista con fecha 30-sep.

1. Que **compile** y que **pasen los tests**
2. **gitleaks** — que no se cuele ninguna contraseña ni llave
3. **Trivy** — vulnerabilidades en las imágenes
4. **Dependabot** activo

---

## ✅ Checklist final

**Los tres tienen que poder responder sobre cualquier fila**, la haya hecho quien la haya hecho.

### Identidad y frontend · 60% del EP1

- [x] **Cognito creado** — consola con usuarios, grupos y política de contraseñas
- [x] **Registro de usuario** — crear una cuenta nueva desde el frontend
- [x] **Authorization Code + PKCE** — DevTools con `code_challenge`, `S256`, `state`
- [x] **Guard e interceptor** — ruta protegida que redirige · el header `Authorization` en DevTools
- [ ] **Login con Google** — bloqueado por el *client secret*
- [ ] **Los tres tokens** — `id_token`, `access_token` y `refresh_token` decodificados
- [ ] **Roles y permisos del token** — que la pantalla muestre `cognito:groups` y `scope`

### Seguridad y backend · 40% del EP1

- [x] **Validación en el servicio** — el código que revisa `iss`, `client_id`, `token_use` y `exp`
- [x] **200 con token, 401 sin token** — medido con `curl` sobre el sistema corriendo
- [x] **403 con usuario sin permiso** — token válido sin el scope `ingesta`
- [ ] **Repetirlo a través del API Gateway**, que es donde se evalúa
- [ ] **Integración durable** — documentar la tolerancia al catálogo atrasado
- [x] **Historial de precios** — con el matiz de abajo

> ⚠️ El scraper arrancó el **27-08**, no en la semana 0. Al cierre el historial tendrá **dos
> semanas**, no las tres que decía el plan. **No es recuperable: conviene decirlo en el informe
> antes de que lo pregunten.**

### Nube y plataforma

- [x] **API Gateway** — consola con rutas, stages `dev`/`prod` e integraciones
- [x] **Servicios desplegados** — Caddy, gateway, product-service, scraper y PostgreSQL en la EC2
- [x] **Frontend desplegado** — el sitio responde
- [ ] **Que el tráfico pase por el API Gateway** — es lo que falta de verdad
- [ ] **CORS** — orígenes explícitos en consola + el preflight OPTIONS en DevTools
- [ ] **Dirección fija** para la instancia
- [ ] **Client credentials** — el scraper saca su token y llama a la ingesta
- [ ] **Red privada** — decidido que no entra en el EP1
- [ ] **CI/CD** — decidido que no entra en el EP1
