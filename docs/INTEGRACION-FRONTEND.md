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

### Fase 1 · El frontend entra, sin tocar una línea de su código — ✅ hecha (§11)

`git subtree add --prefix=frontend`, `npm install`, `npm run humo` y `npm run build`.

**Criterio de salida:** el frontend compila y pasa las pruebas de humo dentro del monorepo, con
cero cambios funcionales. Las pruebas de humo corren en modo `test`, que deja `VITE_API_BASE_URL`
vacía y usa los datos de ejemplo: **no necesitan backend**, así que esta fase se puede cerrar sin
que el backend haya arrancado nunca.

### Fase 2 · Levantar el backend de verdad y medir el contrato — ✅ hecha (§13)

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

---

## 11. Cómo entró el frontend · Fase 1, 09-09

### El comando, y por qué ese y no una copia

```bash
git remote add frontend-origin https://github.com/Panditax727/Cacha-el-Precio-Frontend.git
git subtree add --prefix=frontend frontend-origin main
```

**Sin `--squash`, a propósito.** Con `--squash` el historial completo se aplasta en un solo commit
y la autoría desaparece. Los 18 commits entraron con su autor original:

| Autor | Commits |
|---|---|
| Panditax727 | 15 |
| OrionTheProgrammer | 2 |
| Martin Mora Alvarez | 1 |

Eso no es una cortesía. En un trabajo de tres personas evaluado en parte por el aporte individual,
`git log` es la evidencia de quién hizo qué, y aplastarlo la borra.

### Qué permite el subtree que una copia no

El repositorio de Panditax **sigue existiendo y él puede seguir trabajando ahí**. Con el subtree
los cambios se traen después con un comando, sin resolver a mano archivo por archivo:

```bash
# Traer lo nuevo del repo del frontend al monorepo
git subtree pull --prefix=frontend frontend-origin main

# Y al revés, si hiciera falta devolver algo
git subtree push --prefix=frontend frontend-origin <rama>
```

> ⚠️ **El remoto `frontend-origin` es configuración local**, no viaja en el repositorio. Quien
> clone el monorepo y quiera sincronizar tiene que añadirlo con el primer comando de arriba.

### Verificación

| Qué | Resultado |
|---|---|
| Archivos incorporados | 108 |
| `frontend/.env.production`, `.live`, `.test`, `.example` | ✅ versionados — la excepción del `.gitignore` funcionó en el caso real |
| `frontend/node_modules/` y `frontend/dist/` | ✅ ignorados por las reglas que el monorepo ya tenía |
| `npm install` | ✅ 72 paquetes, **0 vulnerabilidades** |
| `npm run build` | ✅ compila |
| `npm run humo` | ⚠️ **55 comprobaciones fallidas** — ver abajo |

> ✅ **Resuelto en la Fase 1.5 (ver §12).** El apartado de abajo se conserva porque explica de
> dónde venía cada cosa. Hoy `npm run humo` da **0 comprobaciones fallidas**.

### La línea base de las pruebas: 55 fallos que ya estaban

El criterio de salida de esta fase era *«cero cambios funcionales»*, y se cumple: **el mismo
comando en el repositorio original, en el mismo commit, da exactamente el mismo número**. También
da 59 en `2f9c1f6`, el commit anterior al PR #1. No los introdujo el traslado ni el PR de Orion:
**venían de antes y nadie los estaba mirando.**

Se dejan anotados acá porque son la referencia contra la que se compara en la Fase 3: si después
de tocar el frontend el número sube, lo rompimos nosotros.

No son 55 problemas. Son **tres**, y cada uno arrastra a muchos:

**1 · La URL del detalle cambió y las pruebas no.** El commit `07872b8` cambió la ruta de
`/producto/:id` a `/producto/:slug`. `scripts/humo.mjs` sigue renderizando `/producto/1`, que con
el patrón nuevo **hace match pero no corresponde a ningún producto**, así que la vista muestra su
estado de «ya no está». De ahí en cascada: sin ficha no hay migas de pan, ni muescas del ticket,
ni enlaces a la tienda, ni gráfico de precios. **~33 de los 55.**

