-- ═══════════════════════════════════════════════════════════════════════════
--  V1 · Esquema `catalog` — qué productos existen y cuáles son el mismo
-- ═══════════════════════════════════════════════════════════════════════════
--
--  Este modelo NO es el que estaba propuesto en PLAN.md §4. Es ese modelo
--  después de contrastarlo con 2.088 productos reales capturados de Sparta el
--  27-08. Los cambios y su motivo están comentados en cada lugar donde ocurren.
--
--  Alcance del EP1: calzado de New Balance, Adidas, Nike y Puma — las cuatro
--  marcas que existen en Sparta Y en Hites, que son las únicas comparables.
--  El modelo soporta vestuario sin cambios: la decisión está en `categoria` y
--  en cómo se guardan las tallas.
-- ═══════════════════════════════════════════════════════════════════════════

-- ─── Tipos ─────────────────────────────────────────────────────────────────

-- Zapatillas es el recorte de la primera entrega, no el techo del proyecto.
-- Dejar esto como enum desde ahora evita una migración cuando entre la ropa.
CREATE TYPE catalog.categoria AS ENUM ('calzado', 'vestuario', 'accesorio');

CREATE TYPE catalog.genero AS ENUM ('hombre', 'mujer', 'unisex', 'ninos');

-- La escala en la que está expresada una talla.
--
-- MEDIDO: Sparta usa 20 atributos distintos para talla (`talla_us_nb_hombre`,
-- `talla_us_adidas_hombre`, `talla_cl_joma_hombre`, `zapatillas_tallauk`,
-- `ropa_talla`...). Un "9" en escala US de New Balance y un "9" en escala US
-- de Adidas NO son el mismo pie, y un "M" de ropa no es comparable con nada
-- numérico. Sin guardar la escala, comparar tallas entre tiendas da resultados
-- falsos con toda la seguridad del mundo.
--
-- ALFA es S/M/L/XL: es la escala de vestuario, y ya es la MÁS frecuente en la
-- captura (3.068 de 9.125 filas de talla).
CREATE TYPE catalog.escala_talla AS ENUM ('US', 'CL', 'UK', 'EU', 'ALFA');


-- ─── Marcas ────────────────────────────────────────────────────────────────
CREATE TABLE catalog.marca (
    id      smallint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre  varchar(60) NOT NULL,
    slug    varchar(60) NOT NULL UNIQUE
);

COMMENT ON TABLE catalog.marca IS
    'Marcas terceras. El EP1 va con las 4 que existen en ambas tiendas.';


-- ─── Tiendas ───────────────────────────────────────────────────────────────
CREATE TABLE catalog.tienda (
    id           smallint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre       varchar(60)  NOT NULL,
    slug         varchar(60)  NOT NULL UNIQUE,
    plataforma   varchar(40)  NOT NULL,
    url_base     varchar(200) NOT NULL
);


-- ─── Modelo canónico ───────────────────────────────────────────────────────
--  Un modelo = un producto del mundo real ("New Balance 530 blanco"),
--  independiente de en qué tienda se venda.
CREATE TABLE catalog.modelo_canonico (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    marca_id            smallint NOT NULL REFERENCES catalog.marca(id),
    categoria           catalog.categoria NOT NULL,
    genero              catalog.genero,

    -- Medido: el nombre más largo de la captura tiene 95 caracteres.
    nombre              varchar(160) NOT NULL,

    -- Nombre en minúsculas, sin tildes y sin palabras de relleno. Es el campo
    -- contra el que corre el matching, no `nombre`.
    nombre_normalizado  varchar(160) NOT NULL,

    -- ⚠️ NULLABLE, y eso es el cambio más importante de todo este archivo.
    --
    -- PLAN.md daba por hecho que el style code de fábrica es "idéntico en todas
    -- partes" y que por eso se puede afirmar con certeza que dos productos son
    -- el mismo. MEDIDO sobre 2.088 productos: solo Nike lo trae de forma
    -- extraíble (86 de 89, 96%). New Balance, Adidas, Joma, Asics y Puma:
    -- 0%. Cada marca esconde su código con su propio prefijo y sin formato
    -- común — y Hites directamente no publica ninguno.
    --
    -- Conclusión: el style code sirve como identidad interna cuando está, pero
    -- NO puede ser el mecanismo de comparación entre tiendas. Ese rol lo toma
    -- el matching por nombre con pg_trgm (ver el índice de más abajo).
    style_code          varchar(32),

    creado_en           timestamptz NOT NULL DEFAULT now()
);

