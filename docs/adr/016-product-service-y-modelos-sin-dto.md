# ADR-016 — Reemplazar catalog-service por product-service y partir sin DTO

**Estado:** aceptada

**Fecha:** 02-09-2026

## Contexto

El nombre `catalog-service` se estaba quedando chico porque el servicio será dueño tanto de los
catálogos como de los productos y, más adelante, de otros datos relacionados. Para esta primera
etapa también queremos una estructura conocida y fácil de explicar en clases.

## Decisión

- El servicio se llamará `product-service` y tendrá versión independiente `0.1.0`.
- El código de negocio se separará solamente en `model`, `repository`, `service` y `controller`.
- Los controllers recibirán y devolverán los modelos directamente, sin DTO por ahora.
- Las rutas HTTP se versionarán por el header `X-API-VERSION`.
- Los listados de productos y catálogos tendrán v1, v2 y v3 sobre la misma ruta.
- SQLite será la base local y los cambios de esquema se harán con Flyway.
- Swagger documentará HTTP y Protobuf quedará preparado para futuros contratos gRPC.

## Consecuencias

La ventaja es que hay menos clases y el flujo se entiende rápido. La desventaja es que el modelo
de base de datos queda acoplado a la respuesta HTTP. Si aparecen campos internos, permisos o una
respuesta distinta a la entidad, tendremos que introducir DTO antes de seguir agrandando la API.

SQLite sirve bien para esta etapa local, pero no reemplaza automáticamente las búsquedas avanzadas
que estaban pensadas para PostgreSQL con `pg_trgm`; eso se tendrá que reevaluar antes de producción.
