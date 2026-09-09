# Arquitectura de Cacha el Precio

> Describe el sistema vigente desde el 08-09-2026. El cambio desde Java/Micronaut y la
> simplificación de servicios se justifican en el
> [ADR-020](adr/020-csharp-y-simplificacion-de-servicios.md).

## Vista general

```text
Persona ──▶ Frontend Vue ──▶ API Gateway ──▶ Caddy ──▶ Gateway ASP.NET Core
               │                  │                         │
               └── Cognito ◀──────┴── valida JWT ──────────┘
                                                           │ HTTP v1
                                                           ▼
                                                   Product Service C#
                                                           │ esquema product
                                                           ▲
                                                           │ sincronización
Scraper Python ───────▶ PostgreSQL ◀────────────────────────┘
       │               esquema scraper
       │              (productos + historial) ─────────────┘
       └─────────────▶ S3 (imágenes)
```

> 🔴 **El diagrama de arriba es el diseño, no el tráfico real.** Verificado el 09-09:
> `frontend/.env.production` apunta a `api.cacha-el-precio.com`, ese nombre resuelve a la IP de la
> EC2, y **ningún archivo del repositorio menciona `execute-api`**. El API Gateway está creado y
> probado, pero **ninguna petición de usuario lo atraviesa**: el camino real es
> `navegador → Caddy → gateway`. Meterlo en la cadena es el primer punto de la v1, porque de él
> dependen tres indicadores del EP2 (validación del JWT en las rutas, rutas hacia los servicios, y
> CORS).

| Componente | Responsabilidad | Estado |
|---|---|---|
| API Gateway | JWT en el borde, CORS y stages | creado y probado, **fuera del camino real** |
| Caddy | TLS y proxy hacia el BFF | activo |
| Gateway | autorización de negocio y contrato público | ASP.NET Core 10 |
| Product Service | catálogo vigente y ofertas | ASP.NET Core 10 + EF Core + PostgreSQL |
| Scraper | extracción, normalización e historial | Python + PostgreSQL + S3 |
| Cognito | usuarios, login, grupos y tokens | existen dos pools por reconciliar |
| CDN del sitio | TLS y caché de los archivos estáticos | **Cloudflare**, no CloudFront ([ADR-023](adr/023-cloudflare-como-cdn.md)) |

## Principios

1. Cada dato tiene un dueño: el scraper conserva observaciones e historial; Product Service
   conserva el catálogo que consume la aplicación; Cognito conserva identidades.
2. Las bases no se comparten. La sincronización entre scraper y catálogo cruza una API HTTP.
3. El gateway es la única entrada de la aplicación. Product Service solo se publica en loopback
   para desarrollo y en la red interna de Compose.
4. Se crea un proceso independiente solo cuando posee reglas, datos o escalado propios.
5. Las decisiones responden a las capacidades reales del equipo, no a una preferencia universal
   por un lenguaje.

## Límites de servicio

### Gateway

Es un BFF porque adapta la API a las necesidades del frontend y centraliza la autorización. API
Gateway y el BFF no se duplican: el primero rechaza tokens inválidos en el borde; el segundo aplica
reglas que conocen la aplicación, como scopes y pertenencia a grupos.

Mantiene aliases `/productos` y `/catalogos` para no romper el frontend mientras migra al contrato
`/api/products`. El seguimiento está en memoria y es una limitación explícita: se pierde al
reiniciar y no permite múltiples réplicas coherentes.

### Product Service

Es dueño del catálogo vigente y no del historial. Un `Product` representa marca/modelo y agrupa
varias `ProductOffer`; cada oferta contiene tienda, identidad externa, precio actual, tallas,
URL, imagen y disponibilidad. `canonicalKey` intenta reconocer el mismo modelo entre tiendas y
`(Store, ExternalId)` evita duplicar una oferta. EF Core aplica las migraciones al arranque.

Usa PostgreSQL en el esquema `product`. Comparte servidor con el scraper para reducir operación,
pero no tablas ni acceso: cada servicio mantiene su esquema y cruza la frontera por HTTP. La
decisión y los costos del matching heurístico están en el ADR-021.

### Scraper

Python permanece porque sus librerías y el código existente resuelven extracción HTML/JSON-LD,
imágenes y acceso a datos. PostgreSQL conserva productos y observaciones históricas; S3 conserva
imágenes procesadas. Después de persistir, el adaptador HTTP crea o actualiza el catálogo de
Product Service.

Si Product Service está caído, el historial ya persistido no se pierde, pero el catálogo queda
temporalmente atrasado. La sincronización durable y los reintentos persistentes son deuda conocida.

### Por qué no existe Price Service

El proceso anterior no ejecutaba lógica ni poseía datos. Un contenedor vacío no crea desacople:
solo agrega despliegue, monitoreo y parches. El historial sigue dentro del scraper hasta que tenga
un caso de uso, carga o ciclo de vida que justifique extraerlo.

## Contratos

### Scraper → Product Service

- protocolo: HTTP interno;
- versión: encabezado `Version: 1.0`;
- listar: `GET /api/products`;
- crear: `POST /api/products`;
- actualizar: `PUT /api/products/{id}`;
- idempotencia: `(store, externalId)`.

El sincronizador carga el catálogo existente una vez por ejecución y decide crear o actualizar.
El índice único protege además contra duplicados persistidos. Una futura ingestión concurrente
debe traducir la violación del índice a un conflicto controlado o implementar un upsert atómico.

