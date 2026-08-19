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

**Integrante 2 —**

**Integrante 3 —**

**Del equipo:** (decisiones tomadas, cosas que cambiaron respecto del plan)

---

## Semana 1 · 24–30 ago 2026

**Integrante 1 —**

**Integrante 2 —**

**Integrante 3 —**

**Del equipo:**

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
