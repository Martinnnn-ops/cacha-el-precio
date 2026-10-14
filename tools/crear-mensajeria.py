#!/usr/bin/env python3
"""
Crea (o borra, o muestra) la mensajeria de "Cacha el Precio" en AWS:
colas SQS, colas de mensajes muertos (DLQ), el topico SNS y sus suscripciones.

POR QUE ESTE SCRIPT EXISTE
  Todo lo que se crea sale de infra/mensajeria/topologia.json, que es el UNICO
  lugar donde se declaran colas, topicos y reglas de distribucion (ADR-029).
  Este script no inventa nombres ni valores: solo lee ese archivo y lo aplica.
  Es el puente para que el equipo pruebe los flujos mientras se construye el
  componente de administracion, que hara lo mismo desde la aplicacion.

  Esta en Python y no en bash porque las politicas de SQS y SNS son JSON dentro
  de JSON, y escaparlas a mano en bash es donde nacen los errores. Usa la aws
  cli por debajo (la misma de los otros scripts), asi que no pide instalar boto3.

QUE CREA, EN ESTE ORDEN
  1. Las DLQ (tienen que existir antes que las colas que las usan)
  2. Las colas, con su RedrivePolicy apuntando a su DLQ
  3. El topico SNS
  4. La politica de cada cola que deja escribir a SNS, y las suscripciones
     con su filtro
  5. mensajeria.env con las URL y ARN, para los .env de los servicios

ES IDEMPOTENTE: si algo ya existe, le actualiza los atributos.

Uso:
  python3 tools/crear-mensajeria.py              # crea o actualiza
  python3 tools/crear-mensajeria.py --estado     # cuantos mensajes hay en cada cola
  python3 tools/crear-mensajeria.py --borrar     # borra todo lo declarado

Variables opcionales:
  REGION   (por defecto la de cognito.env, o us-east-1)
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
TOPOLOGIA = RAIZ / "infra" / "mensajeria" / "topologia.json"
SALIDA = RAIZ / "mensajeria.env"


def verde(t: str) -> None: print(f"\033[0;32m{t}\033[0m")
def rojo(t: str) -> None: print(f"\033[0;31m{t}\033[0m")
def gris(t: str) -> None: print(f"\033[0;90m{t}\033[0m")
def titulo(t: str) -> None: print(f"\n\033[1m== {t} ==\033[0m")


def leer_region() -> str:
    if os.environ.get("REGION"):
        return os.environ["REGION"]
    cognito = RAIZ / "cognito.env"
    if cognito.exists():
        for linea in cognito.read_text().splitlines():
            if linea.startswith("COGNITO_REGION="):
                return linea.split("=", 1)[1].strip()
    return "us-east-1"


REGION = leer_region()


class ErrorAws(Exception):
    pass


def aws(*args: str, entrada: dict | None = None) -> dict:
    """Corre la aws cli y devuelve la salida como dict.

    `entrada` se pasa con --cli-input-json y no como banderas sueltas: la aws
    cli v1 (la de AWS Academy) intenta DESCARGAR cualquier parametro que empiece
    con https://, y las URL de las colas empiezan asi. Misma trampa que anota
    tools/crear-api-gateway.sh.
    """
    cmd = ["aws", "--region", REGION, "--output", "json", *args]
    if entrada is not None:
        cmd += ["--cli-input-json", json.dumps(entrada)]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        raise ErrorAws(r.stderr.strip())
    return json.loads(r.stdout) if r.stdout.strip() else {}


# ---------------------------------------------------------------------------
# Lectura de la topologia
# ---------------------------------------------------------------------------
def cargar_topologia() -> dict:
    topo = json.loads(TOPOLOGIA.read_text())
    p = topo["prefix"]
    # Los nombres en el JSON van sin prefijo para que se lean bien;
    # en AWS siempre llevan "cep-" para distinguirlos dentro de la cuenta.
    for d in topo["deadLetterQueues"]:
        d["fullName"] = p + d["name"]
    for q in topo["queues"]:
        q["fullName"] = p + q["name"]
        if q.get("deadLetter"):
            q["deadLetter"]["fullName"] = p + q["deadLetter"]["queue"]
    for t in topo["topics"]:
        t["fullName"] = p + t["name"]
    for s in topo["subscriptions"]:
        s["topicFullName"] = p + s["topic"]
        s["queueFullName"] = p + s["queue"]
    return topo


def variable_env(nombre_completo: str, sufijo: str) -> str:
    """cep-ofertas-crudas-dlq + _URL -> SQS_OFERTAS_CRUDAS_DLQ_URL"""
    base = nombre_completo.removeprefix("cep-").upper().replace("-", "_")
    return f"{sufijo.split('_')[0]}_{base}_{sufijo.split('_', 1)[1]}"


# ---------------------------------------------------------------------------
# Colas
# ---------------------------------------------------------------------------
def url_de_cola(nombre: str) -> str | None:
    try:
        return aws("sqs", "get-queue-url", "--queue-name", nombre)["QueueUrl"]
    except ErrorAws:
        return None


def arn_de_cola(url: str) -> str:
    return aws("sqs", "get-queue-attributes",
               entrada={"QueueUrl": url, "AttributeNames": ["QueueArn"]})["Attributes"]["QueueArn"]


def asegurar_cola(nombre: str, atributos: dict[str, str]) -> str:
    # SSE-SQS: cifrado en reposo con clave administrada por SQS (clase del 7-oct).
    # Es compatible con SNS; una clave KMS administrada por AWS no lo seria.
    atributos = {**atributos, "SqsManagedSseEnabled": "true"}
    url = url_de_cola(nombre)
    if url:
        aws("sqs", "set-queue-attributes", entrada={"QueueUrl": url, "Attributes": atributos})
        gris(f"  ~ {nombre} (ya existia, atributos al dia)")
    else:
        url = aws("sqs", "create-queue",
                  entrada={"QueueName": nombre, "Attributes": atributos})["QueueUrl"]
        verde(f"  + {nombre}")
    return url


# ---------------------------------------------------------------------------
# Modo crear
# ---------------------------------------------------------------------------
def crear(topo: dict) -> None:
    urls: dict[str, str] = {}
    arns: dict[str, str] = {}

    titulo("1. Colas de mensajes muertos (DLQ)")
    for d in topo["deadLetterQueues"]:
        url = asegurar_cola(d["fullName"], {
            "MessageRetentionPeriod": str(d["messageRetentionSeconds"]),
        })
        urls[d["fullName"]] = url
        arns[d["fullName"]] = arn_de_cola(url)

    titulo("2. Colas")
    for q in topo["queues"]:
        atributos = {
            "VisibilityTimeout": str(q["visibilityTimeoutSeconds"]),
            "MessageRetentionPeriod": str(q["messageRetentionSeconds"]),
            "ReceiveMessageWaitTimeSeconds": str(q["receiveWaitTimeSeconds"]),
        }
        dl = q.get("deadLetter")
        if dl:
            if dl["fullName"] not in arns:
                rojo(f"  ! {q['fullName']}: su DLQ {dl['fullName']} no esta declarada")
                sys.exit(1)
            # Tras maxReceiveCount recepciones sin DeleteMessage (sin ACK),
            # SQS mueve el mensaje a la DLQ en vez de seguir reintentando.
            atributos["RedrivePolicy"] = json.dumps({
                "deadLetterTargetArn": arns[dl["fullName"]],
                "maxReceiveCount": str(dl["maxReceiveCount"]),
            })
        url = asegurar_cola(q["fullName"], atributos)
        urls[q["fullName"]] = url
        arns[q["fullName"]] = arn_de_cola(url)
        if dl:
            gris(f"      DLQ {dl['fullName']} tras {dl['maxReceiveCount']} intentos")

    titulo("3. Topicos SNS")
    topicos: dict[str, str] = {}
    for t in topo["topics"]:
        # create-topic es idempotente: si ya existe devuelve el mismo ARN.
        arn = aws("sns", "create-topic", "--name", t["fullName"])["TopicArn"]
        topicos[t["fullName"]] = arn
        verde(f"  = {t['fullName']}")

    titulo("4. Suscripciones (reglas de distribucion)")
    # Una cola solo acepta mensajes de SNS si su politica lo permite, y solo de
    # los topicos que la tienen suscrita (aws:SourceArn). Sin esto SNS publica
    # "bien" y el mensaje nunca llega, sin ningun error visible.
    por_cola: dict[str, list[str]] = {}
    for s in topo["subscriptions"]:
        por_cola.setdefault(s["queueFullName"], []).append(topicos[s["topicFullName"]])
    for cola, origenes in por_cola.items():
        politica = {
            "Version": "2012-10-17",
            "Statement": [{
                "Sid": "PermitirTopicosSuscritos",
                "Effect": "Allow",
                "Principal": {"Service": "sns.amazonaws.com"},
                "Action": "sqs:SendMessage",
                "Resource": arns[cola],
                "Condition": {"ArnEquals": {"aws:SourceArn": sorted(set(origenes))}},
            }],
        }
        aws("sqs", "set-queue-attributes",
            entrada={"QueueUrl": urls[cola], "Attributes": {"Policy": json.dumps(politica)}})
        gris(f"  politica de {cola}: acepta {len(set(origenes))} topico(s)")

    for s in topo["subscriptions"]:
        topico, cola = topicos[s["topicFullName"]], arns[s["queueFullName"]]
        existentes = aws("sns", "list-subscriptions-by-topic", "--topic-arn", topico)["Subscriptions"]
        sub = next((x["SubscriptionArn"] for x in existentes if x["Endpoint"] == cola), None)
        if not sub:
            sub = aws("sns", "subscribe", entrada={
                "TopicArn": topico, "Protocol": "sqs", "Endpoint": cola,
                "ReturnSubscriptionArn": True,
            })["SubscriptionArn"]
            verde(f"  + {s['topicFullName']} -> {s['queueFullName']}")
        else:
            gris(f"  ~ {s['topicFullName']} -> {s['queueFullName']} (ya existia)")

        # Entrega cruda: la cola recibe el JSON del evento tal cual, no el sobre
        # de SNS. Asi el consumidor lee el mismo formato venga de donde venga.
        aws("sns", "set-subscription-attributes", entrada={
            "SubscriptionArn": sub, "AttributeName": "RawMessageDelivery",
            "AttributeValue": "true" if s.get("rawMessageDelivery") else "false",
        })
        # El filtro se evalua sobre los ATRIBUTOS del mensaje, no sobre el cuerpo.
        # Por eso product-service publica discountPct como atributo.
        filtro = s.get("filterPolicy")
        if filtro:
            aws("sns", "set-subscription-attributes", entrada={
                "SubscriptionArn": sub, "AttributeName": "FilterPolicy",
                "AttributeValue": json.dumps(filtro),
            })
            gris(f"      filtro: {json.dumps(filtro)}")
        else:
            gris("      sin filtro: recibe todo")

    titulo("5. mensajeria.env")
    lineas = [
        "# Generado por tools/crear-mensajeria.py. Cambia con cada cuenta de AWS.",
        "# No va al repositorio (.gitignore). Copiar lo que necesite cada servicio a su .env.",
        f"MENSAJERIA_REGION={REGION}",
    ]
    for nombre, url in urls.items():
        lineas.append(f"{variable_env(nombre, 'SQS_URL')}={url}")
    for nombre, arn in topicos.items():
        lineas.append(f"{variable_env(nombre, 'SNS_ARN')}={arn}")
    SALIDA.write_text("\n".join(lineas) + "\n")
    verde(f"  {SALIDA.name} escrito")
    for l in lineas[3:]:
        gris(f"  {l}")


# ---------------------------------------------------------------------------
# Modo estado
# ---------------------------------------------------------------------------
def estado(topo: dict) -> None:
    titulo("Colas")
    print(f"  {'cola':32} {'visibles':>9} {'en proceso':>11}")
    nombres = [q["fullName"] for q in topo["queues"]] + [d["fullName"] for d in topo["deadLetterQueues"]]
    for nombre in nombres:
        url = url_de_cola(nombre)
        if not url:
            rojo(f"  {nombre:32} no existe")
            continue
        a = aws("sqs", "get-queue-attributes", entrada={
            "QueueUrl": url,
            "AttributeNames": ["ApproximateNumberOfMessages", "ApproximateNumberOfMessagesNotVisible"],
        })["Attributes"]
        print(f"  {nombre:32} {a['ApproximateNumberOfMessages']:>9} {a['ApproximateNumberOfMessagesNotVisible']:>11}")
    gris("  visibles = esperando consumidor · en proceso = tomados y aun sin ACK")

    titulo("Topicos")
    existentes = aws("sns", "list-topics").get("Topics", [])
    for t in topo["topics"]:
        arn = next((x["TopicArn"] for x in existentes if x["TopicArn"].endswith(":" + t["fullName"])), None)
        if not arn:
            rojo(f"  {t['fullName']} no existe")
            continue
        subs = aws("sns", "list-subscriptions-by-topic", "--topic-arn", arn)["Subscriptions"]
        verde(f"  {t['fullName']}: {len(subs)} suscripcion(es)")
        for s in subs:
            gris(f"    -> {s['Endpoint'].rsplit(':', 1)[-1]}")


# ---------------------------------------------------------------------------
# Modo borrar
# ---------------------------------------------------------------------------
def borrar(topo: dict) -> None:
    titulo("Borrando")
    existentes = aws("sns", "list-topics").get("Topics", [])
    for t in topo["topics"]:
        arn = next((x["TopicArn"] for x in existentes if x["TopicArn"].endswith(":" + t["fullName"])), None)
        if arn:
            # Borrar el topico borra tambien sus suscripciones.
            aws("sns", "delete-topic", "--topic-arn", arn)
            verde(f"  - {t['fullName']}")
    # Primero las colas y despues las DLQ, al reves de como se crearon.
    nombres = [q["fullName"] for q in topo["queues"]] + [d["fullName"] for d in topo["deadLetterQueues"]]
    for nombre in nombres:
        url = url_de_cola(nombre)
        if url:
            aws("sqs", "delete-queue", entrada={"QueueUrl": url})
            verde(f"  - {nombre}")
        else:
            gris(f"  {nombre} no existia")
    SALIDA.unlink(missing_ok=True)
    gris("  OJO: SQS tarda ~60 s en liberar el nombre de una cola borrada.")
    gris("       Si vuelves a crear antes, falla con QueueDeletedRecently.")


def main() -> None:
    titulo("0. Comprobaciones")
    try:
        cuenta = aws("sts", "get-caller-identity")["Account"]
    except (ErrorAws, FileNotFoundError):
        rojo("Las credenciales no sirven o vencieron (o falta la aws cli).")
        rojo("Learner Lab -> Start Lab -> AWS Details -> AWS CLI: Show,")
        rojo("y pega ese bloque en ~/.aws/credentials reemplazando [default].")
        sys.exit(1)
    verde(f"  cuenta {cuenta}, region {REGION}")
    topo = cargar_topologia()
    gris(f"  topologia v{topo['version']}: {len(topo['queues'])} colas, "
         f"{len(topo['deadLetterQueues'])} DLQ, {len(topo['topics'])} topico(s), "
         f"{len(topo['subscriptions'])} suscripciones")

    modo = sys.argv[1] if len(sys.argv) > 1 else ""
    try:
        if modo == "--estado":
            estado(topo)
        elif modo == "--borrar":
            borrar(topo)
        elif modo == "":
            crear(topo)
        else:
            rojo(f"Opcion desconocida: {modo}. Usa --estado, --borrar o nada.")
            sys.exit(2)
    except ErrorAws as e:
        rojo(f"  AWS respondio con error:\n  {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
