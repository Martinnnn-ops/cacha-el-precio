# Cacha el Precio

**Comparador de precios de ropa y calzado en Chile.**
Muestra si el descuento que anuncia una tienda es real, comparando el mismo producto entre varias
tiendas y guardando el historial de precios día por día.

> Proyecto del ramo **DSY1107 · Desarrollo Cloud Native I** · Equipo de 3
> 🌐 En línea: **[cacha-el-precio.com](https://www.cacha-el-precio.com)** · Primera entrega: 13 de septiembre de 2026

---

## El problema

El SERNAC ya persiguió a Falabella y Paris por inflar precios antes del Cyber. En un caso, un
producto se promocionaba con **65% de descuento** usando como referencia $198.900, cuando días
antes esa referencia rondaba los $100.000. El descuento real era 30%.

Hoy el comprador no tiene forma de saber eso. **Cacha el Precio** existe para dárselo.

## Qué hace

- **Compara el mismo producto entre tiendas.**
- **Guarda el historial.** Tres capturas diarias, desde que el sistema arrancó el 27 de agosto.
- **Muestra el descuento real**, medido contra el mínimo que efectivamente observamos, no contra
  el precio de referencia que pone la tienda.
- **Avisa por talla.** Si el 42 está agotado, sirve saber dónde sí está.

No vendemos nada: la app informa y deriva a la tienda.

> ⚠️ **Sobre cómo se comparan dos productos.** El plan original usaba el *style code* de fábrica
> (ej. `HV9774`), que es idéntico en todas las tiendas. Medido contra el catálogo real el 27-08,
> **solo Nike lo publica** (96% de sus fichas); el resto de las marcas, 0%. Así que el style code
> sirve cuando está, pero no puede ser el mecanismo principal. El matching por texto tampoco
> basta solo: `574 Negra` vs `515 Negra` da 0,660 de similitud y **son zapatos distintos**. La
> conclusión medida está en [`docs/BITACORA.md`](docs/BITACORA.md) y el matcher todavía no está
> implementado.

## Estado, al 07-09-2026

Lo que está corriendo de verdad, no lo que está planificado.

| | Pieza | Estado |
|---|---|---|
| 🟢 | **Frontend** | En línea en `www.cacha-el-precio.com` (S3 + Cloudflare) |
| 🟢 | **API** | En línea en `api.cacha-el-precio.com` (EC2 + Caddy → `product-service`) |
| 🟢 | **Scraper de Sparta** | 3 capturas diarias desde el 27-08 |
| 🟢 | **Validación de JWT en el BFF** | Firma, emisor, vigencia, `client_id`, `token_use` y roles |
| 🟡 | **Identidad** | Cognito levantado por script, pero **hay dos user pools** que no se hablan |
| 🔴 | **API Manager** | No hay API Gateway todavía: hoy el rol lo cumple Caddy |
| 🔴 | **Base de datos** | SQLite en `product-service` y Postgres en el scraper. **La RDS no existe** |
| 🔴 | **CI** | Sin workflows |

> 📌 **La deuda técnica, con dueño y fecha, está en [`docs/INTEGRACION.md`](docs/INTEGRACION.md).**
> Ese documento es el que hay que leer antes de tocar nada: explica qué decisiones se tomaron
> construyendo, cuáles contradicen lo escrito, y qué está roto ahora mismo.

## Cómo está construido

```
   Frontend Vue                    ┌── Cognito (OIDC + PKCE)
   (S3 + Cloudflare)               │
        │                          │
        ▼                          ▼
   Caddy (TLS, enrutado)  ····▶  gateway/BFF  ── valida el JWT
        │                            │      │
        ▼                            ▼      ▼
   product-service              price-service
   (Micronaut, SQLite)          (Micronaut)
        ▲
        │ HTTP
   scraper-service ──▶ Postgres (esquema scraper) ──▶ S3 (imágenes)
   (Python, FastAPI)
```

⚠️ Las flechas punteadas son lo que **falta**: hoy Caddy le habla directo a `product-service` y
se salta el BFF, y la ingesta del scraper va por HTTP en vez de por RabbitMQ. Las dos cosas están
en [`docs/INTEGRACION.md`](docs/INTEGRACION.md).

| Capa | Tecnología |
|---|---|
| Backend | **Micronaut 5** · Java 25 · Maven — `gateway`, `product-service`, `price-service` |
| Scraper | **Python 3** · FastAPI — servicio aparte, fuera del monorepo Maven |
| Identidad | **AWS Cognito** — OIDC, Authorization Code + PKCE |
| API Manager | **AWS API Gateway** HTTP API con JWT Authorizer — *pendiente* |
| Mensajería | **RabbitMQ** con DLQ y reintentos — *levantado, todavía sin usar* |
| Datos | **SQLite** (`product-service`) · **PostgreSQL** (scraper) · **S3** (imágenes) |
| Frontend | **Vue 3** · Pinia · Vite — [repo aparte](https://github.com/Panditax727/Cacha-el-Precio-Frontend) |
| Infra | **EC2 con Docker Compose** ([ADR-008](docs/adr/008-ec2-docker-compose.md)) · S3 + Cloudflare |

Las razones detrás de cada decisión están en [`docs/ARQUITECTURA.md`](docs/ARQUITECTURA.md) y en
[`docs/adr/`](docs/adr/).

## Fuentes de datos

| Tienda | Cómo se obtiene | Estado |
|---|---|---|
| **Sparta** | Sitemap + JSON-LD de la ficha · también el capturador rápido de `tools/` | 🟢 Capturando |
| **Falabella · Paris · Ripley · Hites · Converse** | Sitemap + JSON-LD, un parser por tienda | 🟡 Escritos y con tests, sin correr en producción |
| Nike.cl | Requiere navegador headless | Post-MVP |

Se consultan únicamente **datos públicos de precio y stock**, con un request cada 1–2 segundos y
respetando `robots.txt`. Ver [`docs/PLAN.md`](docs/PLAN.md#8-consideraciones-legales-y-éticas).

## Documentación

| Documento | Para qué |
|---|---|
| [`docs/INTEGRACION.md`](docs/INTEGRACION.md) | **Empieza acá.** Qué está roto, qué se decidió construyendo y qué contradice lo escrito |
| [`docs/PLAN.md`](docs/PLAN.md) | La idea, el alcance, las tiendas, el modelo de datos, los riesgos |
| [`docs/ARQUITECTURA.md`](docs/ARQUITECTURA.md) | Por qué el sistema está armado así |
| [`docs/TAREAS.md`](docs/TAREAS.md) | Estado semana a semana y modo de trabajo |
| [`docs/EVALUACIONES.md`](docs/EVALUACIONES.md) | Qué pide el ramo y cómo lo cumplimos |
| [`docs/BITACORA.md`](docs/BITACORA.md) | Registro de avance. En septiembre **es** el informe |
| [`docs/REQUISITOS.md`](docs/REQUISITOS.md) | Historias de usuario, requisitos funcionales y no funcionales |
| [`docs/IDENTIDAD.md`](docs/IDENTIDAD.md) | Cómo funciona el login, cómo se valida el token y cómo se replica |
| [`docs/DESPLIEGUE.md`](docs/DESPLIEGUE.md) | En qué orden se despliega en AWS y las restricciones del Learner Lab |
| [`docs/MIGRACION.md`](docs/MIGRACION.md) | Cómo levantar todo en una cuenta de AWS nueva, y qué datos se pierden si no se respaldan |
| [`docs/adr/`](docs/adr/) | Decisiones de arquitectura, una por archivo |
| [`gateway/README.md`](gateway/README.md) | Qué valida el BFF y cómo se prueba |
| [`product-service/README.md`](product-service/README.md) | Las rutas de productos y catálogos, y el versionado por header |
| [`scraper-service/README.md`](scraper-service/README.md) | El scraper de Python: cómo se agrega una tienda |
| [`infra/db/README.md`](infra/db/README.md) | Las migraciones y por qué el modelo cambió contra datos reales |

## Cómo levantarlo

Requiere **JDK 25** y **Docker**. El Maven Wrapper descarga la versión de Maven del proyecto.

### 1. La infraestructura

```bash
cp .env.example .env      # y rellena las contraseñas
docker compose up -d
docker compose ps
```

Levanta Postgres 16, RabbitMQ 3.13, `product-service` y el `scraper-api`. Postgres y RabbitMQ
deben decir `healthy`; los otros dos, `running`. El panel de RabbitMQ queda en
`http://localhost:15672`. Es el mismo archivo que corre en la EC2
([ADR-008](docs/adr/008-ec2-docker-compose.md)).

### 2. Los servicios Java

```bash
./mvnw verify                      # compila y testea los tres módulos
./mvnw -pl gateway mn:run          # o el que necesites
```

| Servicio | Puerto | `/health` |
|---|---|---|
| `gateway` (BFF) | 8080 | `http://localhost:8080/health` |
| `product-service` | 8081 | `http://localhost:8081/health` |
| `price-service` | 8082 | `http://localhost:8082/health` |
| `scraper-api` (Python) | 8000 | `http://localhost:8000/health` |

El `gateway` necesita además las tres variables de Cognito. Ver
[`gateway/README.md`](gateway/README.md).

### 3. El scraper de arranque

Captura precios de Sparta 3 veces al día con un timer de systemd. Es el que tiene el historial
acumulado. Ver [`tools/scraper-rapido/README.md`](tools/scraper-rapido/README.md).

## Equipo

Tres estudiantes de Ingeniería en Informática.