-- Cuando el style code existe, no puede repetirse dentro de la marca.
-- Índice parcial: las filas con NULL no molestan.
CREATE UNIQUE INDEX modelo_style_code_unico
    ON catalog.modelo_canonico (marca_id, style_code)
    WHERE style_code IS NOT NULL;

-- El índice que sostiene el matching. GIN + trigramas hace que
-- `nombre_normalizado % 'texto'` no recorra la tabla entera.
--
-- ⚠️ Los umbrales de PLAN.md (0,85 acepta / 0,60 rechaza) NO resisten el
-- catálogo real. Medido contra estos mismos datos:
--
--   'New Balance 574 Negra'  vs  'New Balance 574 Negro'   -> 0,696
--   'New Balance 574 Negra'  vs  'New Balance 515 Negra'   -> 0,660   ¡otro zapato!
--
-- El modelo correcto queda BAJO el 0,85 y uno equivocado queda SOBRE el 0,60:
-- con nombres reales casi todo cae en la banda de revisión manual, y la
-- diferencia entre acertar y equivocarse es de apenas 0,04.
--
-- La causa es que el número del modelo (574 vs 515) es lo único que los
-- distingue, y el trigrama casi no lo pesa porque comparten todo lo demás.
--
-- Por eso el matching NO puede ser solo trigrama sobre el nombre completo:
-- hay que extraer el número/token de modelo y exigir coincidencia exacta,
-- usando el trigrama solo para el resto. Queda pendiente para el matcher.
CREATE INDEX modelo_nombre_trgm
    ON catalog.modelo_canonico USING gin (nombre_normalizado gin_trgm_ops);

CREATE INDEX modelo_por_marca ON catalog.modelo_canonico (marca_id, categoria);


-- ─── Variantes ─────────────────────────────────────────────────────────────
--  Una variante = un modelo en una talla y color concretos. Es el nivel al que
--  de verdad se compra y al que se compara el precio.
CREATE TABLE catalog.variante (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    modelo_id       bigint NOT NULL REFERENCES catalog.modelo_canonico(id) ON DELETE CASCADE,

    -- Lo que dice la tienda, tal cual: "9.5", "M", "42". Medido: la etiqueta
    -- más larga son 11 caracteres, sobre 107 etiquetas distintas.
    talla_original  varchar(16) NOT NULL,

    -- En qué escala está esa etiqueta. Sin esto no se pueden comparar tallas.
    escala          catalog.escala_talla NOT NULL,

    -- La talla como número, para poder ordenarla y compararla.
    -- NULL a propósito cuando la escala es ALFA: "M" no es un número, y
    -- forzarlo a serlo sería inventar un dato.
    talla_valor     numeric(4,1),

    color           varchar(60),

    -- Una talla no se repite dentro del mismo modelo y escala.
    UNIQUE (modelo_id, escala, talla_original, color),

    -- Coherencia entre escala y valor: si es ALFA no hay número, y si es
    -- numérica tiene que haberlo.
    CONSTRAINT talla_valor_coherente CHECK (
        (escala = 'ALFA' AND talla_valor IS NULL) OR
        (escala <> 'ALFA' AND talla_valor IS NOT NULL)
    ),

    -- ⚠️ Esta restricción existe por un problema real, no por prolijidad.
    --
    -- MEDIDO: Sparta llama a su atributo `talla_us_nb_mujer`, pero los valores
    -- que trae son 37.5, 38, 40 — números EUROPEOS, no US. De 3.993 variantes
    -- rotuladas "US", 3.229 traían en realidad numeración europea.
    --
    -- Si la ingesta le cree al NOMBRE del atributo, termina comparando un 40
    -- europeo contra un 9 US y el resultado es basura entregada con total
    -- seguridad. La escala hay que deducirla del RANGO del valor, no del
    -- nombre del campo; esta restricción impide que un error así entre a la
    -- base en silencio.
    CONSTRAINT talla_rango_plausible CHECK (
        escala = 'ALFA'
        OR (escala IN ('US', 'UK') AND talla_valor BETWEEN 0.5 AND 19.5)
        OR (escala IN ('EU', 'CL')  AND talla_valor BETWEEN 15   AND 55)
    )
);

