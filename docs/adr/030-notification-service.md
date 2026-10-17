# ADR-030 · notification-service: de dónde salen los seguidores y el correo

Fecha: 16-10-2026 · Estado: aceptado (Fase 2 del EP3).

## Contexto

El [ADR-029](029-mensajeria-sqs-sns.md) creó notification-service como dueño del envío de
correos: consume `cep-notificaciones` y le avisa a quien sigue una prenda cuando baja de precio,
se agota o se repone. Para mandar un correo necesita dos datos que **no están en el mensaje**:
quiénes siguen esa prenda, y cuál es su correo.

Los seguidores viven en product-service: un «deseado» es un `PersonalItem` con `Kind="deseado"`
y `Key=productId` (ADR-028). El correo no está en la base de datos: la identidad la administra
Cognito, y `PersonalItem.Owner` es el `sub`, no el email.

## Alternativas

- **notification-service lee el esquema `product` directo.** Un servicio menos al que llamar,
  pero acopla dos servicios por la base: cualquier cambio de esquema en product-service puede
  romper al de notificaciones en silencio, y borra el límite entre los dos.
- **product-service mete los correos en el evento.** Evita la consulta, pero obliga al productor
  a conocer a los seguidores y a cargar datos personales en cada mensaje de la cola y de
  auditoría, donde no hacen falta y quedan retenidos días.
- **notification-service pregunta por HTTP interno + Cognito.** Un salto más por mensaje, pero
  cada servicio sigue siendo dueño de lo suyo.

## Decisión

notification-service **no toca el esquema `product`**. Resuelve los dos datos así:

1. **Seguidores:** `GET /internal/personal/seguidores/{productId}` en product-service, que
   devuelve los `sub` con `Kind="deseado"` y `Key=productId`. Va solo por la red interna del
   compose; **no se enruta en el API Gateway**.
2. **Correo:** `sub → email` con `ListUsers` de Cognito (`Filter: sub = "…"`), usando el rol de
   la EC2. Se cachea **1 hora** en memoria para no pegarle a Cognito por cada mensaje. A quien no
   tenga el correo **verificado** se lo salta, con log. En local, sin cuenta de AWS, un mapa de
   configuración (`CorreosPrueba`) cumple el mismo rol para poder demostrar el flujo en modo log.

El servicio es dueño de su esquema `notification`, con la tabla `notificaciones_enviadas`
`(event_id, owner)` para no reenviar ante una entrega doble de SQS.

## Consecuencias

- product-service y notification-service quedan desacoplados: se comunican por un contrato HTTP
  explícito, no por tablas compartidas.
- Hay una dependencia nueva: si product-service o Cognito están caídos, el aviso **no se pierde**,
  se trata como error recuperable y el mensaje se reintenta.
- Queda pendiente confirmar en la EC2 que `LabRole` permite `cognito-idp:ListUsers`. Si no, la
  alternativa es que el gateway guarde el correo al momento de seguir (consultando
  `/oauth2/userInfo`); el resto de esta decisión no cambia.
- **Una dirección rechazada por el servidor (SMTP 550/553) se registra y se salta a esa persona**,
  no manda el evento entero a la DLQ: un correo malo no debe bloquear al resto de los seguidores.
  A la DLQ van los errores que arruinan el mensaje completo: JSON roto, `type`/`version`
  desconocido o `data.productId` ausente.
- El endpoint interno expone `sub` (y en diagnóstico, correos): por eso vive solo en la red
  privada y el de diagnóstico, únicamente en entorno de desarrollo.
