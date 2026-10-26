# notification-service

Avisa por correo a quien sigue una prenda cuando **baja de precio**, se **agota** o se **repone**.
Es el consumidor del flujo 2 de la mensajería (ver [`docs/MENSAJERIA.md`](../docs/MENSAJERIA.md) y
[ADR-029](../docs/adr/029-mensajeria-sqs-sns.md) / [ADR-030](../docs/adr/030-notification-service.md)).

## Qué hace

```
SNS cep-cambios-oferta ─(filtro)─▶ SQS cep-notificaciones ─▶ notification-service ─SMTP─▶ Gmail
                                                                    │
                                   product-service /internal/… ◀────┤ ¿quién sigue la prenda?
                                   Cognito ListUsers           ◀────┘ ¿cuál es su correo?
```

Por cada mensaje: valida el sobre, pregunta a product-service quién sigue la prenda, traduce cada
`sub` a su correo con Cognito, y manda un correo por persona **sin repetir** (tabla
`notificaciones_enviadas`). El mensaje se borra de la cola recién cuando todos quedaron enviados o
saltados.

## Estructura

| Carpeta | Qué hay |
|---|---|
| `Consumidor/` | `ConsumidorNotificaciones`: el bucle de long polling y el ACK/DLQ |
| `Service/` | `ServicioNotificaciones`: el trabajo de un mensaje (validar → seguidores → correos → enviar) |
| `Mensajeria/` | `IColaMensajes` y su implementación SQS. El negocio no habla con el SDK |
| `Correo/` | `IEnviadorCorreo` (`smtp` con MailKit / `log`), las plantillas HTML/texto y las rutas de la bandeja (`EndpointsBandeja`) |
| `Seguidores/` | cliente HTTP a product-service y `sub → email` por Cognito (o config en local) |
| `Repository/` | la marca `(event_id, owner)` que evita reenviar, y la bandeja de correos generados (`BandejaCorreos`) |
| `Data/` + `Migrations/` | esquema propio `notification`, aplicado con `Migrate()` al arrancar |

## Bandeja de correos

Cada correo generado se copia en `notification.correos_generados` (se guardan los últimos 50), para
verlo en el panel de mensajería aunque el servicio corra con `CORREO_MODO=log`. Se lee por
`GET /internal/correos`, `/internal/correos/{id}` y `/internal/correos/ejemplo/{tipo}`, este último
con una prenda inventada y sin enviar nada. El destinatario sale enmascarado. Si la copia falla, el
aviso igual se da por enviado. Detalle en
[`docs/MENSAJERIA.md`](../docs/MENSAJERIA.md#bandeja-de-correos).

## Errores (resumen; el detalle está en MENSAJERIA.md)

- **Recuperable** (BD/Cognito/product-service caídos, Gmail no responde, timeout): no se borra el
  mensaje; reaparece a los 60 s. En el **último** intento (el `maxReceiveCount` de la cola, leído de
  su `RedrivePolicy`) el consumidor lo aparta en la DLQ con `error-type=recuperable-agotado` y la
  causa de fondo, para que desde el panel se sepa por qué falló. Si no puede leer el máximo, lo deja
  como antes: SQS lo mueve solo, sin atributos.
- **Definitivo** (JSON roto, `type`/`version` desconocido, `productId` ausente): va a la DLQ con
  atributos de diagnóstico y se borra, sin gastar reintentos.
- Una **dirección inválida** (SMTP 550/553) se registra y se salta a esa persona; no manda todo el
  evento a la DLQ.

## Pruebas

```bash
dotnet test notification-service.Tests
```

Prueban el consumidor con el `ServicioNotificaciones` real y todo lo demás falso (cola, seguidores,
directorio, repositorio, bandeja y un enviador de correo en memoria): éxito, definitivo, reintento,
último intento con `recuperable-agotado`, causa de fondo y apagado. También la bandeja: que guarde
la copia con su modo, que su falla no reintente el mensaje y que no duplique la copia. En
`BandejaCorreosTests`, el enmascarado, los ejemplos y que **el snapshot de migraciones calce con el
modelo**: la migración de la bandeja se escribió a mano, sin `dotnet-ef`. No necesitan AWS, Gmail
ni base.

## Configuración

Todo por variables de entorno (ver [`.env.example`](../.env.example)):

| Variable | Para qué |
|---|---|
| `ConnectionStrings__PostgreSql` | Postgres con `Search Path=notification,public` |
| `PRODUCT_URL` | dónde preguntar por los seguidores (host interno del compose) |
| `COGNITO_USER_POOL_ID`, `AWS_REGION` | `sub → email` con Cognito; sin pool usa `CorreosPrueba` |
| `SQS_NOTIFICACIONES_URL`, `SQS_NOTIFICACIONES_DLQ_URL` | la cola y su DLQ; vacías ⇒ el consumidor no arranca |
| `CORREO_MODO` | `log` (no envía, solo registra) o `smtp` (Gmail real) |
| `SMTP_USUARIO`, `SMTP_CLAVE` | cuenta de Gmail y contraseña de **aplicación**; con `smtp` son obligatorias |
| `SITIO_URL` | base de los enlaces «Ver el detalle» del correo |

## Correr en local

```bash
# Con la BD del compose arriba:
docker compose up -d postgres product-service notification-service
curl -s localhost:8082/health        # -> Healthy

# Sin cola configurada, el consumidor no arranca (es lo normal en local).
# Para probar la cadena productId -> seguidores -> correos (solo en Development):
curl -s localhost:8082/internal/diagnostico/seguidores/123
```

Para ver un correo de verdad en el log sin AWS: deja `CORREO_MODO=log`, agrega el `sub` de un
usuario de prueba a `CorreosPrueba` en `appsettings.Development.json`, y manda un mensaje de prueba
a la cola (o usa las pruebas de la T09 cuando estén).