CREATE INDEX variante_por_modelo ON catalog.variante (modelo_id);


-- ─── Producto tal como lo publica la tienda ────────────────────────────────
--  Tabla que NO estaba en el modelo propuesto y que hace falta.
--
--  El matching no es instantáneo ni perfecto: hay productos que llegan de una
--  tienda y todavía no se sabe a qué modelo canónico corresponden. Sin un lugar
--  donde dejarlos, la única opción sería descartarlos — y perder el dato.
CREATE TABLE catalog.producto_tienda (
    id              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tienda_id       smallint NOT NULL REFERENCES catalog.tienda(id),

    -- Medido: el SKU más largo de Sparta tiene 20 caracteres.
    sku_externo     varchar(64)  NOT NULL,
    nombre_externo  varchar(200) NOT NULL,
    url             varchar(400),

    -- NULL mientras no se haya matcheado. Es el estado normal al ingresar.
    variante_id     bigint REFERENCES catalog.variante(id),

    visto_por_primera_vez timestamptz NOT NULL DEFAULT now(),

    UNIQUE (tienda_id, sku_externo)
);

CREATE INDEX producto_tienda_sin_matchear
    ON catalog.producto_tienda (tienda_id)
    WHERE variante_id IS NULL;


-- ─── Candidatos de matching ────────────────────────────────────────────────
--  Lo que cayó en la zona gris entre 0,60 y 0,85: ni se acepta ni se descarta
--  solo, lo revisa una persona.
CREATE TABLE catalog.match_candidato (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    producto_tienda_id  bigint NOT NULL REFERENCES catalog.producto_tienda(id) ON DELETE CASCADE,
    modelo_id           bigint NOT NULL REFERENCES catalog.modelo_canonico(id) ON DELETE CASCADE,
    similitud           numeric(4,3) NOT NULL CHECK (similitud BETWEEN 0 AND 1),
    estado              varchar(12)  NOT NULL DEFAULT 'pendiente'
                        CHECK (estado IN ('pendiente', 'aceptado', 'rechazado')),
    revisado_en         timestamptz,
    UNIQUE (producto_tienda_id, modelo_id)
);

CREATE INDEX match_pendientes
    ON catalog.match_candidato (similitud DESC)
    WHERE estado = 'pendiente';


-- ─── Datos iniciales ───────────────────────────────────────────────────────

-- Las 4 marcas del EP1. Son las que están en Sparta Y en Hites: Joma y Asics
-- tienen catálogo en Sparta (138 y 95 zapatillas) pero Hites no las vende, así
-- que no hay contra qué compararlas y quedan fuera del MVP.
INSERT INTO catalog.marca (nombre, slug) VALUES
    ('New Balance', 'new-balance'),
    ('Adidas',      'adidas'),
    ('Nike',        'nike'),
    ('Puma',        'puma');

INSERT INTO catalog.tienda (nombre, slug, plataforma, url_base) VALUES
    ('Sparta', 'sparta', 'magento-graphql',            'https://www.sparta.cl'),
    ('Hites',  'hites',  'salesforce-commerce-scraping','https://www.hites.com');
