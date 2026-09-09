# Integración de los PR #10 y #11 · deuda abierta

> **Qué es esto.** El 07-09 se integraron a `development` las dos ramas que venían trabajando
> Orion y Panditax ([#10](https://github.com/Martinnnn-ops/cacha-el-precio/pull/10) y
> [#11](https://github.com/Martinnnn-ops/cacha-el-precio/pull/11)). Se mergearon **con deuda
> conocida**: a tres días del code freeze, integrar y corregir arriba es más barato que dejar dos
> ramas divergiendo. Este documento es esa deuda, con dueño y fecha.
>
> Se lee en dos partes: **§1 qué decisiones cambiaron** (y cuáles no quedaron escritas) y
> **§2 qué hay que arreglar**, en orden de gravedad.
>
> Última revisión: **09-09-2026, integración de visitas con catálogo PostgreSQL**

## Actualización de ingesta e imágenes · 08-09

Falabella expone el sitemap, pero su WAF devuelve `403` al agente identificado cuando este
descarga fichas individuales. El scraper ahora usa páginas públicas de categoría y lee su
`__NEXT_DATA__`: seis peticiones entregaron 357 productos sincronizados, sin fallos, incluyendo
SKU, precio vigente, tallas e imagen. Product Service conserva la URL de imagen de origen cuando
S3 no está configurado, por lo que el frontend ya no depende de un almacenamiento opcional.

Converse sigue teniendo parser y pruebas, pero `robots.txt` declara `User-agent: *` y
`Disallow: /`; su sitemap también responde `403` al agente del proyecto. No se implementa una
suplantación de navegador. Para ingesta en vivo hace falta permiso o un feed autorizado.

## Actualización de catálogo y persistencia · 08-09

El [ADR-021](adr/021-catalogo-multi-oferta-postgresql.md) reemplaza la decisión transitoria de
SQLite: Product Service ahora usa el esquema PostgreSQL `product`, separado de `scraper` por
propiedad y migraciones. Un producto canónico agrupa múltiples ofertas identificadas por
`(store, externalId)`; las tallas son listas abiertas y admiten números.

Con esto quedan históricamente resueltos los puntos 3, 8 y la limitación de una oferta por fila.
Permanece el riesgo de matching heurístico de marca/modelo, mitigado con `canonicalKey` manual.

## Actualización de visitas y fecha de alta · 09-09

La función de visitas y `createdAt` que llegó a `development` sobre SQLite se conservó al traer
la versión remota: ahora vive en el producto canónico PostgreSQL. El incremento usa un `UPDATE`
atómico en el repositorio EF Core y la fecha se guarda como `timestamptz`; no se trasladó la
migración SQLite porque sus tipos `TEXT`/`INTEGER` no son válidos para este proveedor.

## Actualización arquitectónica · 08-09

El [ADR-020](adr/020-csharp-y-simplificacion-de-servicios.md) cierra varias contradicciones de
este documento con una decisión explícita:

- `gateway` y `product-service` pasan de Java/Micronaut a C# y ASP.NET Core;
- `price-service` se elimina porque estaba vacío y no poseía una capacidad real;
- RabbitMQ se retira mientras no existan productores ni consumidores;
- el scraper conserva PostgreSQL e historial y sincroniza por HTTP v1;
- Product Service evolucionó después a PostgreSQL y catálogo multi-oferta (ADR-021);
- las migraciones huérfanas de `catalog` y `price` se eliminan.

Por eso los puntos 5, 5b, 6 y 8 de abajo se conservan como evidencia histórica, pero ya tienen
una resolución documentada. Siguen abiertos el despliegue de esta rama, la validación con tokens
reales, la reconciliación de User Pools y la restricción de acceso directo a la EC2.

---

## 0. Lo que apareció al mirar el despliegue · 07-09, 21:00

> Esta sección se escribió **después** del resto: se descubrió al revisar el dominio, no al leer
> el código. **El sistema ya está en internet** y no estaba anotado en ninguna parte.

| Qué | Dónde | Estado verificado |
|---|---|---|
| **API** | `api.cacha-el-precio.com` → EC2 `44.196.131.41` (us-east-1), vía Caddy | 🟢 `/health` → `{"status":"UP"}` |
| **Frontend** | `www.cacha-el-precio.com` → S3 detrás de Cloudflare | 🟢 200 |
| **Datos** | 12 productos, 12 catálogos | 🟢 vivos |
| **Login** | PKCE real: `code_challenge`, `S256`, `state`, `identity_provider=Google` | 🟢 implementado |

**Eso es una buena noticia y hay que decirlo:** la fecha del 6-sep no se incumplió del todo. Hay
sistema desplegado, con dominio propio y TLS. Pero trae dos problemas que mandan sobre todo lo
demás de este documento.

### 🔴 0.1 · La escritura está abierta a internet, ahora

```
DELETE https://api.cacha-el-precio.com/productos/999999999  →  404
```

Se probó con un id inexistente justamente para no borrar nada. **404 y no 401** significa que la
petición llegó hasta la lógica de negocio sin pasar por ninguna autorización: cualquiera que
sepa la URL puede crear, editar y borrar el catálogo entero con un `curl`.

El punto 1 de la §2 de este documento deja de ser deuda y pasa a ser **un incidente abierto**.

> ⚠️ Ojo con `POST /productos/{id}/visitas`, que es de la misma tanda: **esa sí debe ser
> anónima**, porque el frontend cuenta vistas sin pedir login. El arreglo no es cerrar todos los
> `@Post` de un plumazo: es lectura y visitas anónimas, y CRUD solo para `admin` o el scope
> `ingesta`.

### 🔴 0.2 · Hay dos User Pools de Cognito, y no se hablan

| | Pool del script (`cognito.env`) | Pool del frontend desplegado |
|---|---|---|
| User pool | `us-east-1_cH76LiA02` | otro (dominio `us-east-1ji5w1jelx`) |
| Client | `61amk99kv70gndiupsfsebc49d` | `3ev76jdoin1ouc1grqfdi3laam` |
| Grupos `admin` / `usuario` | ✅ | ❓ |
| Resource server + scope `ingesta` | ✅ | ❓ |
| Client de `client_credentials` (scraper) | ✅ | ❓ |
| **Google federado** | 🔴 **no** | ✅ |

Los dos están vivos. Un user pool tiene **un solo** dominio, así que son pools distintos y no dos
clients del mismo.

**Por qué pasó, sin culpables:** `tools/crear-cognito.sh` **nunca creó el IdP de Google** — no
hay un solo `create-identity-provider` en el script. `TAREAS.md` lo tiene como casilla sin marcar
desde la Semana 1, pero `README.md` y `ARQUITECTURA.md` ya lo daban por hecho. Quien construyó el
frontend necesitaba login con Google, el pool no lo tenía, y levantó uno que sí.

**Por qué es lo más bloqueante que hay:** el BFF valida contra `COGNITO_ISSUER`. Si el frontend
emite tokens de otro pool, **todo token del frontend da 401**. El 60% y el 40% del EP1 estarían
validando identidades distintas y la demo end-to-end no existe.

> 💡 **Propuesta para la reunión** (no decidida todavía): gana el **pool del script**, porque es
> el único con grupos `admin`/`usuario` —sin ellos no hay 403, que es el 20% del EP2—, con
> resource server y con el client de `client_credentials` del scraper. Le falta Google, y eso
> **se le agrega**; los grupos y el resource server no se le agregan al otro sin rehacerlos.
> Costo: dos constantes en el frontend, más agregarle Google al script y la callback de
> producción (el paso 7 de `DESPLIEGUE.md`, que nunca se hizo).

### 0.3 · Tres contradicciones más entre lo escrito y lo desplegado

| El documento dice | El despliegue hace |
|---|---|
| `TAREAS` / `ARQUITECTURA`: frontend en **React** con `react-oidc-context` | Es **Vue**, con el PKCE escrito a mano |
| `ADR-015`: EC2 en subred privada detrás de VPC Link | EC2 con **IP pública directa** |
| `README`: "Google federado" en nuestro pool | El script no lo crea |

Lo de Vue **no es un problema de nota** —la rúbrica pide una librería certificada OIDC, no React—
pero sí es un problema de informe: hoy los documentos describen un sistema que no es el que está
corriendo.

### 0.4 · Un riesgo operativo para el día de la demo

Esa EC2 es del **Learner Lab**: se apaga sola cuando el laboratorio se cierra y **la IP pública
cambia al reiniciar**. Si el DNS de Cloudflare apunta a una IP fija, el sitio se cae solo entre
sesiones. Hay que resolverlo con una **Elastic IP** antes del ensayo, o la demo empieza con el
dominio caído.

> ⚠️ **Corrección del 09-09.** Esto describe solo la mitad del problema. La instancia vive en la
> cuenta de AWS de un compañero y **no se mantiene encendida 24/7 por decisión propia**, por
> créditos limitados. Que `api.cacha-el-precio.com` no responda fuera de las sesiones de trabajo es
> lo normal, no una avería.
>
> Una Elastic IP evita que la dirección cambie al reiniciar, pero **no enciende la instancia**. Son
> dos problemas distintos, y el segundo es de coordinación, no de infraestructura: el día del
> ensayo y el de la demo, alguien tiene que haberla levantado. Conviene decirlo así en el informe
> en vez de presentarlo como un servicio caído.

---

## 1. Las decisiones que cambiaron en el camino

Los dos PR no solo traen código: traen **ocho decisiones de arquitectura**, y solo una está
escrita en un ADR. Esto importa más de lo que parece, porque el informe del EP1 se califica
justamente por *justificar las elecciones*, y en la defensa un ADR **es** la respuesta a
«¿por qué?».

| # | Decisión | ¿Documentada? | ¿Choca con algo ya decidido? |
|---|---|---|---|
| 1 | `catalog-service` → `product-service`, dueño de catálogos **y** productos | ✅ ADR-016 | No. Docs alineados de forma consistente |
| 2 | Sin DTO: los controllers devuelven las entidades | ✅ ADR-016, con el costo anotado | No |
| 3 | **SQLite** como base de `product-service` | ⚠️ Mencionada al pasar en ADR-016 | 🔴 **Sí** — ADR-010: una RDS Postgres con un esquema por servicio |
| 4 | **Versionado de rutas por header** `X-API-VERSION` | ❌ No | No, pero ADR-014 es sobre versionar *artefactos*, no rutas. Es otra decisión |
| 5 | El scraper deja de ser **Java** y pasa a **Python/FastAPI** | ❌ No | 🔴 **Sí** — ADR-013: Java 25 + Maven como base del backend |
| 6 | **Caddy** como reverse proxy con TLS y CORS | ❌ No | 🔴 **Sí** — ADR-015 y la rúbrica: el API Manager es API Gateway de AWS |
| 7 | La ingesta va **HTTP directo** scraper → `product-service` | ❌ No | 🔴 **Sí** — ARQUITECTURA §6: la ingesta pasa por RabbitMQ |
| 8 | El mismo producto vive **duplicado** en Postgres y en SQLite | ❌ No | 🔴 **Sí** — deja a `price-service` sin dueño del historial |
| 9 | El alcance pasa de **solo calzado** a **ropa y calzado** | ❌ No | 🔴 **Sí** — `PLAN.md` §2 dice que la ropa entra *después* del EP1 |

### Lo que hay que entender de esta tabla

**No es que las decisiones estén mal.** Varias son razonables: SQLite arranca en segundos y no
pide contenedor, Python tiene el mejor ecosistema de scraping que existe, y Caddy resuelve TLS
con dos líneas. El problema es otro y es doble:

**a) Cuatro de ellas contradicen algo que ya está escrito y que vamos a defender oralmente.**
Si en la presentación alguien pregunta *«¿por qué mensajería y no llamadas directas?»*,
`ARQUITECTURA.md` §6 tiene tres párrafos de respuesta —y el código hace exactamente lo
contrario—. Eso no se improvisa en la defensa: o se cambia el código, o se cambia el documento
y se explica por qué se cambió de opinión. **Las dos salidas sirven; quedarse callado, no.**

**b) La #6 y la #7 tocan lo que se califica.** Caddy haciendo CORS y TLS delante de
`product-service` es, funcionalmente, un API Manager chico. La rúbrica pide ver el **API Gateway
de AWS validando el JWT en el borde** (20% del EP2) y **el CORS configurado en el API Manager**
(7% del EP2). Si el tráfico real entra por Caddy, esos dos indicadores se muestran sobre una
pieza que no es la que dijimos que íbamos a usar.

### Lo que falta escribir

Tres ADR, y son cortos porque la decisión ya está tomada — solo hay que dejar el porqué:

- **ADR-017 · Versionado de rutas HTTP por header.** Qué versiona cada cosa y por qué el header
  y no la URL. *(Orion)*
- **ADR-018 · El scraper sale de Java y pasa a Python.** Qué se gana, qué se pierde, y por qué
  esto no invalida el ADR-013 para el resto del backend. *(Panditax)*
- **ADR-019 · Quién es el API Manager, y qué hace Caddy.** Es el que más peso tiene en la nota;
  ver el punto 2 y el punto 4 de la lista de abajo. *(el equipo)*

Y dos ADR existentes hay que marcarlos: **ADR-010** (reemplazado en parte por el uso de SQLite)
y **ADR-013** (acotado a los servicios Java).

---

## 2. Lo que hay que arreglar, en orden

### 🔴 1 · `/productos` acepta escrituras de cualquiera

`ProductoController` expone `POST /productos`, `PUT /productos/{id}` y `DELETE /productos/{id}`
**sin ninguna anotación de seguridad**, y el `Caddyfile` publica ese servicio en
`api.cacha-el-precio.com`. Tal como está, cualquiera en internet puede crear, modificar y borrar
el catálogo.

Hoy no hay agujero real porque nada de esto está desplegado. **Pero es el commit que no puede
llegar a la EC2 sin arreglarse**, y arreglarlo es justamente el 40% del EP1.

**Cómo se arregla:** `micronaut-security-jwt` en `product-service`, lectura anónima
(`@Secured(IS_ANONYMOUS)` en los `@Get`) y escritura solo para el grupo `admin` o el scope
`ingesta` (`@Secured("admin")`). El scraper deja de llamar sin credenciales y usa el app client
de **client credentials** que ya existe (`COGNITO_SCRAPER_CLIENT_ID` en `cognito.env`).

**Dueño:** Martín · **Antes de:** cualquier despliegue.

---

### 🔴 2 · Caddy le pega directo a `product-service` y se salta el BFF

> ✅ **Avance del 07-09 por la noche: el API Gateway ya existe y funciona.**
> `tools/crear-api-gateway.sh` levanta la HTTP API con JWT Authorizer, CORS de orígenes
> explícitos y los stages `dev`/`prod`. Probado con un token real: **200 / 401 / 403** en el
> borde. Ver [`ADR-019`](adr/019-api-gateway-como-api-manager.md) y la evidencia en
> [`evidencia/`](evidencia/).
>
> ✅ **08-09: resuelto en código.** Caddy ahora apunta al `gateway`, que entró al compose con su
> propio `Dockerfile` y expone las mismas rutas que el frontend ya llamaba, consultando a
> `product-service`. La cadena queda `API Gateway → Caddy → gateway → product-service`.
>
> ⚠️ **Falta desplegarlo**, y toca `docker-compose.yml`, que es de Panditax.
> Y la EC2 sigue siendo alcanzable directo, así que **el punto 1 sigue abierto**.

El `Caddyfile` enruta `api.cacha-el-precio.com` → `product-service:8081`. El `gateway` no
aparece en la cadena. Si el tráfico no pasa por el BFF, **el 40% del EP1 no se puede demostrar**:
no hay dónde mostrar la validación de `iss`, `client_id`, firma y vigencia.

**Cómo se arregla:** la cadena queda
`API Gateway (AWS) → Caddy → gateway (BFF) → product-service / price-service`. Caddy se queda
solo con TLS y el enrutamiento; el CORS y la validación del token suben al API Gateway y al BFF.

**Dueño:** Martín + Panditax · **Antes de:** el despliegue.

---

### ✅ 3 · El puerto 8080 estaba pedido dos veces — resuelto

Caddy publicaba el 8080 al host y el `gateway` también usa el 8080. Resuelto al meter el gateway
al compose: **no publica ningún puerto al host**. Solo Caddy le habla, por la red interna, donde
`gateway:8080` no choca con nada.

Y es más que un arreglo de puertos: si el gateway publicara un puerto, existiría una puerta que
esquiva a Caddy y al API Gateway. Una validación que se puede esquivar no es una validación.

<details>
<summary>El texto original</summary>

### 🟠 3 · El puerto 8080 está pedido dos veces

Caddy publica `8080:8080` y `GATEWAY_PORT` también es 8080. Hoy no chocan porque el `gateway`
todavía no está en el compose; van a chocar el día que se agregue, con un error que no dice por
qué. Se resuelve solo si se arregla el punto 2 (Caddy deja de necesitar ese puerto interno).

**Dueño:** Martín · **Antes de:** subir el `gateway` al compose.

</details>

---

### 🟠 4 · Dos dominios distintos, y ninguno verificado

| Dónde | Valor |
|---|---|
| `caddy/Caddyfile` | `api.cacha-el-precio.com` · `https://www.cacha-el-precio.com` |
| `docs/IDENTIDAD.md` (resource server de Cognito) | `https://api.cachaelprecio.cl` |

Son dominios **distintos**, con guiones y TLD distintos. Además Caddy pide certificado por
ACME al arrancar: **si ese dominio no está registrado y apuntando a la EC2, Caddy no levanta**.

Hay que elegir uno, y elegir rápido: el identificador del resource server de Cognito **no se
puede cambiar** una vez creado — habría que recrearlo y volver a repartir los scopes.

> 💡 Lo barato: si no hay dominio comprado, Caddy va con la IP pública o el DNS de la EC2 y TLS
> interno, y `https://api.cachaelprecio.cl` se queda como **identificador lógico** del resource
> server, que es lo que Cognito realmente necesita (nunca lo resuelve por DNS).

**Dueño:** el equipo · **Antes de:** el despliegue.

---

### 🟠 5 · La ingesta se saltó RabbitMQ

`ProductServiceSync` llama por HTTP directo a `product-service`. `ARQUITECTURA.md` §6 dedica
tres argumentos a explicar por qué eso **no** se hace: si el servicio está caído la captura se
pierde, y son 8 horas de hueco en el historial que no se recuperan.

Son dos salidas y las dos valen:

- **Publicar en la cola** `ofertas.crudas` y que `product-service` consuma. Es lo escrito.
- **Dejarlo en HTTP** y reescribir §6 diciendo que la cola se pospuso, con el motivo.

Lo que no sirve es la situación de ahora: el documento dice una cosa y el código hace otra, y en
la defensa esa contradicción la encuentra cualquiera que mire los dos.

**Dueño:** Panditax · **Antes de:** el code freeze (10-09).
🗓️ **Se conversa en la reunión del 08-09.** Ataca directo el argumento de arquitectura, así que
no es una decisión que pueda tomar una persona sola.

---

### 🟠 5b · `price-service` está vacío, y eso sí debilita la defensa

Es el otro lado de la misma moneda que el punto 5, y salió al revisar cómo defendemos la
arquitectura.

`ARQUITECTURA.md` §3 responde a «¿por qué `price` separado de `product`?» con un buen argumento:
*las cargas son genuinamente distintas — escritura masiva en ráfaga tres veces al día contra
lectura constante*. El problema es que **ahí no corre nada**: `price-service` arranca, responde
`/health` y se acaba. El historial de precios lo está guardando el scraper en su propio esquema
de Postgres.

**Un servicio vacío es más difícil de defender que un servicio en otro lenguaje.** Si en la
presentación preguntan por qué hay cuatro servicios y uno no hace nada, la separación se lee
como anticipación de algo que no llegó, no como diseño.

Las salidas, y las dos sirven:

- **Moverle una responsabilidad chica**, aunque sea una: que `price-service` sea el que lee
  `scraper.price_history` y expone el historial de un producto. Es un endpoint, y de paso es el
  que alimenta el gráfico de la demo.
- **Decirlo antes de que lo pregunten**: "`price-service` está definido y desplegado pero su
  lógica es EP3; hoy el historial lo escribe el scraper". Honesto y se sostiene.

Lo que no sirve es presentar cuatro cajas en el diagrama y que una esté vacía sin mencionarlo.

**Dueño:** ⬜ · 🗓️ **Se conversa en la reunión del 08-09.**

---

### 🟡 6 · El mismo producto vive en dos bases

El scraper guarda en `scraper.products` (Postgres) y después lo copia por HTTP a la SQLite de
`product-service`, deduplicando por URL porque no hay id externo. Dos consecuencias:

- El **historial de precios** (`scraper.price_history`) queda solo en Postgres, mientras que
  `price-service` —que según todos los documentos es el dueño del historial— sigue vacío. El
  gráfico de la demo hay que sacarlo de algún lado, y hoy no está claro de cuál.
- El `precio` de `product-service` es un valor único, no una serie. Ahí no hay descuento real
  que calcular.

No es urgente para el EP1 (el historial no tiene indicador propio), pero **sí decide de dónde
sale el gráfico de la demo**.

**Dueño:** el equipo · **Antes de:** armar la demo.

---

### 🟡 7 · Dos scrapers de Sparta, y el que tiene los datos es el viejo

Conviven `tools/scraper-rapido/sparta.py` (el que corre con timer de systemd desde el 27-08 y
**es el único que tiene 14 días de historial acumulado**) y el nuevo `SpartaScraper` de Python.
Nadie los conectó ni se decidió cuál muere.

⚠️ Cuidado al apagar el viejo: los datos que ya capturó son irremplazables, no se pueden
recuperar hacia atrás.

**Dueño:** Panditax + Martín · **Antes de:** el code freeze.

---

### 🟡 8 · `product-service` usa SQLite, no el modelo validado con datos reales

El modelo que se validó contra los 2.088 productos reales —con `pg_trgm`, `unaccent` y los
esquemas `catalog` y `price`— sigue en `infra/db/` sin que nadie lo use. `product-service` tiene
en su lugar un CRUD de `catalogos` + `productos` con un solo precio.

El propio ADR-016 lo dice: *«SQLite no reemplaza las búsquedas avanzadas pensadas para
PostgreSQL con pg_trgm; eso se tendrá que reevaluar antes de producción»*. Esto es esa
reevaluación, y hay que hacerla **después** del EP1: cambiar de motor a tres días del freeze es
como se pierde una entrega.

**Dueño:** Orion · **Después de:** la entrega del 13-09.

---

### 🟢 9 · 7 MB de HTML de terceros en el repo

`scraper-service/tests/fixtures/` trae 7 MB de páginas descargadas de las tiendas. Sirven como
fixtures reales y por eso los tests valen, pero:

- Dentro viene incrustada una **API key de Google Maps de Hites** y un token de sesión. No son
  secretos nuestros, pero el día que se enchufe **gitleaks** al CI se va a poner rojo, y hay que
  saber de antemano que es un falso positivo.
- Es HTML con copyright de las tiendas, versionado en un repo.

**Cómo se arregla, cuando se pueda:** recortar cada fixture al fragmento que el parser realmente
lee (suelen ser el `<script type="application/ld+json">` y poco más). Bajan de MB a KB y los
tests siguen valiendo lo mismo.

**Dueño:** Panditax · **Después de:** la entrega.

---

## 3. Lo que sigue sin dueño y no puede esperar

Nada de lo de arriba es tan urgente como esto:

| Falta | Peso | Estado |
|---|---|---|
| ~~**Frontend con OIDC**~~ | **60% del EP1** | ✅ **Existe y está en el monorepo.** Lo hizo Panditax en su repositorio; entró con `git subtree` el 09-09 conservando sus 15 commits. Ver [`INTEGRACION-FRONTEND.md`](INTEGRACION-FRONTEND.md) |
| **CI en GitHub Actions** | — | 0 workflows, arrastrado desde la semana 1. `npm run humo` es hoy lo más parecido a un CI que hay |
| **Despliegue en AWS** | requisito para entregar | La fecha era el 6-09. Hay sistema desplegado, pero **con el contrato viejo**: nadie ha subido todavía el backend C# ni el frontend nuevo |

> **Actualización del 09-09.** Cuando se escribió esta tabla, el frontend «no lo estaba haciendo
> nadie». Sí lo estaba haciendo Panditax, en su propio repositorio, y este documento no se enteró.
> Es el mismo problema que el archivo describe una y otra vez: tres personas trabajando sin cruzar
> lo que hace cada una.
