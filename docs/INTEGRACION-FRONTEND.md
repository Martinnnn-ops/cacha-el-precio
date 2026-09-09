# Integración del frontend al monorepo

> **Última revisión: 09-09-2026.** Este documento tiene dos partes: el **diagnóstico** (§0 a §8),
> escrito el 08-09 y verificado contra el código y el sistema desplegado, y el **plan de trabajo**
> (§10), que es lo que se está ejecutando. Los comandos para volver a comprobarlo todo están en §9.

---

## 0. Resumen en diez líneas

El frontend vive **fuera de este repositorio** y durante el 8 de septiembre pasaron tres cosas
encadenadas, ninguna de las cuales se habló entre las personas que las hicieron:

1. Panditax subió a `main` del front el **login con Google mediado por Cognito** (4 commits).
2. Orion mergeó el **PR #18** de este repo: migración completa de los servicios a **C#**, que
   **rompió el contrato de datos** que el frontend consumía.
3. Orion abrió el **PR #1 en el repo del front** adaptándolo al contrato nuevo. Quedó
   **mergeado a `main`** el 08-09 a las 23:58 UTC (`5dd43e0`).

**Lo que hay publicado no es ninguna de las dos cosas.** El bundle que sirve
`www.cacha-el-precio.com` es el **anterior a todo esto**: se descargó y se inspeccionó el 09-09, y
manda `X-API-VERSION: 0.3.0` y llama a `/catalogos`, `/productos/{id}/historial` y
`/productos/{id}/visitas`. Es decir, **nadie ha desplegado el contrato nuevo todavía**, ni el del
backend ni el del frontend. Eso es una suerte: el orden de despliegue de §4 sigue intacto.

> ⚠️ **Nadie ha ejecutado nunca el backend C#.** El PR #18 lo dice: *"Docker no está instalado en
> el equipo donde se preparó el cambio"*, y en el equipo de Martín tampoco hay `dotnet`. No existe
> evidencia de que compile. Esa es la primera incógnita que hay que despejar, y por eso el plan
> tiene una fase dedicada a levantarlo antes de adaptar nada.

---

## 1. Los tres repositorios

| Repositorio | Dueño | Qué contiene |
|---|---|---|
| `Martinnnn-ops/cacha-el-precio` | equipo | este monorepo: gateway, product-service, scraper, infra, docs |
| `Panditax727/Cacha-el-Precio-Frontend` | Panditax | el frontend Vue 3 completo |
| *(product-service independiente)* | Orion | mencionado en el ADR-020 como copia paralela |

El ADR-020 advierte del riesgo por escrito: *"Product Service vive también como repositorio
independiente, por lo que se debe evitar que ambas copias diverjan"*. Con el frontend son
**tres superficies** que pueden separarse sin que nadie se entere.

Clon local del frontend para trabajar: **`/home/kenny/cacha-el-precio-frontend`**

---

## 2. Qué pasó con el backend (PR #18)

Mergeado el 08-09 a las 16:02 (hora local), **+3544 / −4584**, por Orion.
**Sin una sola revisión ni comentario.** Decisión documentada en
[`docs/adr/020-csharp-y-simplificacion-de-servicios.md`](adr/020-csharp-y-simplificacion-de-servicios.md).

### Lo que desapareció

