# Mensajeria (fase 1): colas SQS, colas de mensajes muertos (DLQ), el topico SNS y
# sus suscripciones. Reemplaza a tools/crear-mensajeria.py.
#
# Igual que el script, no inventa nombres ni valores: todo sale de
# infra/mensajeria/topologia.json, que sigue siendo el UNICO lugar donde se declara
# la mensajeria (ADR-029). Para agregar una cola se edita ese JSON, no este archivo.
#
# Las URL de SQS y los ARN de SNS dependen solo de la cuenta y el nombre, asi que
# salen iguales a las que dejaba el script: mensajeria.env, el API Gateway
# (/ingesta) y los .env de los servicios no cambian.

locals {
  topologia = jsondecode(file("${path.module}/../../mensajeria/topologia.json"))
  prefijo   = local.topologia.prefix

  # El JSON trae objetos con campos distintos (una cola con DLQ y otra sin ella).
  # Se normalizan a la misma forma para que Terraform los pueda recorrer con for_each.
  dlqs = {
    for d in local.topologia.deadLetterQueues : d.name => {
      retencion = d.messageRetentionSeconds
    }
  }

  colas = {
    for q in local.topologia.queues : q.name => {
      visibilidad = q.visibilityTimeoutSeconds
      retencion   = q.messageRetentionSeconds
      espera      = q.receiveWaitTimeSeconds
      dlq         = try(q.deadLetter.queue, null)
      intentos    = try(q.deadLetter.maxReceiveCount, null)
    }
  }

  tamano_maximo = 1048576

  topicos = toset([for t in local.topologia.topics : t.name])

  suscripciones = {
    for s in local.topologia.subscriptions : "${s.topic}->${s.queue}" => {
      topico = s.topic
      cola   = s.queue
      cruda  = try(s.rawMessageDelivery, false)
      filtro = try(s.filterPolicy, null) == null ? null : jsonencode(s.filterPolicy)
    }
  }

  # Que topicos escriben en cada cola. Con esto se arma la politica de la cola.
  topicos_por_cola = {
    for s in local.topologia.subscriptions : s.queue => s.topic...
  }
}

# ── 1. Colas de mensajes muertos ──────────────────────────────────────────────
resource "aws_sqs_queue" "dlq" {
  for_each = local.dlqs

  name                      = "${local.prefijo}${each.key}"
  message_retention_seconds = each.value.retencion

  # SSE-SQS: cifrado en reposo con clave administrada por SQS (clase del 7-oct).
  # Es compatible con SNS; una clave KMS administrada por AWS no lo seria.
  sqs_managed_sse_enabled = true

  # 1 MiB, el maximo actual de SQS y lo que trae una cola creada por la CLI. El
  # proveedor asume 256 KiB (el limite antiguo) y sin esto achicaria la cola.
  max_message_size = local.tamano_maximo
}

# ── 2. Colas ──────────────────────────────────────────────────────────────────
resource "aws_sqs_queue" "cola" {
  for_each = local.colas

  name                       = "${local.prefijo}${each.key}"
  visibility_timeout_seconds = each.value.visibilidad
  message_retention_seconds  = each.value.retencion
  receive_wait_time_seconds  = each.value.espera
  sqs_managed_sse_enabled    = true
  max_message_size           = local.tamano_maximo

  # Tras maxReceiveCount recepciones sin DeleteMessage (sin ACK), SQS mueve el
  # mensaje a la DLQ en vez de seguir reintentando.
  redrive_policy = each.value.dlq == null ? null : jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dlq[each.value.dlq].arn
    maxReceiveCount     = each.value.intentos
  })

  lifecycle {
    precondition {
      condition     = each.value.dlq == null || contains(keys(local.dlqs), coalesce(each.value.dlq, "-"))
      error_message = "La cola ${each.key} apunta a una DLQ que no esta declarada en topologia.json."
    }
  }
}

# ── 3. Topicos SNS ────────────────────────────────────────────────────────────
resource "aws_sns_topic" "topico" {
  for_each = local.topicos

  name = "${local.prefijo}${each.key}"
}

# ── 4. Reglas de distribucion ─────────────────────────────────────────────────
# Una cola solo acepta mensajes de SNS si su politica lo permite, y solo de los
# topicos que la tienen suscrita (aws:SourceArn). Sin esto SNS publica "bien" y el
# mensaje nunca llega, sin ningun error visible.
resource "aws_sqs_queue_policy" "desde_sns" {
  for_each = local.topicos_por_cola

  queue_url = aws_sqs_queue.cola[each.key].id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Sid       = "PermitirTopicosSuscritos"
      Effect    = "Allow"
      Principal = { Service = "sns.amazonaws.com" }
      Action    = "sqs:SendMessage"
      Resource  = aws_sqs_queue.cola[each.key].arn
      Condition = {
        ArnEquals = {
          "aws:SourceArn" = sort(distinct([for t in each.value : aws_sns_topic.topico[t].arn]))
        }
      }
    }]
  })
}

resource "aws_sns_topic_subscription" "suscripcion" {
  for_each = local.suscripciones

  topic_arn = aws_sns_topic.topico[each.value.topico].arn
  protocol  = "sqs"
  endpoint  = aws_sqs_queue.cola[each.value.cola].arn

  # Entrega cruda: la cola recibe el JSON del evento tal cual, no el sobre de SNS.
  raw_message_delivery = each.value.cruda

  # El filtro se evalua sobre los ATRIBUTOS del mensaje, no sobre el cuerpo.
  # Por eso product-service publica discountPct como atributo.
  filter_policy = each.value.filtro

  # La suscripcion se crea despues de la politica: si llega un mensaje entremedio,
  # la cola ya lo acepta.
  depends_on = [aws_sqs_queue_policy.desde_sns]
}

# ── 5. Lo que va a mensajeria.env ─────────────────────────────────────────────
# Mismos nombres de variable que escribia el script
# (cep-ofertas-crudas-dlq -> SQS_OFERTAS_CRUDAS_DLQ_URL). La fase 5 los escribe al
# archivo; por ahora se ven con `terraform output mensajeria_env`.
output "mensajeria_env" {
  description = "Variables de mensajeria.env, con los mismos nombres que dejaba tools/crear-mensajeria.py."
  value = merge(
    { MENSAJERIA_REGION = "us-east-1" },
    { for k, q in aws_sqs_queue.dlq : "SQS_${upper(replace(k, "-", "_"))}_URL" => q.url },
    { for k, q in aws_sqs_queue.cola : "SQS_${upper(replace(k, "-", "_"))}_URL" => q.url },
    { for k, t in aws_sns_topic.topico : "SNS_${upper(replace(k, "-", "_"))}_ARN" => t.arn },
  )
}
