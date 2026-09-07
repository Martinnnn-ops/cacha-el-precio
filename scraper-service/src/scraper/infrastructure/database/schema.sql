-- src/scraper/infrastructure/database/schema.sql
-- Esquema de Cacha el Precio.
--
-- Dos tablas, siguiendo el modelo elegido: un producto por tienda, y
-- un historial que solo recibe fila cuando el precio o el stock cambian.

CREATE TABLE IF NOT EXISTS products (
    store        TEXT        NOT NULL,
    external_id  TEXT        NOT NULL,

    name         TEXT        NOT NULL,
    brand        TEXT        NOT NULL,
    price        INTEGER     NOT NULL CHECK (price >= 0),
    currency     TEXT        NOT NULL DEFAULT 'CLP',
    product_url  TEXT        NOT NULL,
    description  TEXT,
    source_image_url TEXT,
    image_url        TEXT,
    image_card_url   TEXT,
    image_detail_url TEXT,
    image_card_key   TEXT,
    image_detail_key TEXT,
    image_hash       TEXT,
    available    BOOLEAN     NOT NULL DEFAULT TRUE,
    scraped_at   TIMESTAMPTZ NOT NULL,

    -- La identidad es el par, no un id autonumerico: la misma zapatilla
    -- en dos tiendas son dos filas distintas y eso es intencionado.
    PRIMARY KEY (store, external_id)
);

-- Migracion idempotente para instalaciones creadas antes del pipeline de imagenes.
ALTER TABLE products ADD COLUMN IF NOT EXISTS source_image_url TEXT;
ALTER TABLE products ADD COLUMN IF NOT EXISTS image_card_url TEXT;
ALTER TABLE products ADD COLUMN IF NOT EXISTS image_detail_url TEXT;
ALTER TABLE products ADD COLUMN IF NOT EXISTS image_card_key TEXT;
ALTER TABLE products ADD COLUMN IF NOT EXISTS image_detail_key TEXT;
ALTER TABLE products ADD COLUMN IF NOT EXISTS image_hash TEXT;

CREATE TABLE IF NOT EXISTS price_history (
    id           BIGSERIAL   PRIMARY KEY,
    store        TEXT        NOT NULL,
    external_id  TEXT        NOT NULL,
    price        INTEGER     NOT NULL CHECK (price >= 0),
    currency     TEXT        NOT NULL DEFAULT 'CLP',
    available    BOOLEAN     NOT NULL,
    scraped_at   TIMESTAMPTZ NOT NULL,

    CONSTRAINT fk_price_history_product
        FOREIGN KEY (store, external_id)
        REFERENCES products (store, external_id)
        ON DELETE CASCADE
);

-- El grafico de precios de un producto pide siempre lo mismo:
-- "dame los ultimos N puntos de este producto, del mas nuevo al mas
-- viejo". Este indice cubre esa consulta entera.
CREATE INDEX IF NOT EXISTS ix_price_history_producto_fecha
    ON price_history (store, external_id, scraped_at DESC);

-- Para la portada: "que bajo de precio hoy".
CREATE INDEX IF NOT EXISTS ix_price_history_fecha
    ON price_history (scraped_at DESC);

-- Buscador por marca.
CREATE INDEX IF NOT EXISTS ix_products_brand
    ON products (brand);
