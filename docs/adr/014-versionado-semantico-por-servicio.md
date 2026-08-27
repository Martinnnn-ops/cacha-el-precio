# ADR-014 · Versionado semántico independiente por microservicio

**Fecha:** 19-08-2026

**Estado:** aceptada

## Contexto

Los microservicios se desarrollarán y desplegarán a ritmos distintos. Heredar una versión única
del proyecto obligaría a cambiar la versión de servicios que no participaron en una entrega.

## Alternativas consideradas

- **Una versión para todo el monorepo:** simplifica las entregas, pero no representa el avance
  independiente de cada servicio.
- **Versiones independientes sin una regla común:** entrega autonomía, pero vuelve ambiguo el
  significado de cada incremento.
- **SemVer independiente por servicio:** mantiene la autonomía y comunica el impacto de los
  cambios mediante `MAJOR.MINOR.PATCH`.

## Decisión

Cada microservicio declara su propia versión en su `pom.xml` y sigue Semantic Versioning 2.0.0.
`catalog-service` comienza en `0.1.0`, correspondiente a su desarrollo inicial. La versión del
`pom.xml` raíz identifica solamente al agregador y no determina la versión de los servicios.

## Consecuencias

- Un servicio puede publicar una nueva versión sin cambiar las versiones de los demás.
- El equipo debe mantener la versión de cada módulo de forma explícita.
- Una versión publicada o etiquetada no se modifica; el siguiente cambio requiere otra versión.
- Mientras la versión mayor sea cero, la API se considera en desarrollo y puede cambiar.
- Los tags deben identificar el servicio, por ejemplo `catalog-service-v0.1.0`.
