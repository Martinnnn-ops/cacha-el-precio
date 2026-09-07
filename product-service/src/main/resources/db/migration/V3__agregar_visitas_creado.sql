-- Rastro de actividad para pintar "lo vieron X veces" y "agregado hace X".
-- visitas sube con cada visita a la ficha; visto_en y creado_en son fechas
-- ISO-8601 en texto (SQLite no tiene tipo fecha nativo).
-- El scraper no conoce estas columnas: al actualizar un producto no las toca.
ALTER TABLE productos ADD COLUMN visitas INTEGER NOT NULL DEFAULT 0;
ALTER TABLE productos ADD COLUMN visto_en TEXT;
ALTER TABLE productos ADD COLUMN creado_en TEXT;