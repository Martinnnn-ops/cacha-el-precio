# ADR-007 · El catálogo se ve sin iniciar sesión

**Fecha:** 19-08-2026 · **escrito como ADR el 07-09-2026**

**Estado:** aceptada

## Contexto

La decisión está tomada desde la semana 0 y vive en
[`ARQUITECTURA.md` §8](../ARQUITECTURA.md), en la tabla de permisos:

| Scope / rol | Quién | Protege |
|---|---|---|
| `precios:leer` | **público, sin token** | Búsqueda y ficha de producto |

Nunca se escribió como ADR porque parecía obvia. **Dejó de serlo el 07-09**, cuando al montar el
API Gateway se puso `GET /productos` detrás del JWT Authorizer para poder demostrar el 401 que
pide la rúbrica. Eso habría dejado el catálogo invisible para cualquiera que llegue de una
búsqueda de Google.

Se detectó antes de que el tráfico pasara por ahí, pero el episodio muestra el problema: una
decisión que solo vive dentro de una tabla en un documento de 677 líneas es una decisión que
alguien va a contradecir sin darse cuenta.

## Decisión

**Leer el catálogo y las fichas de producto es público. No requiere token.**

Lo que sí se protege:

| | Ruta | Regla |
|---|---|---|
| 🔓 | `GET /productos`, `/productos/{id}`, `/catalogos` | Público |
| 🔓 | `POST /productos/{id}/visitas` | Público — el contador se lleva de visitantes sin sesión |
| 🔒 | `GET`/`POST`/`DELETE /seguimiento` | **Sesión** → 401 sin token |
| 🔒 | `GET /api/admin/*` | **Grupo `admin`** → 403 al usuario común |
| 🔒 | `POST`/`PUT`/`DELETE /productos` | **Scope `ingesta`** → 403 sin él |

## Por qué

**Es lo que el producto es.** Un comparador de precios existe para que alguien que está por
comprar sepa si el descuento es real. Ponerle un registro delante convierte una herramienta
pública en un servicio con cuenta, y nadie crea una cuenta para mirar un precio que puede ver
entrando al sitio de la tienda.

**El dato no es privado.** Los precios se obtienen de páginas públicas. Proteger la copia de algo
que cualquiera puede leer en el origen no protege nada, solo estorba.

**Se pierde el tráfico que importa.** Un catálogo detrás de login no lo indexa ningún buscador, y
para un comparador la búsqueda es de donde llega la gente.

## Consecuencias

- **El 401 de la demo no puede salir del catálogo**, y por eso existe `/seguimiento`: es privado
  por su naturaleza —son los productos que una persona eligió seguir— y demuestra lo mismo sin
  romper nada. El 403 sale de las escrituras y del grupo `admin`.
- **Hay que decirlo en la presentación antes de que lo pregunten.** Un evaluador que ve rutas
  abiertas puede leerlo como un descuido. La frase es corta: *"lo público es público a propósito;
  esto es lo que sí protegemos y por qué"*, y se defiende mejor que haber protegido todo.
- **Lectura abierta significa que se puede abusar.** Hoy no hay límite de peticiones. Es
  aceptable con este tráfico, y la respuesta correcta es el **throttling del API Gateway**, no un
  token: el objetivo es frenar el volumen, no identificar a la persona.
- **Esta regla hay que revisarla en cada capa nueva.** Ya se contradijo una vez en el API Gateway.
  Falta aplicarla al `@Secured` de `product-service`, que hoy no tiene ninguna.
