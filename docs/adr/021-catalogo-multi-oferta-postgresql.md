# ADR-021 · Catálogo canónico multi-oferta en PostgreSQL

- **Estado:** aceptado
- **Fecha:** 08-09-2026
- **Reemplaza parcialmente:** ADR-016 y la persistencia SQLite decidida en ADR-020

## Contexto

El catálogo guardaba una fila por publicación de tienda. Dos Converse del mismo modelo en Paris
y Ripley aparecían como productos independientes, por lo que el frontend no podía comparar sus
precios. El contrato de tallas era un conjunto fijo de letras y no representaba calzado numérico;
además, una SQLite montada en un contenedor impedía escalar Product Service a varias réplicas.

El scraper ya usa PostgreSQL. Mantener dos motores aumentaba respaldo, diagnóstico y despliegue,
sin aportar aislamiento físico real en la única EC2 del proyecto.

## Decisión

1. Product Service persiste en PostgreSQL y es dueño exclusivo del esquema `product`; el scraper
   conserva el esquema `scraper`. Compartir servidor no autoriza consultas cruzadas.
2. `Product` representa la identidad canónica de marca/modelo y contiene muchas `ProductOffer`.
3. `canonicalKey` agrupa publicaciones equivalentes. Se puede enviar explícitamente; si falta, el
   servicio la deriva normalizando marca/nombre y quitando género, color y talla.
4. `(store, externalId)` es único para cada oferta. `POST /api/products` es idempotente: crea el
   producto/oferta o actualiza precio, tallas, URL, imagen y disponibilidad.
5. Las tallas son `string[]`, por lo que admite `XS`, `38` y `42.5` sin un enum cerrado. La
   categoría también es texto abierto para incorporar nuevos tipos sin otra migración.
6. El scraper sigue comunicándose por HTTP. No conoce tablas ni credenciales del esquema product.

## Consecuencias

- El frontend recibe un producto con varias ofertas y puede calcular la tienda más barata.
- PostgreSQL habilita varias réplicas y un único procedimiento de respaldo, manteniendo schemas
  y migraciones independientes.
- El matching es heurístico: nombres muy distintos pueden separar el mismo modelo y nombres
  demasiado parecidos pueden unir variantes. `canonicalKey` manual es la salida operativa; una
  futura cola de revisión o matching por GTIN/modelo puede reemplazar la heurística.
- La migración inicial PostgreSQL compacta las migraciones SQLite anteriores porque el servicio
  C# aún no estaba desplegado. Un `product.db` local no se convierte automáticamente.
- El contrato v1 cambia antes de su primer despliegue C#: `sizes` pasa a lista y la respuesta
  contiene `offers[]`. Scraper y frontend se actualizan en el mismo cambio.