### Frontend → Gateway

Las lecturas del catálogo son públicas. Las escrituras requieren el scope configurado en
`COGNITO_WRITE_SCOPE`, cuyo valor actual es `https://api.cachaelprecio.cl/ingesta`. Las rutas de
seguimiento requieren sesión y el diagnóstico administrativo requiere el grupo `admin`.

## Seguridad

```text
JWT ──▶ API Gateway: firma + issuer + vigencia
  └──▶ Gateway: firma + issuer + vigencia + token_use + client_id + scope/grupo
```

El gateway exige `token_use=access`. Cognito no incluye `aud` en todos sus access tokens, por lo
que se valida `client_id` contra `COGNITO_CLIENT_IDS_VALIDOS`. La firma se obtiene a través de la
metadata del `Authority`; no se configura una URL JWKS duplicada.

Las credenciales nunca se versionan. `.env.example` solo define nombres y valores no secretos; el
despliegue usa variables de entorno o Secrets Manager.

## Persistencia

| Almacén | Dueño | Contenido | Motivo de permanencia |
|---|---|---|---|
| PostgreSQL `product` | Product Service | catálogo y ofertas actuales | multiwriter, índices y respaldo común |
| PostgreSQL `scraper` | Scraper | extracción e historial | consultas históricas y escrituras del scraper |
| S3 | Scraper | imágenes | binarios fuera de las bases relacionales |

Las antiguas migraciones `catalog` y `price` se eliminaron porque ningún proceso las consumía.
Compartir el servidor no comparte la propiedad del dato: ningún servicio consulta el esquema del otro.

## Comunicación síncrona y mensajería

RabbitMQ se retiró porque no tenía productores ni consumidores. Hoy la sincronización directa
representa el sistema con menos piezas y permite detectar el fallo en la misma ejecución.

El costo es real: no hay amortiguación de ráfagas, entrega durable ni DLQ. Se reintroducirá una
cola cuando exista más de un consumidor, cuando una caída de Product Service no pueda tolerar un
catálogo atrasado o cuando las ráfagas superen su capacidad.

## Tecnología

C# y ASP.NET Core son la base de los servicios de aplicación. En este equipo resultan preferibles
porque el responsable tiene más experiencia con C#, lo que reduce el riesgo de entrega y el tiempo
de diagnóstico. Además, el gateway y Product Service comparten una solución, configuración por
entorno, health checks, DI y el modelo asíncrono.

No se reescribe el scraper: Python sigue siendo una frontera útil y especializada. La arquitectura
no busca usar un solo lenguaje a cualquier costo, sino reducir variedad accidental donde no aporta.

## Despliegue

Docker Compose ejecuta en una EC2:

- Caddy;
- gateway;
- Product Service;
- scraper API;
- PostgreSQL.

Product Service y scraper usan el volumen de PostgreSQL con esquemas separados. Solo Caddy expone
la aplicación; sus puertos y PostgreSQL se atan a `127.0.0.1` al publicarse para diagnóstico.
API Gateway continúa delante de Caddy como API Manager.

La situación actual de red pública, los dos User Pools y los pasos de la cuenta AWS están en
[INTEGRACION](INTEGRACION.md), [IDENTIDAD](IDENTIDAD.md) y [DESPLIEGUE](DESPLIEGUE.md).

## Escalado y fallas

| Señal | Acción |
|---|---|
| latencia o CPU alta en gateway | agregar réplicas; antes persistir seguimiento fuera de memoria |
| matching canónico une o separa modelos mal | permitir `canonicalKey` manual y revisar candidatos |
| barridos saturan Product Service | limitar concurrencia y evaluar cola durable |
| historial crece | índices, particionado o retención en PostgreSQL |
| imágenes crecen | políticas de ciclo de vida y CDN sobre S3 |

## Registro de decisiones

| ADR | Decisión | Estado |
|---|---|---|
| [006](adr/006-cognito-como-idaas.md) | Cognito como IDaaS | vigente |
| [007](adr/007-catalogo-publico-sin-token.md) | catálogo público | vigente |
| [008](adr/008-ec2-docker-compose.md) | EC2 con Docker Compose | vigente |
| [013](adr/013-java-25-maven.md) | Java y Maven | reemplazado por ADR-020 |
| [014](adr/014-versionado-semantico-por-servicio.md) | versiones por servicio | vigente |
| [015](adr/015-red-privada-con-vpc-link.md) | red privada con VPC Link | destino pendiente |
| [016](adr/016-product-service-y-modelos-sin-dto.md) | límite de Product Service | reemplazado en parte |
| [018](adr/018-scraper-en-python.md) | scraper en Python | vigente |
| [019](adr/019-api-gateway-como-api-manager.md) | API Gateway y Caddy | vigente |
| [020](adr/020-csharp-y-simplificacion-de-servicios.md) | C# y retiro de procesos vacíos | vigente |
| [021](adr/021-contrato-publico-en-el-bff.md) | el contrato público es el del BFF | vigente |
| [022](adr/022-identidad-de-producto-entre-tiendas.md) | identidad de producto entre tiendas | propuesta, sin implementar |
| [023](adr/023-cloudflare-como-cdn.md) | Cloudflare como CDN | vigente |
| [024](adr/024-cors-en-el-api-manager.md) | CORS solo en el API Manager | vigente |
| [025](adr/025-catalogo-multi-oferta-postgresql.md) | catálogo multi-oferta y PostgreSQL | vigente |
