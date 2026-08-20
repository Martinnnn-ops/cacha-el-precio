# catalog-service

Microservicio responsable de los modelos canónicos y, posteriormente, del matching entre los
productos encontrados en las tiendas.

## Estado actual

- Micronaut 5.1.1, Java 25 y Maven.
- Versión independiente del servicio: `0.1.0`.
- Puerto local configurable mediante `CATALOG_PORT`; por defecto usa `8081`.
- Rutas versionadas mediante headers HTTP.
- Sin base de datos, matching ni mensajería todavía.

## Estructura

```text
catalog-service/
├── pom.xml                              # Dependencias y versión del servicio
└── src/
    ├── main/
    │   ├── java/cl/cachaelprecio/catalog/
    │   │   ├── Application.java         # Punto de entrada de Micronaut
    │   │   ├── controller/
    │   │   │   ├── CatalogController.java # Información y ejemplo de versiones
    │   │   │   └── ModeloController.java  # Operaciones de modelos
    │   │   └── dto/
    │   │       └── ModelResumeResponse.java # Contrato JSON resumido
    │   └── resources/
    │       ├── application.properties        # Puerto, health y versionado
    │       └── logback.xml                   # Configuración de logs
    └── test/
        └── java/cl/cachaelprecio/catalog/
            └── ApplicationTest.java       # Verifica el arranque del contexto
```

## Ejecución

Desde la raíz del repositorio, con JDK 25 activo:

```bash
./mvnw -pl catalog-service mn:run
```

Verificación del servicio:

```bash
curl http://localhost:8081/health
```

## Rutas actuales

El header principal para seleccionar una versión es `X-API-VERSION`.

```bash
curl -H "X-API-VERSION: 0.1.0" http://localhost:8081/catalog/info
curl -H "X-API-VERSION: 0.2.0" http://localhost:8081/catalog/info
curl -H "X-API-VERSION: 0.1.0" http://localhost:8081/catalog/modelos
```

`GET /catalog/modelos` devuelve por ahora una lista vacía. La persistencia y la lógica de
dominio se agregarán en incrementos posteriores.