`gateway/` en Java · `product-service/` en Java · `price-service/` entero · `pom.xml` raíz ·
el wrapper de Maven · RabbitMQ · las migraciones Flyway · **el contador de visitas** (PR #12).

### Lo que se conservó — y hay que reconocerlo

El BFF en C# mantiene íntegro lo que puntúa el 40% del EP1:

| Control | Dónde en `gateway/Program.cs` |
|---|---|
| Firma contra JWKS + issuer | `Authority` + `ValidIssuer` |
| Vigencia | `ValidateLifetime` |
| `token_use == access` | `OnTokenValidated` |
| `client_id` en lista blanca | `OnTokenValidated` |
| Roles desde `cognito:groups` | `RoleClaimType` |
| 401 y 403 con cuerpo JSON explicativo | `OnChallenge` / `OnForbidden` |

`ValidateAudience = false` **es correcto, no un descuido**: el `access_token` de Cognito no trae
`aud`, trae `client_id`. Está explicado en [`IDENTIDAD.md`](IDENTIDAD.md) §5.

### Lo que mejoró

**La escritura dejó de estar abierta a internet.** `POST`, `PUT` y `DELETE /productos` ahora
exigen la policy `product-write`, que pide el scope de ingesta. Era el hallazgo 🔴 número uno de
[`INTEGRACION.md`](INTEGRACION.md) y quedó cerrado.

### Rutas que expone el gateway C# hoy

```
GET    /productos                       anónimo
GET    /productos/{id:long}             anónimo
POST   /productos                       policy product-write
PUT    /productos/{id:long}             policy product-write
DELETE /productos/{id:long}             policy product-write
GET    /catalogos                       anónimo  ← lista HARDCODEADA en Program.cs
GET    /api/products                    anónimo
GET    /api/products/{id:int}           anónimo
GET    /api/products/by-category/{c}    anónimo
GET    /api/products/by-price/{p}       anónimo
GET    /api/products/by-size/{s}        anónimo
POST   /api/products                    policy product-write
PUT    /api/products/{id:int}           policy product-write
DELETE /api/products/{id:int}           policy product-write
GET    /api/yo                          autenticado
GET    /api/admin/diagnostico           rol admin
GET    /seguimiento                     autenticado
POST   /seguimiento/{productId:long}    autenticado
DELETE /seguimiento/{productId:long}    autenticado
GET    /health                          anónimo
```

**No existe `POST /productos/{id}/visitas`** en el gateway ni en el product-service.

### Deuda que el cambio introduce

- **SQLite reemplazó a PostgreSQL** en el Product Service:
  `ConnectionStrings__Sqlite: Data Source=/app/data/product.db`, con volumen `product-datos`.
  Persiste entre redeploys ✅, pero no escala horizontalmente ni tiene réplica. En un ramo de
  **Cloud Native**, defender un archivo dentro del contenedor en vez de una BD gestionada es
  cuesta arriba. El ADR-020 lo reconoce como costo aceptado.
- **El versionado por cabecera murió.** `ProductServiceProxy.cs` fija `Version: 1.0` e ignora el
  `X-API-VERSION` del cliente. Las tres versiones (0.1.0 / 0.2.0 / 0.3.0) ya no existen.
- **Los filtros no se leen.** El proxy conserva el *query string*, pero `GetAllProducts()` no
  declara ningún `[FromQuery]`: `?catalogoId=` y `?soloActivos=` se ignoran en silencio.
- **`/catalogos` está hardcodeado** en `Program.cs` (6 categorías fijas). Sus ids 1–6 no
  corresponden a ningún campo del producto.
- **El seguimiento sigue en memoria** (`AddSingleton<FollowRepository>`): se pierde al reiniciar.
- El PR admite que **Docker nunca se probó**: *"Docker no está instalado en el equipo donde se
  preparó el cambio"*.

---

## 3. Qué pasó con el frontend

### 3.1 `main` — lo que subió Panditax (08-09, 16:53 local)

| commit | qué hace |
|---|---|
| `ff05a5e` | **login con Google mediado por Cognito** |
| `b45ad0c` | slots reales de AdSense |
| `dffa507` | el sitemap incluye las páginas de producto |
| `2f9c1f6` | `registrarVisita` manda `{}` + `Content-Type: application/json` |

El flujo de autenticación está **bien construido**: `response_type=code`, PKCE S256, `state`
contra CSRF, canje en `x-www-form-urlencoded`, y guarda el **`access_token`** en `sessionStorage`
— que es exactamente el token que el BFF espera, porque `client_id` y `token_use` solo viajan en
el access token, no en el `id_token`.

El motivo de pasar por Cognito está bien argumentado en el propio código: Google exige *client
secret* en el canje incluso con PKCE para clientes web, y un secret no puede vivir en el
navegador. Cognito lo guarda por nosotros.

### 3.2 `feature/backend-csharp-contract` — PR #1, de Orion, **MERGEADO el 08-09**

Dos commits, 15 archivos. Adapta el front al contrato C#. La traducción de campos está **bien
hecha**:

| | |
|---|---|
| `name` / `price` / `description` / `brand` / `image` / `active` / `url` | ✅ traducidos |
| `category` como string, sin cruce por id | ✅ las categorías se derivan de los productos |
| Cabecera `Version: 1.0` | ✅ calza con el proxy |
| `VITE_API_BASE_URL` pasa a `…/api` → llama a `/api/products` | ✅ ruta existente y anónima |
| Errores: acepta Problem Details **y** `{estado, error, mensaje}` | ✅ |
| Limpia `VITE_GOOGLE_CLIENT_ID`, inútil tras el cambio a Cognito | ✅ |

Y añade **`adaptarTiendas()`**, que deriva las tiendas del campo `store` con colores por marca:
el primer uso real del multi-tienda que trajo el modelo nuevo.

**Lo que hay que revisar antes de mergearlo:**

1. **Elimina el trabajo de Panditax del mismo día.** `registrarVisita()` desaparece del servicio
   y con ella las 31 líneas de `ProductoDetailView.vue` que registraban una visita por día y por
   navegador, tolerante a fallos. En el adaptador queda `visitas: 0` fijo. Es consecuencia
   inevitable de que el modelo C# no tiene `visitas`, pero el PR no lo menciona y **son dos
   personas tocando los mismos archivos sin coordinarse**.
2. **Campo inexistente.** `agregadoHace: diasDesde(fila.createdAt)` — `ProductResponse.cs` no
   tiene `CreatedAt`. Siempre será `null`. No rompe nada, pero aparenta funcionar.
3. **Sigue sin comparar precios.** `precios[]` conserva **un solo elemento por fila** y nadie
   agrupa por `externalId`. La aplicación lista ofertas sueltas, no compara el mismo producto
   entre tiendas. Lo delicado: la advertencia anterior del adaptador —*"no puede comparar
   precios, que es su razón de ser"*— **se borró sin que el problema se resolviera**.
4. **El filtrado se fue al cliente.** `obtenerProductos()` ya no acepta filtros: trae el catálogo
   entero y filtra en el store. Y `cacheFilas` guarda la promesa para toda la vida de la pestaña:
   nunca se refresca.

---

## 4. 🔴 El orden de despliegue no es negociable

**08-09 por la noche**, contra `api.cacha-el-precio.com`. Corría todavía el Micronaut en Java que
este repositorio ya no contiene:

| Petición | Respuesta |
|---|---|
| `X-API-VERSION: 0.3.0` → `/productos` | **200** ← lo que manda el bundle publicado |
| `Version: 1.0` → `/productos` | **404** ← lo que manda el `main` del front desde el PR #1 |
| sin cabecera de versión → `/productos` | **400** |
| `/api/yo`, `/seguimiento`, `/health` | **404** (rutas que solo existen en el gateway) |

El cuerpo de error llegaba en formato HAL (`_embedded.errors[]`), firma inconfundible de Micronaut.

**09-09:** el host resuelve (`44.196.131.41`) pero **el puerto 443 no acepta conexiones**. No es un
incidente: la EC2 vive en la cuenta de AWS de un compañero, con créditos limitados, y **no se
mantiene encendida 24/7 a propósito**. Conviene decirlo así en el informe en vez de presentarlo
como un servicio caído.

> ⚠️ Esto **corrige el punto 0.4 de [`INTEGRACION.md`](INTEGRACION.md)**, que trata el apagado como
> un riesgo a resolver con una Elastic IP. Una Elastic IP evita que la dirección cambie al
> reiniciar, pero no enciende la instancia: si el laboratorio está cerrado el día de la demo, el
> dominio sigue sin responder. Son dos problemas distintos y el segundo es de coordinación, no de
> infraestructura.

**El orden sigue siendo obligatorio:** desplegar el backend C# → verificar → recién entonces
publicar el frontend. Hoy se cumple por accidente —el bundle publicado es el viejo—, pero el
primero que ejecute `npm run desplegar` desde `main` rompe el sitio si el backend no está arriba:
404 en todas las llamadas, sin error visible para el usuario, y el rastreador de AdSense
recorriendo un catálogo vacío.

---

## 5. Cognito: los dos User Pools siguen sin reconciliar

| | Pool del script (`cognito.env`) | Pool que usa el frontend |
|---|---|---|
| User pool | `us-east-1_cH76LiA02` | `us-east-1_ji5w1jelx` |
| Client id | `61amk99kv70gndiupsfsebc49d` | `3ev76jdoin1ouc1grqfdi3laam` |
| Grupos `admin`/`usuario` | ✅ | ❓ |
| Resource server + scope `ingesta` | ✅ | ❓ |
| Client de `client_credentials` (scraper) | ✅ | ❓ |
| **Google federado** | 🔴 **no** | ✅ |

Falla por partida doble: el `iss` no coincide **y** el `client_id` no está en
`COGNITO_CLIENT_IDS_VALIDOS`. Todo token emitido por el front da **401** en el gateway.

**La decisión propuesta sigue en pie** ([`INTEGRACION.md`](INTEGRACION.md) §0.2): gana el pool del
script, porque es el único con grupos —sin ellos no hay 403, que es el 20% del EP2—, con resource
server y con el client del scraper. Al otro no se le pueden agregar sin rehacerlos.

### Lo que falta para que ese pool sirva

| # | Qué | Dónde |
|---|---|---|
| 1 | `SupportedIdentityProviders` es `["COGNITO"]`, falta `"Google"` | `tools/crear-cognito.sh:188` |
| 2 | No hay ni un `create-identity-provider` en todo el script | `tools/crear-cognito.sh` |
| 3 | Las callbacks son `/callback`; **el front usa `/auth/google`** | `tools/crear-cognito.sh:181-184` |
| 4 | Faltan las callbacks de producción (`https://www.cacha-el-precio.com/auth/google`) | paso 7 de [`DESPLIEGUE.md`](DESPLIEGUE.md), nunca ejecutado |
| 5 | El bloque es idempotente: si el client existe, **no actualiza nada** | `tools/crear-cognito.sh:168-169` |
| 6 | Las dos constantes del front | `.env.production` y `.env.live` |

El punto **5** es la trampa: cambiar el script no arregla el pool que ya existe. Y el propio
script lo advierte en `crear-cognito.sh:241` — `update-user-pool-client` **reemplaza** la
configuración entera, todo campo que no se le pase vuelve al valor por defecto. Hay que pasarle
los ocho campos completos de una sola vez.

El client `frontend` del script ya coincide con lo que el front pide en `AllowedOAuthFlows`
(`["code"]`), `AllowedOAuthScopes` (`["openid","email","profile"]`) y `GenerateSecret: false`.

### Dos cosas que no se pueden hacer en solitario

- **El client secret de Google** lo tiene quien creó el proyecto en Google Cloud (el client id
  `1080277280532-…` no es de la cuenta de Martín). Cognito lo necesita para crear el IdP.
- En Google Cloud Console hay que añadir como *Authorized redirect URI*:
  `https://cacha-el-precio-116813910999.auth.us-east-1.amazoncognito.com/oauth2/idpresponse`

### Un efecto que nadie ha considerado

Un usuario que entra por Google llega **sin `cognito:groups`**. Los grupos `admin`/`usuario` el
script solo se los asigna a los usuarios de prueba. `/api/admin/diagnostico` exige rol `admin`,
así que el 403 se sigue demostrando con el usuario de prueba — pero conviene **decidirlo ahora,
no descubrirlo en la defensa**.

---

## 6. Cabos sueltos menores

- **www**: `cacha-el-precio.com` responde **301** hacia `www`. El sitemap emite URLs **sin** www
  y el CORS del `Caddyfile` solo permite el origen **con** www. Inconsistente en los dos extremos.
- **`deploy/README.md` del front documenta CloudFront + OAC** con todo detalle, pero lo desplegado
  es **Cloudflare**. Es buena documentación describiendo algo que no existe; candidata a anexo del
  informe una vez corregida.
- `google.oauth.js` quedó sin salto de línea final.
- El `docker-compose.yml` monta `./infra/postgres/init`, que sigue existiendo (verificado).

---

## 7. Las decisiones, y cuáles ya están tomadas

| | Decisión | Estado |
|---|---|---|
| 🟡 | **Cómo entra el front al monorepo** | ✅ **`git subtree`**, para conservar los 15 commits y la autoría de Panditax, y poder seguir sincronizando con su repositorio |
| 🔴 | **¿El front habla inglés o español?** | ✅ **Español, `/productos`, el contrato del BFF.** Argumentada en el [ADR-021](adr/021-contrato-publico-en-el-bff.md) |
| 🔴 | **¿Vuelven las visitas?** | ✅ **Sí**, en la Fase 4: campo en el modelo + migración EF Core + endpoint anónimo + restaurar las 31 líneas del front |
| 🔴 | **Qué User Pool gana** | ⬜ Pendiente — depende de información que tiene otra persona (§5). Se resuelve en la Fase 5 |
| 🟡 | **¿Se agrupa por `externalId`?** | ⬜ Pendiente — se decide en la Fase 4, junto con dónde se agrupa: en el BFF o en el navegador |
| 🟡 | **SQLite vs PostgreSQL** | ⬜ Después de la entrega del 13-09. Cambiar de motor a dos días del freeze es como se pierde una entrega |

### Choque concreto al mover los archivos — ✅ resuelto

El `.gitignore` de este monorepo tenía `.env` + `.env.*` con excepción **solo** para
`.env.example`, y el frontend **versiona a propósito** `.env.production`, `.env.live` y
`.env.test`. Copiado tal cual, esos tres archivos desaparecían del repositorio y el build de
producción se quedaba sin configuración, en silencio.

Se resolvió con **cuatro excepciones de ruta completa** (`!frontend/.env.production`, etc.) en vez
de excepciones genéricas. La diferencia importa: `!.env.production` a secas habría desprotegido
también un `.env.production` en la raíz del repositorio, que sí llevaría credenciales de AWS o de
la base de datos. La regla general falla cerrada y cada excepción nombra el archivo exacto que
abre.

**Por qué versionarlos es correcto y no un descuido:** todo lo que empieza con `VITE_` Vite lo
incrusta en el bundle. Se comprobó descargando el JavaScript publicado: el client id de Cognito y
el de AdSense están ahí, legibles. Son públicos por construcción. El client secret de Google no
aparece en ninguno de los tres: vive solo dentro de Cognito.

Verificación:

```bash
git check-ignore -v frontend/.env.production   # sin coincidencia → se versiona
git check-ignore -v frontend/.env              # coincide .env.*   → sigue ignorado
git check-ignore -v .env.production            # coincide .env.*   → sigue ignorado
```

Lo demás encajó sin fricción: no hay `.github/workflows` que ajustar, el `pom.xml` ya no existe, y
el frontend pesa 1,4 MB con un historial limpio de 15 commits.

---

## 8. Entorno local

| | |
|---|---|
| `dotnet` | ❌ **no instalado** — el backend nuevo no se puede compilar ni correr fuera de Docker |
| `node` | ✅ v26.5.0 |
| `docker` | ✅ 29.6.2 (no autoarranca: alias `docker-on`) |
| Clon del front | `/home/kenny/cacha-el-precio-frontend` |

---

## 9. Comandos para volver a comprobar todo

```bash
# ¿Qué backend está desplegado de verdad?
curl -s -o /dev/null -w '%{http_code}\n' -H 'X-API-VERSION: 0.3.0' \
  https://api.cacha-el-precio.com/productos          # 200 = sigue el Micronaut viejo
curl -s -o /dev/null -w '%{http_code}\n' -H 'Version: 1.0' \
  https://api.cacha-el-precio.com/productos          # 404 = el contrato nuevo no está arriba
curl -s -o /dev/null -w '%{http_code}\n' \
  https://api.cacha-el-precio.com/health             # 404 = el gateway no está en la cadena

# Distinguir "ruta que no existe" de "entidad que no existe", sin escribir nada
curl -s -o /dev/null -w '%{http_code}\n' -X POST \
  https://api.cacha-el-precio.com/productos/1/ruta-inventada-xyz

# Estado de los dos repos
git -C ~/cacha-el-precio log --oneline -5
git -C ~/cacha-el-precio-frontend fetch --all
gh pr list --repo Panditax727/Cacha-el-Precio-Frontend --state open
gh pr list --repo Martinnnn-ops/cacha-el-precio --state open

# El diff del frontend adaptado
git -C ~/cacha-el-precio-frontend diff main...origin/feature/backend-csharp-contract
```

---

## 10. El plan de trabajo

Ocho fases, en la rama `feature/frontend-en-monorepo`. Cada una tiene un **criterio de salida**:
mientras no se cumpla, no se pasa a la siguiente. La razón de partirlo así es simple — mover el
frontend y adaptarlo son dos cosas distintas, y si van en el mismo commit y algo se rompe, no hay
forma de saber cuál de las dos fue.

### Fase 0 · Base y decisiones — ✅ hecha

Rama creada desde `development`, excepciones del `.gitignore` probadas con `git check-ignore`, y
las decisiones de §7 tomadas y argumentadas ([ADR-021](adr/021-contrato-publico-en-el-bff.md)).

### Fase 1 · El frontend entra, sin tocar una línea de su código

`git subtree add --prefix=frontend`, `npm install`, `npm run humo` y `npm run build`.

**Criterio de salida:** el frontend compila y pasa las pruebas de humo dentro del monorepo, con
cero cambios funcionales. Las pruebas de humo corren en modo `test`, que deja `VITE_API_BASE_URL`
vacía y usa los datos de ejemplo: **no necesitan backend**, así que esta fase se puede cerrar sin
que el backend haya arrancado nunca.

### Fase 2 · Levantar el backend de verdad y medir el contrato

Es la fase que más riesgo quita, y no estaba prevista: **nadie ha ejecutado nunca este backend**
(ver el aviso de §0). Antes de adaptar el frontend a un contrato, hay que saber qué responde ese
contrato de verdad, no lo que dice el código que responde.

1. `docker compose build gateway product-service` — acá se sabe si el C# siquiera compila.
2. Levantarlo y probar **cada ruta** del gateway, incluida la que importa para el EP1: que las
   escrituras devuelvan **401 sin token**.
3. Cargar datos de prueba: la base SQLite arranca vacía.
4. Anotar las respuestas reales.

**Criterio de salida:** un mapa verificado del contrato, y el backend corriendo en local.

### Fase 3 · Conectar el frontend al backend, commit por commit

Cada commit con su prueba contra el backend de la Fase 2.

| Commit | Qué |
|---|---|
| A | El frontend pasa a `/productos` (el contrato del BFF, ADR-021) |
| B | `agregadoHace: diasDesde(fila.createdAt)` — `ProductResponse` no tiene `CreatedAt`: hoy siempre es `null` y aparenta funcionar |
| C | `cacheFilas` guarda la promesa para toda la vida de la pestaña: nunca se refresca |
| D | Filtros: `GetAllProducts()` no declara ningún `[FromQuery]`, así que `?catalogoId=` se ignora en silencio |

**Criterio de salida:** el frontend contra el gateway local lista productos, abre fichas y filtra
sin un error en consola.

### Fase 4 · Recuperar lo perdido y cerrar la deuda del contrato

1. **Visitas** — campo en el modelo, migración EF Core, endpoint **anónimo** (el frontend cuenta
   vistas sin pedir login) y restaurar `registrarVisita()`.
2. **`createdAt`** — sin él «Lo más reciente» de la portada ordena por `null`.
3. **Agrupar por `(store, externalId)`** — sin esto la aplicación lista ofertas sueltas y no
   compara precios, que es su razón de ser. Se decide **dónde** se agrupa: hacerlo en el navegador
   obliga a bajar el catálogo entero, y es justo el trabajo que convierte al gateway en un BFF de
   verdad (ver ADR-021, *«Lo que esta decisión todavía NO resuelve»*).
4. **`/catalogos`** — hoy es una lista fija en `Program.cs` cuyos ids no corresponden a ningún
   campo del producto. Es contrato público y miente.

### Fase 5 · Identidad: un solo User Pool

Los seis puntos de §5. **Dos cosas no dependen de este equipo de trabajo:** el client secret de
Google lo tiene quien creó el proyecto en Google Cloud, y hay que añadir allá el redirect URI de
Cognito. Se piden **antes** de empezar la fase, no durante.

**Criterio de salida:** login con Google → token → el BFF lo acepta → `/api/yo` responde 200; sin
token 401; con token válido pero sin el grupo `admin`, 403. Esos tres códigos son evidencia
directa del checklist del EP1.

### Fase 6 · Estado asegurado y PR a `development`

Documentación primero: `BITACORA.md`, `INTEGRACION.md`, `README.md`, los ADR que falten y el
índice de `AGENTS.md`. **El PR se abre cuando el equipo lo decida**, no automáticamente.

### Fase 7 · Dockerizar todo

Dockerfile del frontend y su entrada al `docker-compose.yml`, detrás de Caddy. Si el frontend y la
API salen por el mismo origen, **el CORS deja de existir como problema** en vez de resolverse dos
veces.

**Criterio de salida:** `docker compose up` levanta el sistema entero y el sitio funciona de punta
a punta en local.

### Fase 8 · AWS

Replicar la infraestructura en las tres cuentas, el API Gateway delante de la cadena, y los anexos
del informe. Ojo con lo de §4: el apagado de la EC2 es una restricción de créditos, no una avería,
y la arquitectura tiene que poder levantarse completa desde cero en cualquier cuenta — que es
justamente para lo que existe [`MIGRACION.md`](MIGRACION.md).

⚠️ Nada de esto toca `main`. `development` es la rama de trabajo.
