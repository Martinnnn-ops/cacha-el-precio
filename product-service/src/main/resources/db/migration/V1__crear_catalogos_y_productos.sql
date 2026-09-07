CREATE TABLE catalogos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    nombre TEXT NOT NULL UNIQUE,
    descripcion TEXT
);

CREATE TABLE productos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    nombre TEXT NOT NULL,
    descripcion TEXT,
    precio NUMERIC NOT NULL CHECK (precio >= 0),
    catalogo_id INTEGER NOT NULL,
    activo INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT fk_producto_catalogo
        FOREIGN KEY (catalogo_id) REFERENCES catalogos (id) ON DELETE RESTRICT
);

CREATE INDEX idx_productos_catalogo_id ON productos (catalogo_id);
CREATE INDEX idx_productos_activo ON productos (activo);
