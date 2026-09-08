# ADR-020 · C# para los servicios de aplicación y eliminación de procesos vacíos

**Fecha:** 08-09-2026

**Estado:** aceptada

## Contexto

El diseño inicial proponía `gateway`, `product-service` y `price-service` con Java, Micronaut y
Maven, más RabbitMQ como intermediario. La implementación real se apartó de ese diseño:

- Product Service ya utilizaba SQLite y el nuevo catálogo funcional fue desarrollado en C#;
- el scraper pasó a Python y sincronizaba productos directamente por HTTP;
- `price-service` solo arrancaba y respondía salud, sin ser dueño de datos ni ejecutar reglas;
- RabbitMQ no tenía productores ni consumidores;
- el responsable de estos servicios tiene más experiencia operando y depurando C# que Java.

Mantener los procesos únicamente para hacer coincidir el diagrama aumentaba imágenes, puertos,
parches de seguridad y caminos de falla sin aportar una capacidad observable.

## Alternativas consideradas

- **Conservar Java/Micronaut y portar el Product Service C# a Java.** Mantiene los ADR antiguos,
  pero desecha trabajo funcional y obliga al responsable a mantener una tecnología en la que tiene
  menos experiencia justo antes de la entrega.
- **Mantener un backend mixto.** Evita reescribir el gateway, pero conserva dos cadenas de build,
  dos ecosistemas de dependencias y patrones distintos para configuración y observabilidad.
- **Migrar gateway y Product Service a C# y simplificar los servicios.** Exige adaptar contratos y
  volver a validar Cognito, pero deja una base más pequeña y coherente con la experiencia real.

## Decisión

Se adopta **.NET 10, C# y ASP.NET Core** para `gateway` y `product-service`. EF Core administra el
esquema SQLite de Product Service y sus migraciones se aplican al inicio. El gateway conserva la
doble validación de Cognito y la autorización por scope o grupo.

El scraper permanece en **Python** y conserva PostgreSQL porque allí mantiene productos extraídos,
observaciones e historial. Sincroniza el catálogo vigente hacia Product Service mediante HTTP v1;
el par `(store, externalId)` es su identidad idempotente. SQLite y PostgreSQL no se comparten entre
servicios.

Se elimina `price-service`. Hoy el historial ya pertenece al scraper y el catálogo vigente a
Product Service; un servicio sin una capacidad independiente no constituye un límite de dominio.
Se reconsiderará separarlo cuando el historial tenga reglas, carga o escalado propios.

Se retira RabbitMQ porque no existían productores ni consumidores. La llamada HTTP directa refleja
el comportamiento actual con menos infraestructura. También se eliminan las migraciones SQL
huérfanas de `catalog` y `price`; PostgreSQL se conserva exclusivamente para el scraper.

Esta decisión reemplaza al [ADR-013](013-java-25-maven.md) para los servicios activos y reemplaza
la decisión proyectada de mensajería del ADR-004. El [ADR-018](018-scraper-en-python.md) sigue
vigente.

## Por qué C# es mejor en este contexto

No se sostiene que C# sea intrínsecamente mejor que Java. Es la mejor elección **para este equipo
y este estado del proyecto** porque:

- la experiencia previa del responsable reduce tiempos de diagnóstico y riesgo de mantenimiento;
- ASP.NET Core entrega DI, configuración por entorno, health checks, OpenAPI y autenticación en un
  conjunto coherente;
- EF Core mantiene el modelo y las migraciones SQLite junto al código que los usa;
- `async`/`await`, tipos anulables y una solución común facilitan seguir el flujo gateway-producto;
- las imágenes oficiales de .NET permiten builds reproducibles y ejecución sin privilegios.

## Consecuencias

### Positivas

- una solución y un comando compilan los dos servicios de aplicación;
- desaparecen un proceso vacío, una cola ociosa y migraciones sin consumidor;
- el contrato del scraper queda explícito y probado;
- se reduce la cantidad de tecnologías que el equipo debe diagnosticar en producción.

### Costos y riesgos

- la seguridad del gateway reescrita debe probarse con tokens reales del Cognito desplegado;
- HTTP directo no ofrece buffering, reintentos durables ni backpressure como una cola;
- SQLite limita la escritura concurrente y no permite escalar Product Service horizontalmente sin
  cambiar la persistencia;
- el seguimiento del gateway continúa en memoria y se pierde al reiniciar;
- Product Service vive también como repositorio independiente, por lo que se debe evitar que ambas
  copias diverjan;
- PostgreSQL sigue siendo necesario mientras el scraper sea dueño del historial.

## Condiciones para revisar la decisión

- reintroducir mensajería cuando haya más de un consumidor o la ingesta requiera entrega durable;
- extraer precios cuando exista una capacidad de negocio independiente y medible;
- migrar SQLite cuando la concurrencia, alta disponibilidad o réplica horizontal lo exijan;
- evaluar una versión LTS de .NET antes de producción de largo plazo.
