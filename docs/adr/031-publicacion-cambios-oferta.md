# ADR-031 · product-service: cómo publica los cambios de oferta

Fecha: 17-10-2026 · Estado: aceptado (Fase 2 del EP3).

## Contexto

El [ADR-029](029-mensajeria-sqs-sns.md) decidió que product-service es el productor del flujo 2:
cuando una oferta que **ya existía** baja de precio, se agota o se repone, publica un evento en el
tópico SNS `cep-cambios-oferta`. El contrato está en [`docs/MENSAJERIA.md`](../MENSAJERIA.md):
los tres eventos (`precio.bajo`, `oferta.agotada`, `oferta.repuesta`) se detectan en
`UpsertProductAsync`, comparando lo guardado con lo que llega **antes** de pisar la oferta.

Queda una decisión que el contrato no fija: **cuándo** se publica respecto del guardado en la base,
y **qué pasa si la publicación falla** (SNS caído, timeout, sin credenciales).

## Alternativas

- **Publicar dentro de la misma transacción que el `SaveChanges`.** SNS no es transaccional: si la
  publicación «participa» del commit y luego el commit falla, ya se anunció un cambio que no ocurrió.
  Y al revés, si se publica y el proceso muere antes del commit, se avisa de una baja fantasma.
- **Outbox transaccional**: guardar el evento en una tabla dentro de la misma transacción y que un
  proceso aparte lo despache a SNS. Es la forma correcta de no perder ni inventar eventos, pero
  agrega una tabla, un despachador y su monitoreo — demasiado para el alcance del EP3.
- **Publicar después del `SaveChanges`, best-effort.** El catálogo se guarda primero (es la fuente
  de verdad) y el aviso va detrás. Si la publicación falla, se registra en el log y el upsert
  igual responde bien.

## Decisión

Se publica **después** de `SaveChanges`, y la publicación es **best-effort**: `PublicadorEventosSns`
captura cualquier error de SNS, lo registra y **no lo propaga**. Un aviso perdido no debe tumbar la
actualización del catálogo ni hacer fallar la ingesta.

El negocio habla con `IPublicadorEventos`, no con el SDK (ADR-029). Sin `SNS_CAMBIOS_OFERTA_ARN`
configurado se usa `PublicadorEventosLog`, que solo escribe el evento en el log: así se trabaja en
local y se demuestra el flujo sin cuenta de AWS.

Los atributos del mensaje SNS van como manda el contrato: `type` siempre y `discountPct` (entero)
solo en `precio.bajo`, porque el filtro de `cep-notificaciones` **solo lee atributos, no el cuerpo**.

## Consecuencias

- Simple y sin piezas nuevas: una interfaz y dos implementaciones.
- **Un evento se puede perder** si SNS está caído justo en ese upsert. Y no se recupera al
  reintentar: el scraper reenvía la misma oferta por el flujo 1, pero para entonces el precio ya
  quedó guardado, así que la comparación no detecta baja y no se vuelve a publicar. Es el costo
  consciente de no tener outbox; para el EP3 se acepta porque el flujo 2 es de avisos, no de dinero.
- Como SNS entrega **al menos una vez** y product-service puede recibir el mismo upsert dos veces
  (SQS Standard en el flujo 1), un cambio podría publicarse dos veces. No se deduplica en el
  productor: el responsable de no mandar dos correos iguales es notification-service, con la tabla
  `notificaciones_enviadas (event_id, owner)` (ver [ADR-030](030-notification-service.md)).
- Si algún día un aviso perdido pesa, el cambio natural es mover `PublicadorEventosSns` a un outbox
  sin tocar al negocio, porque este ya depende de la interfaz y no del SDK.
