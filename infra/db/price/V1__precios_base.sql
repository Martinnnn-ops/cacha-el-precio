-- ═══════════════════════════════════════════════════════════════════════════
--  V1 · Esquema `price` — cuánto cuesta cada cosa, hoy y a lo largo del tiempo
-- ═══════════════════════════════════════════════════════════════════════════
--
--  La separación que manda acá se decidió en la semana 0 y se mantiene, porque
--  al contrastarla con datos reales resultó ser lo correcto:
--
--      oferta            = ESTADO actual.  Una fila por (variante, tienda).
--                          En cada captura se hace UPSERT. No crece.
--      precio_historico  = SERIE temporal. Solo se agrega, nunca se actualiza.
--                          Es la tabla que crece y la que alimenta el gráfico.
--
--  MEDIDO el 27-08: la clave `(variante, tienda)` es única en las 9.125 filas
--  de la captura, sin un solo duplicado. El upsert está bien fundado.
-- ═══════════════════════════════════════════════════════════════════════════


-- ─── Ejecución de captura ──────────────────────────────────────────────────
--  Una fila por corrida del scraper. Sirve para tres cosas: el test de contrato
--  ("esta corrida trajo más de N productos"), detectar cuándo una tienda cambió
--  su HTML, y ser evidencia en el informe.
CREATE TABLE price.ejecucion_captura (
    id                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tienda_id         smallint NOT NULL,
    inicio            timestamptz NOT NULL,
    fin               timestamptz,
    productos_ok      integer NOT NULL DEFAULT 0,
    productos_error   integer NOT NULL DEFAULT 0,
    estado            varchar(10) NOT NULL
                      CHECK (estado IN ('ok', 'parcial', 'fallido', 'corriendo')),
    -- Dónde quedó el JSON crudo. El crudo es la fuente de verdad (ADR-011):
    -- si la base se pierde, el historial se reconstruye desde ahí.
    ruta_crudo        varchar(400)
);

CREATE INDEX captura_por_tienda
    ON price.ejecucion_captura (tienda_id, inicio DESC);


-- ─── Oferta: el estado de hoy ──────────────────────────────────────────────
CREATE TABLE price.oferta (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    variante_id         bigint   NOT NULL,
    tienda_id           smallint NOT NULL,
    url                 varchar(400),

    -- Pesos chilenos, sin decimales. Medido: el precio más alto de la captura
    -- es $6.990.000, así que 10 dígitos sobran con holgura.
    precio              numeric(10,2) NOT NULL CHECK (precio > 0),
    precio_lista        numeric(10,2) CHECK (precio_lista IS NULL OR precio_lista > 0),
    moneda              char(3) NOT NULL DEFAULT 'CLP',

    -- ⚠️ Acá está el segundo hallazgo importante de la validación.
    --
    -- El modelo propuesto tenía un campo `stock`. No sirve, porque MEDIDO:
    -- Sparta NUNCA devuelve productos agotados — los 2.088 de la captura vienen
    -- IN_STOCK, sin una sola excepción. Magento los filtra antes de responder.
    --
    -- O sea que jamás vamos a ver un OUT_OF_STOCK, y la promesa de "avisa si tu
    -- talla se agotó" no se puede cumplir leyendo una bandera.
    --
    -- La señal real es la AUSENCIA: si una talla estaba en la captura de ayer y
    -- no está en la de hoy, se agotó. Por eso en vez de `stock` hay dos campos:
    --   · `vista_en`  — la última captura en que la tienda la mostró
    --   · `activa`    — false cuando una captura completa no la trajo
    -- Ya se nota en los datos: entre dos capturas el total de New Balance pasó
    -- de 857 a 856.
    vista_en            timestamptz NOT NULL,
    activa              boolean NOT NULL DEFAULT true,

    -- Cuando la tienda avisa "quedan pocas". Medido: viene en ~12% de los
    -- productos. Es lo más cercano a un stock real que entrega Sparta.
    unidades_restantes  smallint CHECK (unidades_restantes IS NULL OR unidades_restantes >= 0),

    actualizado_en      timestamptz NOT NULL DEFAULT now(),

    -- La clave del UPSERT. Validada contra 9.125 filas reales.
    UNIQUE (variante_id, tienda_id)
);

-- Para "muéstrame dónde está más barata esta variante".
CREATE INDEX oferta_por_variante
    ON price.oferta (variante_id, precio)
    WHERE activa;

-- Para detectar lo que dejó de aparecer.
CREATE INDEX oferta_inactivas
    ON price.oferta (tienda_id, vista_en)
    WHERE NOT activa;


-- ─── Historial: la serie temporal ──────────────────────────────────────────
CREATE TABLE price.precio_historico (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    variante_id         bigint   NOT NULL,
    tienda_id           smallint NOT NULL,
    capturado_en        timestamptz NOT NULL,

    precio              numeric(10,2) NOT NULL CHECK (precio > 0),
    precio_lista        numeric(10,2) CHECK (precio_lista IS NULL OR precio_lista > 0),

    -- true = la tienda la mostró en esta captura. Como los agotados no vienen,
    -- en la práctica siempre es true al insertar; existe para que el día que
    -- una tienda sí informe agotados, el modelo no tenga que cambiar.
    disponible          boolean NOT NULL DEFAULT true,

    -- RabbitMQ entrega at-least-once: el mismo mensaje puede llegar dos veces.
    -- Sin esta clave, el historial se llenaría de duplicados y el gráfico
    -- mostraría escalones que nunca ocurrieron.
    -- Se calcula como hash de (variante, tienda, captura, precio).
    clave_idempotencia  char(64) NOT NULL UNIQUE
);

-- El índice del gráfico: "dame el historial de esta variante, más reciente
-- primero". Es la consulta que se ejecuta en la demo.
CREATE INDEX historico_para_grafico
    ON price.precio_historico (variante_id, capturado_en DESC);

-- Para calcular el mínimo observado por tienda.
CREATE INDEX historico_por_tienda
    ON price.precio_historico (tienda_id, capturado_en DESC);


-- ─── Seguimiento del usuario ───────────────────────────────────────────────
--  La razón por la que el login existe: los precios son públicos, pero tu lista
--  es tuya. `usuario_sub` es el claim `sub` del JWT de Cognito — nunca el
--  correo ni el nombre, que son datos personales que no necesitamos guardar.
CREATE TABLE price.seguimiento (
    id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    usuario_sub   varchar(64) NOT NULL,
    modelo_id     bigint NOT NULL,
    -- Opcional: se puede seguir un modelo completo o solo una talla.
    variante_id   bigint,
    creado_en     timestamptz NOT NULL DEFAULT now(),
    UNIQUE (usuario_sub, modelo_id, variante_id)
);

CREATE INDEX seguimiento_por_usuario ON price.seguimiento (usuario_sub);
