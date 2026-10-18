# ADR-032 · Administración de mensajería dentro del gateway

Fecha: 18-10-2026 · Estado: aceptado (T11 del EP3).

## Contexto

La pauta del EP3 pide un componente que **consulte y gestione** colas, tópicos y reglas de
distribución (13%), que **encapsule** crear, modificar y eliminar esos recursos sin exponer
detalles internos (10%) y que **valide** las solicitudes con respuestas claras (7%). Hasta ahora
eso lo hacía `tools/crear-mensajeria.py` desde la terminal, leyendo
`infra/mensajeria/topologia.json` ([ADR-029](029-mensajeria-sqs-sns.md)).

Hay que decidir dónde vive ese componente, quién puede usarlo y cómo se prueba sin una cuenta de
AWS encendida.

## Alternativas

- **Un servicio nuevo (`messaging-admin`)** detrás del gateway. Se probó en la primera versión de la
  T11: un contenedor más, un puerto más, un proxy más en el gateway y otra imagen que desplegar,
  para algo que solo usa el grupo admin de vez en cuando.
- **Dentro de product-service.** Mezcla la administración de la infraestructura con el catálogo, y
  product-service no conoce la identidad del usuario (eso lo valida el gateway).
- **Dentro del gateway**, bajo `/api/admin/mensajeria`, con la misma regla de rol que ya usa
  `/api/admin/diagnostico`.

## Decisión

Vive **en el gateway** (carpeta `gateway/Mensajeria/`), en tres capas:

| Capa | Pieza | Qué hace |
|---|---|---|
| HTTP | `EndpointsMensajeria` | Traduce HTTP a métodos y las excepciones a `{ estado, error, mensaje }`; el 400 trae `errores: [{ campo, error }]`. Nunca devuelve un ARN ni un stack trace |
| Reglas | `ServicioAdministracion` + `ValidadorSolicitudes` | Forma (función pura: prefijo `cep-`, rangos de AWS, filtro solo sobre `type`/`discountPct`) y estado (la DLQ existe, no se repite, no se borra lo que otro usa) |
| Infraestructura | `IAdministradorMensajeria` | **Único** lugar que llama al SDK de SQS/SNS para administrar. Dos implementaciones: AWS y memoria |

- **Acceso:** el API Gateway exige un token válido (rutas `jwt`) y el gateway exige el grupo `admin`
  de Cognito. Un usuario normal recibe 403.
- **Lo declarado en `topologia.json` está protegido:** borrarlo pide `?forzar=true`. Una DLQ en uso,
  o una cola o tópico con suscripciones, no se borra ni forzando: primero se quita lo que depende.
- **`POST /topologia/aplicar`** hace lo mismo que `crear-mensajeria.py`, desde la aplicación, y es
  idempotente. `GET /topologia` compara lo declarado con lo que existe.
- **Redrive:** se intenta `StartMessageMoveTask`; si el Learner Lab no lo permite, se mueve a mano
  (recibir, reenviar y recién ahí borrar de la DLQ).
- **`MENSAJERIA_MODO=memoria`** simula SQS/SNS: arranca con la topología aplicada y un par de fallos
  en las DLQ. Sirve para programar la vista de la T12 y para los tests, sin AWS.

## Consecuencias

- Un contenedor menos que con un servicio aparte, y la autorización queda en un solo lugar.
- El gateway ahora depende de `AWSSDK.SQS` y `AWSSDK.SimpleNotificationService`, y su imagen lleva
  `topologia.json` (`/app/topologia.json`).
- Las credenciales salen del LabRole de la EC2: si Academy niega una operación, la respuesta es un
  502 «Permiso denegado por AWS» y el detalle queda en el log.
- El modo memoria **no reemplaza** a SQS: no hay visibilidad, reintentos ni DLQ automática. Solo
  imita lo que el panel necesita ver.
- Probado sin AWS en `gateway.Tests`: 62 tests de reglas y validación, y 25 por HTTP contra el
  gateway completo (401, 403, 400 con `{ campo, error }`, 409, 404 y la topología aplicada dos
  veces). La demo en vivo está en `gateway/Mensajeria.http`.
