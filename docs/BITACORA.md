# Bitácora del proyecto

> Tres líneas por persona por semana. No es burocracia: en septiembre **esta bitácora es el
> informe**, ya escrito. Escribir esto al final de cada semana cuesta 5 minutos y ahorra un día.
>
> Formato: qué hice · qué me costó · qué queda pendiente.

---

## Semana 0 · 19–23 ago 2026

**19-08 · Sesión de planificación (los tres)**

Se armó toda la documentación del proyecto y se creó el repositorio. Todavía no hay código.

Decisiones tomadas:

- **El scraper ingesta por el API Manager**, no directo a RabbitMQ. Antes el token de client
  credentials no se validaba en ninguna parte; ahora entra por `POST /ingesta` con su scope.
- **3 capturas diarias** en vez de una, para que el gráfico de historial tenga densidad.
- **`oferta` es estado (upsert) y `precio_historico` es serie (append)**. Estaba ambiguo y
  habría reventado en septiembre.
- **Idempotencia** con hash + `UNIQUE`, porque RabbitMQ entrega at-least-once.
- **Matching con `pg_trgm`** y umbrales 0.85 / 0.60, más tabla de candidatos para revisión manual.
- **Grupos de Cognito** además de scopes: sin roles no hay 403.
- **Code freeze el 10-sep**, tres días de margen antes de la entrega del 13.
- El repo queda en la cuenta `Martinnnn-ops` (la de los runners de Actions).

**Integrante 1 —**

**Integrante 2 — Backend**

Se configuró el monorepo Maven con Java 25 y se agregó `catalog-service` como primer módulo
Micronaut, todavía sin lógica de negocio. Se verificó el arranque y el endpoint de salud. Queda
pendiente que el integrante implemente el primer endpoint del catálogo con acompañamiento.

Se decidió usar Semantic Versioning independiente por microservicio porque cada uno avanzará a
un ritmo distinto. `catalog-service` parte en `0.1.0`; la versión del agregador Maven no se hereda
como versión del servicio.

Se habilitó el versionado de rutas mediante headers y se crearon los primeros controladores del
catálogo junto con su DTO de respuesta. Queda pendiente incorporar pruebas HTTP antes de conectar
la capa de aplicación y la persistencia.

**Integrante 3 —**

**Del equipo:** (decisiones tomadas, cosas que cambiaron respecto del plan)

---

## Semana 1 · 24–30 ago 2026

**Integrante 1 —** (27-08) Levanté el scraper de arranque de Sparta, que estaba pendiente desde
la semana 0. Está en `tools/scraper-rapido/` y ya corre solo: 3 capturas diarias por timer de
systemd (08:00 / 15:00 / 22:00), guardando el JSON crudo comprimido en `capturas/`. Primera
captura real el 27-08 a las 18:40: **2.088 productos, 908 zapatillas, 9.125 filas talla-stock,
142 KB, 110 segundos**. Lo que me costó fue descubrir que la query que teníamos estaba mal
(ver abajo). Queda pendiente que el Integrante 3 lo migre a Lambda y el crudo suba a S3.

**Integrante 2 —**

**Integrante 3 —**

**Del equipo:**

### 🔴 Hallazgo del 27-08 · la búsqueda de Sparta no filtra por marca

`PLAN.md §3` da por verificado que se consulta Sparta buscando por texto. **No sirve.** La
búsqueda de Magento es difusa y el término "zapatillas" domina al de marca: pedir
`"nike zapatillas"` devuelve **1126 resultados de los cuales solo 24 son Nike**, y las cuatro
marcas devolvían prácticamente los mismos productos. La primera captura trajo 4.000 productos
que en su mayoría eran ruido.

Se corrige filtrando por el atributo `gral_marca`, cuyos ids salen de `aggregations`:

```graphql
products(filter: {gral_marca: {eq: "21"}}, pageSize: 50, currentPage: 1)
```

Con el filtro puesto, el catálogo real de Sparta es este — y **contradice la elección de marcas
del MVP**:

| Marca | id | Productos | Zapatillas |
|---|---|---|---|
| New Balance | 21 | 857 | 408 |
| Adidas | 3 | 752 | 239 |
| Joma | 18644 | 267 | 138 |
| Asics | 447 | 96 | 95 |
| Nike | 23 | 89 | 24 |
| Puma | 30 | 27 | 4 |

**Sparta es una tienda de New Balance y Adidas.** Nike y Puma son marginales, y varios de los
89 productos Nike son balones y accesorios, no calzado.

Esto importa porque `PLAN.md` usa Nike como **ejemplo canónico del style code** (`HV9774`) y
declara a Sparta *fuente canónica del catálogo*. Si el catálogo canónico se arma sobre Nike,
se arma sobre 24 productos. **Hay que decidir en equipo si las 4 marcas del MVP pasan a ser
New Balance, Adidas, Joma y Asics.**

Dato a favor de la tesis del proyecto: **1.277 de los 2.088 productos tienen descuento activo
declarado**, con casos de 38% — o sea que hay bastante que contrastar contra el mínimo
observado en cuanto el historial tenga fondo.

### 🟠 Hallazgo del 27-08 · el endpoint de Hites que teníamos ya no responde

`PLAN.md §3` registra `Search-UpdateGrid` como la vía para Hites, verificada el 18-08. Hoy ese
endpoint devuelve **500** para cualquier búsqueda. La home sí responde 200.

Alternativa que sí funciona, probada hoy: la página de búsqueda normal
`https://www.hites.com/search?q=<término>`, que trae la grilla con 24 productos por página y
sus `data-pid`. Sigue siendo parseo con Jsoup, así que el plan del Integrante 3 no cambia de
forma — cambia la URL. Conviene confirmarlo antes de escribir el adaptador.

### ⏱️ Sobre el historial

El scraper arrancó el **27-08**, no en la semana 0. Al code freeze del 10-09 el historial va a
tener **14 días**, no las 3 semanas que pide el checklist final de `TAREAS.md`. No es
recuperable; conviene ajustar esa casilla y decirlo en el informe en vez de que lo pregunten.

---

## Semana 2 · 31 ago – 6 sep 2026

**Integrante 1 —**

**Integrante 2 —**

**Integrante 3 —**

**Del equipo:**

---

## Semana 3 · 7–13 sep 2026 · Entrega EP1

**Integrante 1 —**

**Integrante 2 —**

**Integrante 3 —**

**Del equipo:**
