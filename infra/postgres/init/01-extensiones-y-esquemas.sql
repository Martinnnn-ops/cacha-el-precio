-- Se ejecuta UNA sola vez, cuando el volumen de Postgres se crea vacío.
-- Si cambias algo acá y quieres que se vuelva a aplicar:
--     docker compose down -v     (ojo: -v borra los datos)

-- ─── Extensiones ────────────────────────────────────────────────────────────
-- pg_trgm es la que hace el matching de nombres entre tiendas. Compara qué tan
-- parecidos son dos textos con un número entre 0 y 1 ("similitud por trigramas").
-- Es la razón por la que la base es Postgres y no MySQL: en MySQL no existe.
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- unaccent quita las tildes antes de comparar, para que "Básquetbol" y
-- "Basquetbol" no se traten como productos distintos.
CREATE EXTENSION IF NOT EXISTS unaccent;

-- ─── Un esquema por servicio ────────────────────────────────────────────────
-- Decisión de ARQUITECTURA.md §12 (ADR-010): una sola instancia de base de datos,
-- pero cada microservicio con su propio esquema.
--
-- El patrón "de libro" es una base de datos por servicio. No lo hacemos porque el
-- crédito de AWS Academy no da para dos instancias RDS. El esquema separado
-- mantiene el aislamiento del modelo sin duplicar el costo — y saber explicar esa
-- diferencia es justamente lo que se evalúa en la defensa.
CREATE SCHEMA IF NOT EXISTS catalog;
CREATE SCHEMA IF NOT EXISTS price;

COMMENT ON SCHEMA catalog IS 'Modelos, variantes, tiendas y matching — catalog-service';
COMMENT ON SCHEMA price   IS 'Ofertas e historial de precios — price-service';