**2 · `.env.test` no define las variables de AdSense**, así que `AdSlot` no renderiza el `<ins>`
—que es su comportamiento correcto y deseado— pero la prueba espera encontrarlo. **5 fallos.**
Es un desacuerdo entre la prueba y el diseño, no un defecto del código.

**3 · `.env.fallo` no existe en el repositorio, y el `.gitignore` del frontend se contradice.**
La prueba «el store no borra lo que ya tenías» arranca un Vite en `mode: 'fallo'` para que la
petición falle **de verdad** — la idea es excelente, porque poner `error` a mano dejaría pasar un
`catch` que vaciara la lista. Pero sin ese archivo, `VITE_API_BASE_URL` queda indefinida,
`USAR_MOCK` se activa y la carga **tiene éxito** con los datos de ejemplo. La prueba no falla
porque el código esté mal: falla porque nunca llega a probar nada. **4 fallos.**

Lo que lo hace vistoso es el `.gitignore` del propio frontend:

```gitignore
# Los .env.test/.env.live/.env.fallo SÍ van al repo: son parte de las
# pruebas y no contienen nada privado.
.env

# Archivos de test/respaldo (solo se usan localmente)
.env.fallo          ← cuatro líneas más abajo, lo ignora
```

El comentario y la regla dicen lo contrario. **La prueba solo pasa en la máquina donde ese archivo
existe sin versionar** — que es la definición de una prueba que no protege a nadie.

### Lo que esto significa para el equipo

Un conjunto de pruebas con 55 fallos permanentes deja de avisar de nada: cuando todo está rojo,
un rojo nuevo no se distingue. `npm run humo` es lo más parecido a un CI que tiene este proyecto
—no hay ni un workflow en `.github/`— así que arreglarlo no es cosmética, es recuperar la única
red de seguridad que hay antes del freeze.

---

## 12. Fase 1.5 · Recuperar la red de seguridad · 09-09

Se hizo antes de la Fase 2 por una razón de método: **el arnés de pruebas se arregla antes de
tocar el código que va a probar**, no después. Las Fases 3 y 4 consisten justamente en cambiar el
frontend y el contrato, que es cuando más falta hace saber si algo se rompió.

`npm run humo` pasó de **55 comprobaciones fallidas a 0**, en cuatro commits.

| Commit | Qué arregla | Fallos |
|---|---|---|
| `89a95c8` | Las pruebas piden la ficha por slug, como la aplicación de verdad | −34 |
| `7c663d2` | Se versionan `.env.anuncios` y `.env.fallo`, que el `.gitignore` se comía | −19 |
| `59274f7` | Un slug inexistente cae en el 404 | −2 |

### 🔴 El hallazgo: anuncios en páginas que no existen

De los tres, este no era un problema del arnés sino **un defecto real en producción**, y es el que
justifica haber hecho esta fase.

Cuando la ruta del detalle era `/producto/:id(\d{1,12})`, el router **rechazaba por su forma**
cualquier cosa que no fuera un número, y caía en el 404 —que lleva `meta.sinAnuncios` justamente
para esto—. Al pasar a `/producto/:slug`, el patrón acepta cualquier texto. Nadie quitó la
defensa: **dejó de aplicarse sola** al cambiar el patrón.

Medido antes y después, con todos los bloques de anuncio configurados:

```
antes:  1 anuncio(s) | 404:NO  | «ya no está»:sí | /producto/esto-no-existe-en-ningun-catalogo
ahora:  0 anuncio(s) | 404:sí  | «ya no está»:no | /producto/esto-no-existe-en-ningun-catalogo
```

Dos consecuencias, y la segunda cuesta dinero:

1. **Soft 404.** Infinitas URLs válidas respondiendo como página buena. Google lo cuenta contra el
   sitio entero, no contra esas páginas.
