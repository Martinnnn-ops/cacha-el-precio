# Streaming (EA3/EFT): el stream de Kinesis cep-precios.
#
# Por que Kinesis y no otra cola: SQS cep-ofertas-crudas es TRABAJO (guardar cada
# oferta, se borra al leerla). Kinesis es ANALISIS: cada precio capturado se queda
# 24 h, varios lectores pueden releerlo con su propio marcador, y se lee en lotes.
# Con eso product-service calcula el "precio habitual" de cada oferta. Ver
# docs/PLAN-EFT.md §5 y ADR-035.
#
# El scraper escribe en el stream con put_record despues de aceptar cada prenda
# (igual que ya sube imagenes a S3 con el perfil de la EC2); product-service lo lee
# en segundo plano con LectorPrecios. La DLQ cep-precios-dlq se declara en
# topologia.json como las demas: Kinesis no trae DLQ propia, la arma el lector.
#
# ⚠️ COBRA POR SHARD-HORA aunque la EC2 este apagada (~US$11/mes con 1 shard). Por
# eso el stream se gatilla con `encendido`: `tools/tf.sh apagar` lo destruye junto
# con la EC2. El precio igual quedo en el catalogo por el flujo 1, asi que perder
# el stream unas horas no pierde datos, solo pausa el calculo del precio habitual.

resource "aws_kinesis_stream" "precios" {
  count = var.encendido ? 1 : 0

  name = "${local.prefijo}precios"

  # 1 shard = 1.000 registros/s y 1 MiB/s de escritura. Con 8 tiendas barridas
  # cada 3 h sobra de lejos; subir a 2 shards es cambiar este numero.
  shard_count = 1

  # 24 h: una caida del lector de medio dia se recupera sin perder precios. Mas
  # retencion cobra extra por shard-hora.
  retention_period = 24

  # Sin cifrado administrado por KMS a proposito: una clave CMK suma permisos de
  # kms:* que el Learner Lab podria negar, y el stream no lleva datos personales
  # (solo tienda, id de producto y precio). Si el sondeo (6a) muestra que KMS pasa,
  # se cambia a encryption_type = "KMS" con la clave administrada alias/aws/kinesis.
  stream_mode_details {
    stream_mode = "PROVISIONED"
  }
}

# Si el lector se cae o se queda atras, los registros se borran a las 24 h. Esta
# alarma mira la edad del ultimo registro leido (iterator age): si pasa de 1 h, el
# lector no esta siguiendo el ritmo. Sin accion (no manda correo): se ve en la
# consola y en el tablero de Grafana por CloudWatch (docs/PLAN-EFT.md §8).
resource "aws_cloudwatch_metric_alarm" "precios_atrasado" {
  count = var.encendido ? 1 : 0

  alarm_name          = "${local.prefijo}precios-lector-atrasado"
  alarm_description   = "El lector de cep-precios se quedo atras: los registros se borran a las 24 h."
  namespace           = "AWS/Kinesis"
  metric_name         = "GetRecords.IteratorAgeMilliseconds"
  statistic           = "Maximum"
  comparison_operator = "GreaterThanThreshold"
  threshold           = 3600000 # 1 h en ms
  period              = 300
  evaluation_periods  = 2
  treat_missing_data  = "notBreaching"

  dimensions = {
    StreamName = aws_kinesis_stream.precios[0].name
  }
}

# Lo que va a mensajeria.env, con el mismo estilo que el output de mensajeria.tf.
# Vacio cuando `encendido = false` (el stream no existe): el scraper y el lector
# ven KINESIS_PRECIOS_STREAM vacio y, como SQS sin URL, no arrancan esa parte.
output "streaming_env" {
  description = "Variables del stream para mensajeria.env. Vacias cuando encendido = false."
  value = {
    KINESIS_PRECIOS_STREAM = try(aws_kinesis_stream.precios[0].name, "")
    KINESIS_REGION         = "us-east-1"
  }
}
