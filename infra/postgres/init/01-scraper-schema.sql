-- Esquemas propiedad del scraper Python y Product Service.
-- Comparten servidor, no tablas ni acceso de dominio.
-- Se ejecuta UNA sola vez, cuando el volumen de Postgres se crea vacío.

-- ─── Extensiones ────────────────────────────────────────────────────────────
-- Las necesita el emparejamiento entre tiendas (ADR-022): la misma zapatilla se
-- llama distinto en cada una ("Nike Air Max 90" / "NIKE AIRMAX 90 Hombre"), asi
-- que hay que comparar por parecido y no por igualdad.
--
--   pg_trgm   compara cadenas por trigramas -> "similitud del 82%"
--   unaccent  quita tildes, para que "Adidas" y "Ádidas" sean lo mismo
--
-- Van aqui y no en una migracion de EF porque son del servidor, no del modelo:
-- crearlas necesita permisos que la aplicacion no tiene por que tener.
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS unaccent;

-- ─── Esquemas ───────────────────────────────────────────────────────────────

CREATE SCHEMA IF NOT EXISTS scraper;
CREATE SCHEMA IF NOT EXISTS product;

COMMENT ON SCHEMA scraper IS 'Productos scrapeados e historial de precios — scraper-service (Python)';
COMMENT ON SCHEMA product IS 'Catálogo canónico y ofertas — Product Service (ASP.NET Core)';

-- ─── Tablas ─────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS scraper.products (
    store           TEXT        NOT NULL,
    external_id     TEXT        NOT NULL,
    name            TEXT        NOT NULL,
    brand           TEXT        NOT NULL,
    price           INTEGER     NOT NULL CHECK (price >= 0),
    currency        TEXT        NOT NULL DEFAULT 'CLP',
    product_url     TEXT        NOT NULL,
    description     TEXT,
    source_image_url TEXT,
    image_url       TEXT,
    image_card_url  TEXT,
    image_detail_url TEXT,
    image_card_key  TEXT,
    image_detail_key TEXT,
    image_hash      TEXT,
    available       BOOLEAN     NOT NULL DEFAULT TRUE,
    scraped_at      TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (store, external_id)
);

CREATE TABLE IF NOT EXISTS scraper.price_history (
    id              BIGSERIAL   PRIMARY KEY,
    store           TEXT        NOT NULL,
    external_id     TEXT        NOT NULL,
    price           INTEGER     NOT NULL CHECK (price >= 0),
    currency        TEXT        NOT NULL DEFAULT 'CLP',
    available       BOOLEAN     NOT NULL,
    scraped_at      TIMESTAMPTZ NOT NULL,
    CONSTRAINT fk_price_history_product
        FOREIGN KEY (store, external_id)
        REFERENCES scraper.products (store, external_id)
        ON DELETE CASCADE
);

-- ─── Índices ────────────────────────────────────────────────────────────────

CREATE INDEX IF NOT EXISTS ix_price_history_producto_fecha
    ON scraper.price_history (store, external_id, scraped_at DESC);

CREATE INDEX IF NOT EXISTS ix_price_history_fecha
    ON scraper.price_history (scraped_at DESC);

CREATE INDEX IF NOT EXISTS ix_products_brand
    ON scraper.products (brand);
