#!/usr/bin/env python3
"""
Sondeo de AWS Academy para la mensajeria del EP3 (Fase 0).

Responde una sola pregunta: ¿el API Gateway puede escribir DIRECTO en una cola
SQS usando LabRole? De eso depende como entra el scraper (ADR-029):

  Opcion A  scraper -> API Gateway (POST /ingesta) -> SQS        sin codigo nuevo
  Opcion B  scraper -> API Gateway -> gateway (C#) -> SQS        el BFF publica

En Academy no se pueden crear roles, asi que la integracion SQS-SendMessage
tiene que asumir LabRole. Si su trust policy no acepta a apigateway.amazonaws.com,
el API Gateway responde 500 y la opcion A queda descartada.

Crea una cola y una HTTP API de prueba, manda un mensaje, mira si llego, y BORRA
todo al final, pase lo que pase. No toca la API real ni las colas cep-*.

Uso:
  python3 tools/probar-mensajeria-academy.py
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request

REGION = os.environ.get("REGION", "us-east-1")
SELLO = time.strftime("%Y%m%d%H%M%S")
COLA = f"prueba-ep3-{SELLO}"
API = f"prueba-ep3-{SELLO}"


def verde(t: str) -> None: print(f"\033[0;32m{t}\033[0m")
def rojo(t: str) -> None: print(f"\033[0;31m{t}\033[0m")
def amar(t: str) -> None: print(f"\033[0;33m{t}\033[0m")
def gris(t: str) -> None: print(f"\033[0;90m{t}\033[0m")
def titulo(t: str) -> None: print(f"\n\033[1m== {t} ==\033[0m")


def aws(*args: str, entrada: dict | None = None) -> dict:
    # --cli-input-json por la trampa de la aws cli v1 con los parametros https://
    cmd = ["aws", "--region", REGION, "--output", "json", *args]
    if entrada is not None:
        cmd += ["--cli-input-json", json.dumps(entrada)]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        raise RuntimeError(r.stderr.strip())
    return json.loads(r.stdout) if r.stdout.strip() else {}


def url_moderna(url: str) -> str:
    """Pasa la URL de una cola al formato https://sqs.<region>.amazonaws.com/<cuenta>/<cola>.

    La aws cli v1 devuelve el formato antiguo (https://queue.amazonaws.com/...), que
    la cli sigue aceptando pero la integracion SQS-SendMessage del API Gateway no:
    responde 400 Bad Request y el mensaje nunca llega (probado el 14-oct).
    """
    cuenta, cola = url.rstrip("/").split("/")[-2:]
    return f"https://sqs.{REGION}.amazonaws.com/{cuenta}/{cola}"


def main() -> int:
    url_cola = api_id = None
    try:
        titulo("0. Credenciales")
        try:
            cuenta = aws("sts", "get-caller-identity")["Account"]
        except RuntimeError:
            rojo("  Credenciales vencidas: Learner Lab -> AWS Details -> AWS CLI: Show")
            rojo("  y pegarlas en ~/.aws/credentials.")
            return 1
        verde(f"  cuenta {cuenta}, region {REGION}")
        rol = f"arn:aws:iam::{cuenta}:role/LabRole"

        titulo("1. ¿LabRole confia en API Gateway?")
        try:
            confianza = aws("iam", "get-role", "--role-name", "LabRole")["Role"]["AssumeRolePolicyDocument"]
            servicios = json.dumps(confianza)
            if "apigateway.amazonaws.com" in servicios:
                verde("  si: apigateway.amazonaws.com aparece en la trust policy")
            else:
                amar("  no aparece apigateway.amazonaws.com; igual se prueba de verdad abajo")
        except RuntimeError as e:
            amar(f"  no se pudo leer el rol (Academy a veces lo bloquea): {e.splitlines()[-1]}")

        titulo("2. Cola de prueba")
        url_cola = aws("sqs", "create-queue", "--queue-name", COLA)["QueueUrl"]
        verde(f"  + {COLA}")

        titulo("3. HTTP API con integracion SQS-SendMessage")
        api = aws("apigatewayv2", "create-api", entrada={"Name": API, "ProtocolType": "HTTP"})
        api_id, endpoint = api["ApiId"], api["ApiEndpoint"]
        integ = aws("apigatewayv2", "create-integration", entrada={
            "ApiId": api_id,
            "IntegrationType": "AWS_PROXY",
            "IntegrationSubtype": "SQS-SendMessage",
            "PayloadFormatVersion": "1.0",
            "CredentialsArn": rol,
            "RequestParameters": {"QueueUrl": url_moderna(url_cola), "MessageBody": "$request.body"},
        })
        aws("apigatewayv2", "create-route", entrada={
            "ApiId": api_id, "RouteKey": "POST /ingesta",
            "Target": "integrations/" + integ["IntegrationId"],
        })
        aws("apigatewayv2", "create-stage", entrada={
            "ApiId": api_id, "StageName": "$default", "AutoDeploy": True,
        })
        verde(f"  + {endpoint}/ingesta (credencial: LabRole)")

        titulo("4. Mandar un mensaje")
        cuerpo = json.dumps({"type": "oferta.capturada", "prueba": SELLO}).encode()
        estado = texto = None
        # La API recien creada puede tardar unos segundos en responder.
        for _ in range(6):
            time.sleep(5)
            req = urllib.request.Request(f"{endpoint}/ingesta", data=cuerpo, method="POST",
                                         headers={"Content-Type": "application/json"})
            try:
                with urllib.request.urlopen(req, timeout=15) as r:
                    estado, texto = r.status, r.read().decode()
            except urllib.error.HTTPError as e:
                estado, texto = e.code, e.read().decode()
            except urllib.error.URLError as e:
                estado, texto = None, str(e)
            if estado not in (None, 404):
                break
        gris(f"  HTTP {estado}: {texto[:200] if texto else ''}")

        titulo("5. ¿Llego a la cola?")
        recibido = aws("sqs", "receive-message", entrada={
            "QueueUrl": url_cola, "WaitTimeSeconds": 10, "MaxNumberOfMessages": 1,
        }).get("Messages", [])
        if recibido and SELLO in recibido[0]["Body"]:
            verde("  ✓ SI. El API Gateway escribe en SQS con LabRole.")
            verde("  → OPCION A: la ruta /ingesta va directo a la cola, sin codigo nuevo.")
            return 0
        rojo("  ✗ NO llego.")
        rojo("  → OPCION B: el gateway (C#) recibe /ingesta y publica en SQS con AWSSDK.SQS.")
        if estado == 500:
            gris("    Un 500 aqui casi siempre es que API Gateway no pudo asumir LabRole.")
        return 3
    except RuntimeError as e:
        rojo(f"  AWS respondio con error:\n  {e}")
        return 1
    finally:
        titulo("6. Limpieza")
        if api_id:
            try:
                aws("apigatewayv2", "delete-api", "--api-id", api_id)
                verde(f"  - API {api_id}")
            except RuntimeError as e:
                rojo(f"  no se pudo borrar la API {api_id}: {e}")
        if url_cola:
            try:
                aws("sqs", "delete-queue", entrada={"QueueUrl": url_cola})
                verde(f"  - {COLA}")
            except RuntimeError as e:
                rojo(f"  no se pudo borrar la cola {COLA}: {e}")


if __name__ == "__main__":
    sys.exit(main())
