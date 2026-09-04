# product-service

Microservicio encargado de administrar los catálogos y productos de **Cacha el Precio**.

## Estado actual

- Versión del microservicio: `0.1.0`.
- Java 25, Micronaut y Maven.
- Persistencia con SQLite y migraciones de Flyway.
- API REST documentada con OpenAPI y Swagger UI.
- Contratos Protobuf preparados para integrar gRPC.
- Modelos utilizados directamente en las solicitudes y respuestas HTTP.

## Estructura

```text
controller -> service -> repository -> SQLite
                    ^
                  model
```

Por ahora no se utilizan DTO. Los controladores reciben y devuelven los modelos `Catalogo` y
`Producto` directamente. Esta decisión se puede revisar si aparecen campos internos o si el
contrato HTTP comienza a diferenciarse del modelo persistido.

## Ejecutar

El proyecto necesita que Maven use Java 25:

```bash
JAVA_HOME=/usr/lib/jvm/java-25-openjdk ./mvnw -pl product-service mn:run
```

- HTTP: `http://localhost:8081`
- gRPC: `localhost:50051`
- Swagger UI: `http://localhost:8081/swagger-ui/index.html`
- Health: `http://localhost:8081/health`
- SQLite: archivo local `product.db`

Se pueden cambiar con `PRODUCT_PORT`, `PRODUCT_GRPC_PORT` y `PRODUCT_DB_URL`.

## Docker

La imagen se construye desde la raíz del repositorio porque el módulo utiliza el `pom.xml` padre:

```bash
docker build \
  --file product-service/Dockerfile \
  --tag cachaelprecio/product-service:0.1.0 \
  .
```

Para ejecutar solamente este microservicio y conservar la base SQLite en un volumen:

```bash
docker run --rm \
  --name product-service \
  --publish 8081:8081 \
  --publish 50051:50051 \
  --volume product-service-data:/app/data \
  cachaelprecio/product-service:0.1.0
```

El servicio queda disponible en `http://localhost:8081`. Para detenerlo se utiliza `Ctrl + C`.

## Versionado HTTP

Las versiones se seleccionan mediante el header `X-API-VERSION` y utilizan el formato SemVer.
Los listados mantienen la misma ruta y cambian su comportamiento según la versión solicitada.

```bash
curl -H 'X-API-VERSION: 0.1.0' http://localhost:8081/productos
curl -H 'X-API-VERSION: 0.2.0' http://localhost:8081/productos
curl -H 'X-API-VERSION: 0.3.0' 'http://localhost:8081/productos?soloActivos=true'
```

| Versión | Productos | Catálogos |
| --- | --- | --- |
| `0.1.0` | Listado básico | Listado básico |
| `0.2.0` | Listado ordenado por nombre | Listado ordenado por nombre |
| `0.3.0` | Filtros por catálogo y estado | Filtro por nombre |

Las operaciones CRUD comienzan en la versión `0.1.0`.

## Base de datos

Flyway ejecuta las migraciones ubicadas en `src/main/resources/db/migration`. Cada cambio de
estructura debe agregarse en una migración nueva sin modificar las anteriores.

## Protobuf

El archivo `src/main/proto/product.proto` se compila y genera las fuentes Java. Los RPC se
agregarán cuando se defina el contrato de comunicación con los demás microservicios.
