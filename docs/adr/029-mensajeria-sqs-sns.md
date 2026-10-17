# ADR-029 · Mensajería asíncrona con SQS y SNS

Fecha: 14-10-2026 · Estado: aceptado (opción A confirmada en Academy, ver al final).

## Contexto

El [ADR-020](020-csharp-y-simplificacion-de-servicios.md) retiró RabbitMQ porque no tenía
productores ni consumidores, y dejó al scraper llamando a product-service por HTTP. Ese mismo
ADR anotó el costo: HTTP directo no ofrece buffering, reintentos durables ni backpressure. Si
product-service está caído o se está redesplegando durante un barrido, la captura se pierde.

El EP3 pide productores y consumidores conectados por mensajería, con destinos, reglas de
distribución y tratamiento de mensajes fallidos, y un componente que administre esos recursos.
Además se quiere avisar por correo a quien sigue una prenda cuando baja de precio, se agota o vuelve a estar disponible.

## Alternativas

- **RabbitMQ en la EC2** (contenedor). Portable (AMQP) y es lo que se vio en clases, pero es un
  proceso más que operar, respaldar y vigilar en una sola máquina, y se cae junto con ella.
- **Amazon SQS + SNS.** Gestionados: no hay broker que mantener, la DLQ y los reintentos son
  configuración, y el fan-out con filtros es nativo. Amarra el sistema a AWS.
- **Mantener HTTP directo** con reintentos en el scraper. No cumple el encargo y los reintentos
  viven en memoria del proceso: si el scraper también se reinicia, se pierden.

## Decisión

Se adopta **SQS + SNS**, con dos flujos independientes:

1. **Ingesta (SQS).** El scraper publica `oferta.capturada` en `cep-ofertas-crudas` y
   product-service la consume. El argumento es **resiliencia, no volumen**: si product-service
   no está, los mensajes esperan en la cola y se procesan al volver.
2. **Notificaciones (SNS fan-out).** product-service publica los cambios de una oferta
   (`precio.bajo`, `oferta.agotada`, `oferta.repuesta`) en el tópico `cep-cambios-oferta`. SNS
   los copia según reglas de suscripción: a `cep-notificaciones` llegan las bajas de 10% o más
   y todos los agotados y reposiciones; a `cep-auditoria-precios` llega todo. product-service no
   sabe quién escucha: agregar un canal es suscribir otra cola, y agregar un tipo de aviso es
   sumarlo al filtro, sin tocar notification-service.

Se crea **notification-service**, un microservicio nuevo, dueño de los seguimientos y del envío
de correos por SMTP de Gmail, detrás de una interfaz `IEnviadorCorreo` con una implementación
real y otra que solo registra en el log.

Toda la topología se declara en **un solo archivo**, `infra/mensajeria/topologia.json`. Lo
aplican `tools/crear-mensajeria.py` y el componente de administración; ningún servicio crea
colas ni tópicos por su cuenta.

Reglas de procesamiento (detalle en [MENSAJERIA.md](../MENSAJERIA.md)):

- El ACK es `DeleteMessage`, y se hace **después** de guardar o enviar.
- Error **recuperable** → no se borra; SQS lo reintenta y al 5.º intento lo mueve a la DLQ.
- Error **definitivo** → se manda a la DLQ con atributos de diagnóstico y se borra, sin gastar
  reintentos que van a fallar igual.
- Colas **Standard**, no FIFO: el orden no importa (el upsert usa el último precio) y la
  entrega «al menos una vez» se cubre con idempotencia: upsert + `UNIQUE` en product-service y
  la tabla `notificaciones_enviadas` en notification-service.

## Consecuencias

- Una captura ya no se pierde si product-service está caído; queda en la cola hasta 4 días.
- La consistencia pasa a ser **eventual**: una prenda aparece en el sitio segundos después de
  capturarse, no en el mismo request.
- Hay más piezas que vigilar: las DLQ deben revisarse, y el redrive es manual.
- El sistema queda atado a AWS. Migrar a RabbitMQ significaría reescribir productores y
  consumidores; por eso el negocio habla con interfaces (`IPublicadorEventos`) y no con el SDK.
- `POST /api/products` directo se mantiene como respaldo mientras la cola se estabiliza.
- Costo: ~22.500 mensajes diarios (≈7.500 prendas × 3 barridos), con envío, recepción y borrado
  supera el millón gratuito al mes, pero a USD 0,40 por millón queda bajo un dólar. Cálculo
  propio, sin verificar en la calculadora de AWS.
- **Limitación del productor:** product-service publica los cambios de oferta *después* de
  guardar y en modo best-effort. Si SNS falla justo en ese upsert, el precio queda bien pero el
  aviso **se pierde** y no se recupera al reintentar (no hay outbox). Es una decisión consciente
  por el alcance del EP3; el detalle y la alternativa están en
  [ADR-031](031-publicacion-cambios-oferta.md).
## Comprobado en AWS Academy (14-10-2026, cuenta `731705830984`)

- **Opción A confirmada.** `tools/probar-mensajeria-academy.py` respondió HTTP 200 y el mensaje
  llegó a la cola: API Gateway escribe directo en SQS asumiendo `LabRole`, cuya trust policy ya
  incluye `apigateway.amazonaws.com`. La ruta `POST /ingesta` va a la cola sin código nuevo en
  el gateway.
- **Trampa encontrada:** la aws cli v1 de Academy devuelve la URL de la cola en el formato
  antiguo (`https://queue.amazonaws.com/<cuenta>/<cola>`). La cli la acepta, pero la integración
  `SQS-SendMessage` responde **400 Bad Request** y el mensaje no llega. Los scripts la pasan a
  `https://sqs.<region>.amazonaws.com/<cuenta>/<cola>` antes de usarla (`url_moderna`).
- **Correo:** desde la EC2 el puerto 587 de `smtp.gmail.com` sale y negocia STARTTLS (TLS 1.3).
  El 25 está bloqueado, como documenta AWS. notification-service puede usar Gmail por SMTP.