2. **Anuncios en una página sin contenido propio.** Las políticas de AdSense lo prohíben, y no hay
   aviso previo: se cierra la cuenta, y con ella todos los ingresos del sitio. El propio
   `routes.js` tenía escrito el riesgo en un comentario, para la ruta del 404.

**Dónde se arregló y por qué ahí:** en un `beforeEnter` de la ruta, no en la vista. La pregunta
«¿esta página llega a existir?» es de enrutado. Resuelta en la vista, la ficha ya se montó —con su
layout y su bloque de anuncio— antes de descubrir que no había nada que enseñar; y en el render
del servidor el redirect ni siquiera se espera. La vista conserva el suyo para el único caso que
`beforeEnter` no ve: cuando solo cambia el parámetro, saltando de un producto a otro dentro de la
aplicación.

`frontend/README.md` describía la defensa vieja (*«la forma del id se declara en la ruta, así que
ni llega a la vista»*). Se corrigió en el mismo commit: un documento que miente es peor que uno
que falta, porque en la defensa oral la contradicción la encuentra cualquiera.

### Lo que hay que contarle al equipo

Los tres arreglos tocan archivos de Panditax (`scripts/humo.mjs`, `.gitignore`, `routes.js`,
`ProductoDetailView.vue`), y `AGENTS.md` pide avisar antes de entrar en el carril de otro. Van en
commits separados y descritos para que se vea qué se tocó sin leer un diff grande.

Vale la pena decirle también lo que **no** es culpa de nadie: los 55 fallos no los introdujo su PR
ni el de Orion. Se acumularon commit a commit, cada uno por un motivo razonable —una URL más
legible, un `v-if` para no pintar un elemento vacío—, y ninguno de esos cambios avisó de que había
roto una prueba, porque ya había otras en rojo. Es exactamente cómo un conjunto de pruebas deja de
servir: no de golpe, sino de a poco.

---

## 13. Fase 2 · El backend, ejecutado por primera vez · 09-09

### Sí compila, y sí arranca

Era la incógnita de §0: el PR #18 se mergeó sin que nadie hubiera ejecutado nunca ese código.

```bash
docker compose --env-file .env --env-file cognito.env build product-service gateway
```

Las dos imágenes compilan sin un solo error. Product Service **aplica sus migraciones de EF Core
al arrancar** (`InitialCreate` e `IntegrateScraperContract`) y queda escuchando en el 8081; el
gateway, en el 8080. La cadena `Caddy → gateway → product-service` responde de punta a punta.

### 🔴 Levantar el proyecto en local peleaba con producción

El `Caddyfile` del repositorio es **el de producción**: declara el sitio `api.cacha-el-precio.com`,
así que Caddy, al arrancar, hace lo que se le pidió — pedirle a Let's Encrypt un certificado para
ese dominio. En una máquina de desarrollo el desafío no lo puede resolver nadie, porque el dominio
apunta a la EC2.

**Y el daño no se queda en tu máquina.** Let's Encrypt limita los intentos fallidos por dominio y
por hora. Si los tres levantamos el compose en local, se puede agotar la cuota **del dominio real**
y dejar a la EC2 sin poder renovar su certificado. Cualquiera que clone el repositorio y haga
`docker compose up` lo provoca sin enterarse.

Resuelto con dos archivos nuevos:

| Archivo | Qué hace |
|---|---|
| `caddy/Caddyfile.local` | Sin nombre de dominio y con `auto_https off`: Caddy no intenta sacar ningún certificado. Sirve HTTP plano en el 8080 y **sigue pasando por el gateway**, nunca directo a product-service |
| `docker-compose.local.yml` | Monta ese Caddyfile y **reemplaza** los puertos (`!override`) para que el 80 y el 443 dejen de publicarse. El 8080 queda atado a `127.0.0.1` |

```bash
docker compose -f docker-compose.yml -f docker-compose.local.yml \
  --env-file .env --env-file cognito.env up -d
```

