# ADR-013 · Java 25 y Maven como base del backend

**Fecha:** 19-08-2026

**Estado:** reemplazada por el [ADR-020](020-csharp-y-simplificacion-de-servicios.md) el 08-09-2026

## Contexto

El backend necesita una versión de Java y una herramienta de construcción únicas para que los
microservicios compilen igual en los computadores del equipo y en CI. Micronaut 5 tiene Java 25
como versión base.

> Este documento conserva la justificación histórica. Los servicios activos de aplicación fueron
> migrados a C# y ASP.NET Core; el scraper continúa en Python.

## Alternativas consideradas

- **Java 21:** es LTS y tiene un ecosistema maduro, pero no coincide con la versión base de
  Micronaut 5 elegida para el proyecto.
- **Gradle:** tiene buen soporte en Micronaut, pero el equipo decidió estandarizar el trabajo del
  backend con Maven.
- **Maven instalado en cada computador:** funciona, pero permite diferencias de versión entre
  entornos.

## Decisión

Los microservicios usan Java 25, Micronaut 5 y Maven. El repositorio incluye Maven Wrapper y un
`pom.xml` agregador en la raíz para ejecutar todos los módulos con la misma orden.

## Consecuencias

- El equipo y los runners de CI deben disponer de un JDK 25.
- `./mvnw` fija la versión de Maven y reduce diferencias entre entornos.
- Usar una versión reciente de Java puede revelar incompatibilidades en herramientas que todavía
  no la soporten; deben verificarse antes de incorporarlas.
- Los futuros microservicios **en Java** deben agregarse como módulos del `pom.xml` raíz. Uno
  en otro lenguaje se levanta desde el `docker-compose.yml`, como el scraper.
