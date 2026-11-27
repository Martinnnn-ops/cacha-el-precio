# ADR-035 · Kinesis para los precios capturados

Fecha: 24-11-2026 · Estado: aceptado (EFT). Encargo del profe del 10-11-2026.

## Contexto

El profe pidió el 10-11 avanzar en **Kinesis** dentro del proyecto. Kinesis es para
*stream de datos*: muchos registros que valen poco por separado y se procesan en lotes,
donde varios lectores pueden releer la misma ventana. Hay que usarlo donde eso tenga
sentido y no duplicar lo que ya hacen SQS/SNS ([ADR-029](029-mensajeria-sqs-sns.md)).

El candidato natural son **los precios capturados**: 8 tiendas barridas cada 3 h producen
un chorro de precios cuyo interés no es cada dato suelto, sino cómo se mueven en el
tiempo. Con eso se calcula el **precio habitual** de cada oferta, que la ficha muestra
como «bajo / dentro / sobre su rango habitual». El plan completo está en
[PLAN-EFT §5](../PLAN-EFT.md#5--kinesis-los-precios-capturados).

La cola SQS `cep-ofertas-crudas` **no** sirve para esto: es trabajo (guardar cada oferta,
el mensaje se borra al leerlo, lo lee uno solo). El stream es análisis (el dato se queda
24 h, lo relee quien quiera con su propio marcador, en lotes).

## Alternativas

- **Kafka (MSK gestionado).** Es lo que decían los documentos viejos del ramo, pero MSK
  cuesta ~US$540/mes, no aparece en los permisos del Academy y un broker propio en la EC2
  se come ~1 GB de RAM. Descartado en clase el 17-11.
- **Mandar el precio por la misma cola SQS y calcular ahí.** Mezcla trabajo con análisis,
  obliga a que un solo consumidor haga las dos cosas y pierde el poder releer la ventana.
- **Kinesis con un shard, el scraper escribe y product-service lee.** Nativo de AWS,
  coherente con SQS/SNS, ~US$11/mes con 1 shard, y se apaga con `tf.sh apagar`.

## Decisión

Un stream **Kinesis `cep-precios`** de **1 shard** y **24 h** de retención
(`infra/terraform/cuenta/streaming.tf`), gatillado por `encendido`: `tf.sh apagar` lo
destruye porque cobra por shard-hora aunque la EC2 esté apagada.

- **Quién escribe:** el scraper, después de aceptar y publicar cada prenda, manda el
  precio con `put_record` (boto3, con el perfil de la EC2, igual que ya sube imágenes a
  S3). Es **de mejor esfuerzo**: si Kinesis falla, se registra y el barrido sigue; el
  precio no se pierde porque ya quedó en el catálogo por el flujo 1. La partition key es
  `tienda#idExterno`, para que las capturas de una misma oferta vayan en orden.
- **Quién lee:** `LectorPrecios`, un servicio en segundo plano de product-service que lee
  en lotes, **anota su checkpoint** (número de secuencia por shard) en la base y va
  sumando cada precio a `estadisticas_precio` (muestras, suma, suma de cuadrados, mínimo,
  máximo). De ahí sale el rango habitual = promedio ± una desviación.
- **Idempotencia:** Kinesis entrega *al menos una vez*. La **técnica** la da la tabla
  `eventos_precio_procesados` (un `eventId` ya sumado no se vuelve a sumar), que se poda
  sola porque el stream solo retiene 24 h. La **de negocio** es la misma de siempre: la
  identidad `(tienda, idExterno)`.
- **DLQ:** Kinesis no trae cola de muertos propia. El lector la arma: un registro con JSON
  roto o precio inválido va a `cep-precios-dlq` (declarada en `topologia.json`) con el
  motivo, y el checkpoint avanza igual para no trabar el shard. Un fallo recuperable (base
  caída) se reintenta 1 s → 2 s → 4 s y, si no cede, también va a la DLQ
  ([PLAN-EFT §6](../PLAN-EFT.md#6--los-mensajes-que-fallan-dlq)).

El negocio no toca el SDK: `ILectorStream`, `IBuzonErrores` e `IProcesadorPrecio` son las
costuras, con dobles en las pruebas. Sin `KINESIS_PRECIOS_STREAM` el lector no arranca y el
servicio sigue atendiendo HTTP, igual que el consumidor de SQS sin su URL.

## Consecuencias

- **Cobra por hora**, se use o no (~US$11/mes con 1 shard). Mitigado: `tf.sh apagar` lo
  destruye; `tf.sh subir` lo vuelve a crear.
- Si el lector se cae, los registros se borran a las 24 h. Mitigado: la alarma de
  *iterator age* avisa (se ve en Grafana por CloudWatch), y el precio igual quedó en el
  catálogo por el flujo 1, así que solo se pausa el cálculo del precio habitual.
- **Duplicados**: cubiertos por `eventos_precio_procesados`; aunque uno se colara, recalcular
  el promedio dos veces con el mismo dato no cambia la clasificación de forma visible.
- **Sin cifrado KMS** en el stream a propósito (evita permisos de `kms:*` que el Academy
  podría negar): no lleva datos personales, solo tienda, id de producto y precio. Si el
  sondeo de permisos muestra que KMS pasa, se activa sin tocar el resto.
- **Pendiente de permisos del Academy (6a):** antes de desplegar hay que confirmar con el
  sondeo (`infra/terraform/sondeo/`) que `LabRole` deja crear un stream y hacer
  `put-record` / `get-records`. El código queda listo; si el permiso no estuviera, el plan B
  es dejarlo apagado y presentar el flujo contra un stream creado a mano para la demo.
