# ADR-021 · El contrato público del sistema es el del BFF

**Fecha:** 09-09-2026

**Estado:** aceptada

## Contexto

El `gateway` expone hoy **dos familias de rutas para el mismo recurso**:

| Familia | Ejemplo | Origen |
|---|---|---|
| Español, propia del BFF | `GET /productos`, `GET /catalogos`, `GET /seguimiento` | contrato que el gateway define |
| Inglés, prestada del servicio interno | `GET /api/products`, `GET /api/products/by-category/{c}` | el mismo path que publica `product-service` |

El frontend, tras el PR #1 de su repositorio (mergeado el 08-09), consume la **segunda**:
`VITE_API_BASE_URL` termina en `/api` y la capa de servicios llama a `/products`.

Esto obliga a decidir algo que hasta ahora nadie había escrito: **cuál de las dos es el contrato
público del sistema**. No es una preferencia de estilo. El BFF es el 40% de la nota del EP1, y se
justifica con dos capacidades: validar la identidad en el borde **y aislar al cliente del servicio
interno**. Si el navegador pide exactamente el mismo path que publica `product-service`, la segunda
capacidad no existe: el gateway queda de intermediario transparente y el nombre del servicio
interno viaja hasta el navegador de cada usuario.

El costo de equivocarse no es hipotético. El día que `product-service` renombre una ruta o cambie
la forma de su respuesta, ese cambio llega hasta el navegador de gente que tiene la página abierta
—y el frontend se despliega aparte, así que ni siquiera se actualizan a la vez.

## Alternativas consideradas

- **Dejar el frontend en `/api/products`.** Cuesta cero: es lo que ya está mergeado. Pero acopla el
  cliente al nombre interno del servicio, deja al BFF sin nada que aislar y convierte la pregunta
  «¿por qué existe el gateway?» en una respuesta débil: «valida el token». Para eso basta el
  authorizer del API Gateway de AWS, que también está en la arquitectura.

- **Eliminar el contrato en español y quedarse solo con el inglés.** Es coherente —una sola familia
  de rutas— y es menos trabajo que lo contrario. Pero renuncia explícitamente al aislamiento, y
  entonces el BFF hay que justificarlo de otra manera o quitarlo del diagrama.

- **El frontend habla el contrato en español y el inglés se retira del borde.** Exige tocar el
  frontend recién mergeado y avisar al equipo. A cambio, deja una sola puerta pública, con nombres
  que pertenecen al sistema y no a uno de sus servicios.

## Decisión

**El contrato público es el del BFF, en español.** El frontend consume únicamente:

```
GET    /productos              GET /productos/{id}         GET /catalogos
GET    /seguimiento            POST /seguimiento/{id}      DELETE /seguimiento/{id}
GET    /api/yo                 GET /health
```

Las rutas `/api/products*` del gateway **dejan de ser parte del contrato público** y se retiran del
borde. `product-service` las conserva: son su interfaz interna, y el gateway es su único cliente.

Se elige el español y no el inglés porque el resto del sistema ya está en español —los documentos,
los commits, los mensajes de error del propio gateway (`{estado, error, mensaje}`)— y porque un
contrato público que no comparte vocabulario con la documentación que lo explica es un contrato
que hay que traducir dos veces en cada conversación.

### Lo que esta decisión todavía NO resuelve

Hay que decirlo, porque en la defensa se nota: **las rutas en español de hoy son passthrough
puro**. `/productos` reenvía a `/api/products` sin transformar el cuerpo. El aislamiento que
consigue esta decisión es el del **nombre y la forma de la URL**, no el de la carga útil: el
frontend sigue traduciendo `name`, `price`, `store` y `sizes` a su propio modelo en
`producto.adapter.js`.

Eso es una mejora real y medible —el navegador ya no depende de cómo se llamen las rutas internas—
pero no es un BFF completo. Un BFF completo devuelve la vista que la pantalla necesita. El caso
concreto que lo pide es la agrupación de ofertas por `(store, externalId)` para comparar el mismo
artículo entre tiendas: **hoy eso no lo hace nadie**, y hacerlo en el navegador significa bajarse
el catálogo entero para agrupar en el cliente. Ese es el trabajo que convierte al gateway en un
BFF de verdad, y se decide aparte.

## Consecuencias

### Positivas

- una sola puerta pública, con una sola forma de nombrar cada recurso;
- el nombre del servicio interno deja de viajar al navegador;
- `product-service` puede renombrar o reorganizar sus rutas sin desplegar el frontend;
- menos superficie que probar y que documentar en el borde;
- la pregunta «¿qué aísla el BFF?» tiene una respuesta demostrable con un `curl`.

### Costos y riesgos

- hay que tocar el frontend recién mergeado, y el cambio pisa trabajo del mismo día;
- **toca el carril de otra persona**: las rutas `/api/products*` del gateway las escribió Orion. Se
  avisa antes de retirarlas, según la regla de `AGENTS.md`;
- mientras el passthrough siga siendo passthrough, el argumento del aislamiento es parcial y hay
  que presentarlo como tal;
- `/catalogos` sigue devolviendo una lista fija escrita en `Program.cs`, con ids que no
  corresponden a ningún campo del producto. Es contrato público y hoy miente;
- las rutas `by-category`, `by-price` y `by-size` del gateway desaparecen del borde sin
  reemplazo: nadie las consumía, pero conviene registrarlo por si alguien contaba con ellas.

## Condiciones para revisar la decisión

- si el gateway empieza a transformar cuerpos y no solo rutas, este ADR se amplía en vez de
  reemplazarse: la decisión de fondo —el contrato es del BFF— es la misma;
- si el equipo decide que el sistema se documenta en inglés, se traduce el contrato completo de una
  vez y no servicio por servicio;
- si aparece un segundo cliente con necesidades distintas del navegador (una app móvil, por
  ejemplo), deja de haber **un** contrato público y hay que decidir si son dos BFF o uno con dos
  vistas.
