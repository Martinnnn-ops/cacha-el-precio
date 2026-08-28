# Cacha el Precio

**Comparador de precios de calzado de marca en Chile.**
Muestra si el descuento que anuncia una tienda es real, comparando el mismo modelo entre varias
tiendas y guardando el historial de precios día por día.

> Proyecto del ramo **DSY1107 · Desarrollo Cloud Native I** · Equipo de 3
> Estado: **en desarrollo** · Primera entrega: 13 de septiembre de 2026

---

## El problema

El SERNAC ya persiguió a Falabella y Paris por inflar precios antes del Cyber. En un caso, un
producto se promocionaba con **65% de descuento** usando como referencia $198.900, cuando días
antes esa referencia rondaba los $100.000. El descuento real era 30%.

Hoy el comprador no tiene forma de saber eso. **Cacha el Precio** existe para dárselo.

## Qué hace

- **Compara el mismo modelo entre tiendas.** Las zapatillas de marca traen un *style code* de
  fábrica (ej. `HV9774`) que es el mismo en todas partes, así que se puede afirmar con certeza
  que es el mismo producto.
- **Guarda el historial.** Tres capturas diarias por tienda, desde que el sistema arrancó.
- **Muestra el descuento real**, medido contra el mínimo que efectivamente observamos, no contra
  el precio de referencia que pone la tienda.
- **Avisa por talla.** Si el 42 está agotado, sirve saber dónde sí está.

No vendemos nada: la app informa y deriva a la tienda.

## Cómo está construido

```
Frontend (S3 + CloudFront)
        │
        ▼
  API Gateway ──── Cognito (OIDC)
        │              ▲
        ▼              │ login con Google
   BFF (Micronaut)     │
    │        │
    ▼        ▼
 catalog   price  ◀── RabbitMQ ◀── scraper (Lambda)
    │        │                          │
    └────────┴──▶ RDS Postgres          └──▶ S3 (crudo)
```

| Capa | Tecnología |
|---|---|
| Backend | **Micronaut 5** · Java 25 · Maven · 4 microservicios |
| Identidad | **AWS Cognito** — OIDC, Authorization Code + PKCE, Google federado |
| API Manager | **AWS API Gateway** HTTP API con JWT Authorizer |
| Mensajería | **RabbitMQ** con DLQ y reintentos |
| Datos | **PostgreSQL** (RDS) + **S3** como fuente de verdad |
| Frontend | **React** + Vite |
| Infra | **ECS Fargate** · **Lambda** · GitHub Actions |

Las razones detrás de cada una de estas decisiones están en
[`docs/ARQUITECTURA.md`](docs/ARQUITECTURA.md).

## Fuentes de datos

| Tienda | Cómo se obtiene | Estado |
|---|---|---|
| **Sparta** | GraphQL público (Magento) — trae style code, precio y stock por talla | En el MVP |
| **Hites** | Grilla HTML parseada con Jsoup (Salesforce Commerce) | En el MVP |
| Nike.cl · Falabella · Ripley · Paris | Requieren navegador headless | Post-MVP |

Se consultan únicamente **datos públicos de precio y stock**, con un request cada 1–2 segundos y
respetando `robots.txt`. Ver [`docs/PLAN.md`](docs/PLAN.md#8-consideraciones-legales-y-éticas).

## Documentación

| Documento | Para qué |
|---|---|
| [`docs/PLAN.md`](docs/PLAN.md) | La idea, el alcance, las tiendas, el modelo de datos, los riesgos |
| [`docs/ARQUITECTURA.md`](docs/ARQUITECTURA.md) | Por qué el sistema está armado así |
| [`docs/TAREAS.md`](docs/TAREAS.md) | Plan semana a semana y modo de trabajo |
| [`docs/EVALUACIONES.md`](docs/EVALUACIONES.md) | Qué pide el ramo y cómo lo cumplimos |
| [`docs/BITACORA.md`](docs/BITACORA.md) | Registro semanal de avance |
| [`docs/REQUISITOS.md`](docs/REQUISITOS.md) | Historias de usuario, requisitos funcionales y no funcionales |
| [`docs/adr/`](docs/adr/) | Decisiones de arquitectura, una por archivo |
| [`infra/db/README.md`](infra/db/README.md) | Las migraciones y por qué el modelo cambió |

## Cómo levantarlo

Requiere **JDK 25** y **Docker**. El Maven Wrapper descarga la versión de Maven del proyecto.

### 1. La infraestructura

```bash
cp .env.example .env      # y rellena las contraseñas
docker compose up -d
docker compose ps         # los dos deben decir (healthy)
```

Levanta **Postgres 16** y **RabbitMQ 3.13**. El panel de RabbitMQ queda en
`http://localhost:15672`. Es el mismo archivo que corre en la EC2 en producción
([ADR-008](docs/adr/008-ec2-docker-compose.md)).

### 2. La base de datos

```bash
set -a && . ./.env && set +a
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=1 \
  < infra/db/catalog/V1__catalogo_base.sql
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=1 \
  < infra/db/price/V1__precios_base.sql
```

Detalle en [`infra/db/README.md`](infra/db/README.md).

### 3. Los servicios

```bash
./mvnw verify                      # compila y testea los 4
./mvnw -pl gateway mn:run          # o el que necesites
```

| Servicio | Puerto | `/health` |
|---|---|---|
| `gateway` (BFF) | 8080 | `http://localhost:8080/health` |
| `catalog-service` | 8081 | `http://localhost:8081/health` |
| `price-service` | 8082 | `http://localhost:8082/health` |
| `scraper-service` | 8083 | `http://localhost:8083/health` |

Los puertos salen del `.env`, así que el mismo build sirve en local y en la nube.

### 4. El scraper de arranque

Captura precios de Sparta 3 veces al día. Ver
[`tools/scraper-rapido/README.md`](tools/scraper-rapido/README.md).

## Equipo

Tres estudiantes de Ingeniería en Informática.
