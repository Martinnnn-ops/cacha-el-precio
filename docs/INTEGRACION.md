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
> Última revisión: **07-09-2026**

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

El `Caddyfile` enruta `api.cacha-el-precio.com` → `product-service:8081`. El `gateway` no
aparece en la cadena. Si el tráfico no pasa por el BFF, **el 40% del EP1 no se puede demostrar**:
no hay dónde mostrar la validación de `iss`, `client_id`, firma y vigencia.

**Cómo se arregla:** la cadena queda
`API Gateway (AWS) → Caddy → gateway (BFF) → product-service / price-service`. Caddy se queda
solo con TLS y el enrutamiento; el CORS y la validación del token suben al API Gateway y al BFF.

**Dueño:** Martín + Panditax · **Antes de:** el despliegue.

---

### 🟠 3 · El puerto 8080 está pedido dos veces

Caddy publica `8080:8080` y `GATEWAY_PORT` también es 8080. Hoy no chocan porque el `gateway`
todavía no está en el compose; van a chocar el día que se agregue, con un error que no dice por
qué. Se resuelve solo si se arregla el punto 2 (Caddy deja de necesitar ese puerto interno).

**Dueño:** Martín · **Antes de:** subir el `gateway` al compose.

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
| **Frontend con OIDC** | **60% del EP1** | No existe ni un `package.json`. **Sin dueño** |
| **CI en GitHub Actions** | — | 0 workflows, arrastrado desde la semana 1 |
| **Despliegue en AWS** | requisito para entregar | La fecha era el 6-09. Venció |

El frontend es la mitad más grande de la nota del EP1 y hoy no lo está haciendo nadie. Si sigue
sin dueño 24 horas más, no llega al freeze del 10.
