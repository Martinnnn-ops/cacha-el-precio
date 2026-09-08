# Cacha el Precio

Comparador de precios de ropa y calzado en Chile. Reúne productos publicados por distintas
tiendas, conserva sus observaciones de precio y permite comprobar si un descuento es real.

> Proyecto de **DSY1107 · Desarrollo Cloud Native I**. La aplicación pública se encuentra en
> [cacha-el-precio.com](https://www.cacha-el-precio.com).

## Qué hace

- recopila productos públicos mediante scrapers específicos por tienda;
- mantiene el historial de precio y disponibilidad en PostgreSQL;
- publica el catálogo actual en Product Service;
- expone el catálogo al frontend a través de un gateway protegido con Cognito;
- conserva enlaces e imágenes para derivar a la tienda, sin vender productos.

## Arquitectura actual

```text
Frontend Vue ──▶ API Gateway ──▶ Caddy ──▶ Gateway ASP.NET Core
                                              │
                                              ▼
                                      Product Service C# ──▶ SQLite
                                              ▲
                                              │ HTTP / API v1
Scraper Python ──▶ PostgreSQL + S3 ────────────┘
```

Los servicios de aplicación se estandarizaron en **C# y ASP.NET Core 10**. El scraper permanece
en Python porque ese ecosistema es adecuado para extracción y procesamiento de datos. La elección
de C# no pretende ser universal: en este equipo reduce el riesgo de entrega porque su responsable
tiene más experiencia con C# que con Java, y permite compartir herramientas, configuración,
inyección de dependencias, autenticación y patrones asíncronos entre el gateway y Product Service.

Se retiraron `price-service`, RabbitMQ y las migraciones SQL de servicios que nunca las consumían.
`price-service` era un proceso vacío: desplegarlo aumentaba superficie operativa sin poseer una
capacidad real. El historial sigue siendo responsabilidad del scraper hasta que su carga o ciclo
de vida justifique separarlo. RabbitMQ se podrá reintroducir cuando exista un consumidor asíncrono
real y se necesiten reintentos o backpressure. La decisión completa y sus costos están en el
[ADR-020](docs/adr/020-csharp-y-simplificacion-de-servicios.md).

| Pieza | Tecnología | Persistencia |
|---|---|---|
| Gateway/BFF | ASP.NET Core 10 | seguimiento temporal en memoria |
| Product Service | ASP.NET Core 10 + EF Core | SQLite, propiedad del servicio |
| Scraper | Python 3.14 + FastAPI | PostgreSQL; imágenes en S3 |
| Identidad | AWS Cognito | administrada por AWS |
| Entrada pública | API Gateway + Caddy | — |
| Frontend | Vue 3 + Pinia + Vite | repositorio separado |

PostgreSQL no se eliminó porque el scraper sí lo usa para historial y datos de extracción. SQLite
evita infraestructura compartida para Product Service, pero sus límites de escritura concurrente
se deben reevaluar antes de escalar horizontalmente.

## Contrato entre scraper y Product Service

El scraper sincroniza por HTTP usando `Version: 1.0`. La identidad idempotente es el par
`(store, externalId)`, con un índice único en SQLite. Crea con `POST /api/products` y actualiza con
`PUT /api/products/{id}`; también envía marca, categoría, URL, imagen, descripción y estado.

## Requisitos

- .NET SDK 10;
- Python 3.14;
- `uv` o `pip` para el scraper;
- Docker y Docker Compose para levantar el entorno completo.

## Desarrollo local

Clona y restaura los servicios C#:

```bash
git clone https://github.com/Martinnnn-ops/cacha-el-precio.git
cd cacha-el-precio
dotnet restore CachaElPrecio.slnx
dotnet build CachaElPrecio.slnx
```

Ejecuta Product Service y el gateway en terminales separadas:

```bash
dotnet run --project product-service/Product-Service.csproj --launch-profile http
PRODUCT_URL=http://localhost:8081 dotnet run --project gateway/Gateway.csproj
```

Prepara el scraper:

```bash
cd scraper-service
uv sync --all-extras
uv run pytest -m "not red"
```

O levanta la solución con contenedores:

```bash
cp .env.example .env
# Completa DB_PASSWORD y la configuración de Cognito.
docker compose up --build -d
docker compose ps
```

| Servicio | Puerto local | Salud |
|---|---:|---|
| Gateway | 8080, a través de Caddy | `http://localhost:8080/health` |
| Product Service | 8081, solo loopback | `http://localhost:8081/health` |
| Scraper API | 8000, solo loopback | `http://localhost:8000/health` |
| PostgreSQL | 5432, solo loopback | healthcheck de Compose |

Product Service aplica automáticamente las migraciones EF Core al iniciar. Para administrarlas:

```bash
dotnet tool install --global dotnet-ef --version 10.0.11
dotnet ef migrations list --project product-service/Product-Service.csproj
dotnet ef database update --project product-service/Product-Service.csproj
dotnet ef migrations has-pending-model-changes --project product-service/Product-Service.csproj
```

## Comprobaciones antes de un PR

```bash
dotnet format CachaElPrecio.slnx --verify-no-changes --no-restore
dotnet build CachaElPrecio.slnx --no-restore
cd scraper-service
uv run pytest -m "not red"
uv run ruff check .
```

## Documentación

| Documento | Contenido |
|---|---|
| [ARQUITECTURA](docs/ARQUITECTURA.md) | vista técnica y límites de servicios |
| [ADR-020](docs/adr/020-csharp-y-simplificacion-de-servicios.md) | migración a C# y simplificación |
| [TAREAS](docs/TAREAS.md) | estado, prioridades y flujo de trabajo |
| [BITÁCORA](docs/BITACORA.md) | avances y evidencia cronológica |
| [INTEGRACIÓN](docs/INTEGRACION.md) | deuda descubierta al integrar ramas anteriores |
| [IDENTIDAD](docs/IDENTIDAD.md) | Cognito, tokens y autorización |
| [DESPLIEGUE](docs/DESPLIEGUE.md) | despliegue en AWS |
| [Product Service](product-service/README.md) | API, modelo y migraciones |
| [Gateway](gateway/README.md) | rutas, seguridad y configuración |
| [Scraper](scraper-service/README.md) | tiendas, persistencia y ejecución |

## Flujo Git

Las funcionalidades se desarrollan en `feature/*`, se revisan mediante PR hacia `development` y
solo después avanzan a `main`. Quien abre el PR no lo mezcla por su cuenta.