> **Se llama `.local.yml` y no `.override.yml` a propósito.** Compose carga
> `docker-compose.override.yml` solo, sin que nadie lo pida: bastaría con que el archivo existiera
> para que un `docker compose up` en la EC2 arrancara con la configuración de desarrollo sin
> avisar. Este hay que nombrarlo con `-f`, y eso lo hace imposible por accidente.

Nota sobre las variables: el `.env` local es de agosto y no tiene `COGNITO_CLIENT_IDS_VALIDOS`,
que vive en `cognito.env`. En vez de fusionar los archivos a mano se le pasan los dos a Compose con
`--env-file` repetido, que es más difícil de romper y no toca la configuración de nadie.

### El contrato, medido a través de la cadena completa

No copiado del código: pedido con `curl` al sistema corriendo.

| | Ruta | Código |
|---|---|---|
| **Anónimas** | `GET /health` · `/productos` · `/catalogos` · `/api/products` | **200** |
| | `GET /productos/{id}` con id inexistente | **404** |
| **Escritura sin token** | `POST` · `PUT` · `DELETE` en `/productos` y `/api/products` | **401** |
| **Identidad sin token** | `GET /api/yo` · `/api/admin/diagnostico` | **401** |
| **Seguimiento sin token** | `GET` · `POST` · `DELETE /seguimiento` | **401** |
| **Ruta inexistente** | `GET /no-existe-esta-ruta` | **404** |

**Los 401 son la evidencia del 40% del EP1**, y ahora están medidos sobre el sistema corriendo, no
argumentados desde el código. El 403 necesita un token real y queda para la Fase 5.

> ⚠️ `product-service` **no valida nada por su cuenta**: en el 8081 acepta escrituras sin
> credenciales. Lo protege la red, no la aplicación — el compose lo publica solo en `127.0.0.1`
> y en la EC2 igual. Es una decisión razonable para un servicio interno, pero conviene saber que
> es la única barrera: quien tenga una consola en esa máquina escribe en el catálogo.

### 🔴 Hallazgo: dos tiendas con la misma prenda, y una desaparece

Se cargaron tres productos de prueba, dos de ellos **la misma prenda en dos tiendas distintas** —
que es exactamente el dato que el scraper existe para producir. El frontend, con su capa de
servicios de verdad, hace esto:

```
  id=1  hym     $  8990  slug=polera-basica-de-algodon
  id=2  zara    $ 12990  slug=polera-basica-de-algodon     ← el mismo slug
  id=3  ripley  $ 39990  slug=jeans-corte-recto-azul

  --- resolución de slug, como la hace la ficha ---
  ✅ /producto/polera-basica-de-algodon  → llega a id=1 (se pedía id=1)
  ❌ /producto/polera-basica-de-algodon  → llega a id=2... no: llega a id=1
  ✅ /producto/jeans-corte-recto-azul    → llega a id=3 (se pedía id=3)
```

El slug se genera del **nombre**, y dos ofertas de la misma prenda tienen el mismo nombre. La
ficha resuelve con `productos.find(...)`, que devuelve **la primera**. La oferta de Zara no tiene
ninguna URL que lleve a ella: existe en el listado, y al hacer clic te lleva a la de H&M.

Esto cambia la naturaleza del problema que §7 tenía como 🟡 *«¿se agrupa por externalId?»*. No es
una funcionalidad pendiente: **es un defecto de corrección que aparece en cuanto hay datos reales**,
y encima aparece en silencio. También ensucia el `sitemap.xml`, que emitiría dos URLs idénticas.

Y refuerza lo que el [ADR-021](adr/021-contrato-publico-en-el-bff.md) dejó abierto: agrupar las
ofertas por `(store, externalId)` **es trabajo del BFF**, no del navegador. Un BFF que devuelve un
producto con sus ofertas dentro resuelve de una vez la comparación de precios, el slug único y el
sitemap. Hacerlo en el cliente obliga además a bajarse el catálogo entero.

**Decisión para la Fase 3/4**, y hay que tomarla antes de tocar el adaptador.
