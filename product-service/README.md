# product-service

Microservicio dueño de los catálogos y productos canónicos de **Cacha el Precio**. Está hecho con
Micronaut, Maven y Java 25, manteniendo una estructura clásica para que sea fácil seguirle la pista:

```text
controller -> service -> repository -> SQLite
                    ^
                  model
```

Por ahora no usamos DTO. El controller recibe y devuelve `Catalogo` y `Producto` directamente. Es
una decisión válida para avanzar rápido, aunque significa que cambiar una entidad puede cambiar
también la API; si el contrato crece o aparecen campos internos, ahí sí conviene agregar DTO.

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

## Versionado HTTP

Las versiones se seleccionan con el header `X-API-VERSION`. Las tres usan la misma ruta para que
se vea claramente cómo evoluciona una operación sin romper la anterior:

```bash
curl -H 'X-API-VERSION: 1' http://localhost:8081/productos
curl -H 'X-API-VERSION: 2' http://localhost:8081/productos
curl -H 'X-API-VERSION: 3' 'http://localhost:8081/productos?soloActivos=true'
```

- `v1`: listado básico.
- `v2`: listado ordenado alfabéticamente.
- `v3`: filtros opcionales por `catalogoId` y estado activo.

Los catálogos siguen la misma idea: básico en v1, ordenado en v2 y búsqueda por nombre en v3.

## Base de datos

Flyway ejecuta las migraciones desde `src/main/resources/db/migration`. No edites las tablas a
mano: el siguiente cambio debería ser `V2__algo_que_explique_el_cambio.sql`.

## Protobuf

`src/main/proto/product.proto` ya se compila y genera fuentes Java. El archivo quedó sin RPC a
propósito; primero hay que acordar el contrato y recién después implementar un servidor gRPC.
