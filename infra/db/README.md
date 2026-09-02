# Migraciones de base de datos

## Dónde viven y por qué acá

Una carpeta por esquema, y los esquemas salen del [ADR-010](../../docs/adr/) — una sola
instancia de RDS con un esquema por servicio:

```
infra/db/catalog/   → esquema catalog, dueño: catalog-service
infra/db/price/     → esquema price,   dueño: price-service
```

> 📌 **Esto sigue siendo temporal, pero ya por otra razón.** `price-service` **ya existe** como
> módulo (creado el 27-08). Lo natural en Micronaut es que cada servicio lleve sus migraciones en
> `src/main/resources/db/migration` y las aplique Flyway al arrancar — pero eso requiere además
> el driver JDBC, la configuración del datasource y las credenciales, que recién se resuelven
> cuando exista la base en RDS (Semana 2).
>
> **Moverlas ahora dejaría a los servicios intentando conectarse a una base que todavía no
> tienen configurada.** Se mueven junto con el cableado de Flyway, no antes.

## Aplicarlas a mano

Con el compose arriba:

```bash
set -a && . ./.env && set +a
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=1 \
  < infra/db/catalog/V1__catalogo_base.sql
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=1 \
  < infra/db/price/V1__precios_base.sql
```

Para volver a empezar de cero:

```bash
docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" \
  -c "DROP SCHEMA catalog CASCADE; DROP SCHEMA price CASCADE; CREATE SCHEMA catalog; CREATE SCHEMA price;"
```

## Este modelo NO es el de PLAN.md §4

Es ese modelo **después de contrastarlo con 2.088 productos reales** capturados de Sparta el
27-08. Cinco cosas cambiaron, y cada una está comentada en el SQL donde ocurre:

| # | Qué cambió | Por qué |
|---|---|---|
| 1 | `style_code` pasa a ser **nullable** | Solo Nike lo trae extraíble (86/89). New Balance, Adidas, Joma, Asics y Puma: **0%**. Y Hites no publica ninguno |
| 2 | `talla` se parte en **`talla_original` + `escala` + `talla_valor`** | Sparta usa **20 atributos distintos** de talla. Un "9" US de New Balance no es el mismo pie que un "9" US de Adidas |
| 3 | `stock` se reemplaza por **`vista_en` + `activa`** | Sparta **nunca devuelve productos agotados**: los 2.088 vienen `IN_STOCK`. La señal de agotado es la **ausencia** |
| 4 | Aparece **`producto_tienda`** | El matching no es instantáneo: hay que poder guardar lo que llegó de una tienda antes de saber a qué modelo corresponde |
| 5 | `CHECK` de rango por escala | 3.229 variantes rotuladas "US" traían números **europeos**. Sin esta restricción, ese error entra en silencio |

## Lo que las restricciones atrapan

Probado el 27-08 insertando datos malos a propósito. Las cuatro rechazan:

| Caso | Restricción |
|---|---|
| Talla `US` con valor `40` (es europeo) | `talla_rango_plausible` |
| Talla `ALFA` con valor numérico | `talla_valor_coherente` |
| Precio `0` (hay 3 en la captura real) | `oferta_precio_check` |
| Mismo `style_code` repetido en una marca | `modelo_style_code_unico` |

## ⚠️ Pendiente que no resuelve el esquema

**Los umbrales de matching de `PLAN.md` no resisten el catálogo real.** Medido:

```
'New Balance 574 Negra'  vs  'New Balance 574 Negro'  ->  0,696   (mismo zapato)
'New Balance 574 Negra'  vs  'New Balance 515 Negra'  ->  0,660   (OTRO zapato)
```

El correcto queda **bajo** el 0,85 que acepta, el equivocado queda **sobre** el 0,60 que
rechaza, y entre acertar y equivocarse hay **0,04**. La causa es que el número del modelo
(574 vs 515) es lo único que los distingue y el trigrama casi no lo pesa.

**El matcher tiene que extraer el número de modelo y exigir coincidencia exacta**, dejando el
trigrama para el resto del nombre. Eso es trabajo del matcher, no del esquema, pero si no se
hace el comparador va a emparejar zapatos distintos con total seguridad.
