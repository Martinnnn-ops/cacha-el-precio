#!/usr/bin/env python3
"""
Pruebas de punta a punta de la mensajeria en la EC2 (T13), A TRAVES del API
Gateway, como lo usaria una persona. Deja la evidencia en docs/evidencia/.

  A. Administracion (T11): 401 sin token, 403 sin el grupo admin, 200 con el
     admin; consultar colas, topicos con sus filtros, la topologia y una DLQ
     con su diagnostico; casos invalidos (400 { campo, error }) y conflictos
     (409); crear y borrar una cola con su DLQ y una regla con filtro.
  B. Flujo 1 con error: una oferta con price -1 por POST /ingesta llega a la
     DLQ en el primer intento, con error-type=definitivo, vista desde el panel.
  C. Flujo 2 en la EC2: el admin sigue un producto real, su oferta baja 30%
     por la cola -> SNS -> regla de distribucion -> notification-service en la
     EC2 genera el correo. Una baja de 5% solo llega a la auditoria. Al final se
     restaura el precio y se deja de seguir el producto.
  D. Redrive desde el panel: la DLQ vuelve a su cola y lo malo vuelve a la DLQ.

Uso:  python3 tools/probar-mensajeria.py      (LLAVE=~/labsuser.pem por defecto)
Requiere cognito.env, api-gateway.env, infra.env y la aws cli con credenciales.
Usa los usuarios de prueba de crear-cognito.sh (admin@ y usuario@).
"""

from __future__ import annotations

import base64
import datetime as dt
import json
import os
import pathlib
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

RAIZ = pathlib.Path(__file__).resolve().parent.parent
EVIDENCIA = RAIZ / "docs" / "evidencia" / f"mensajeria-ec2-{dt.datetime.now():%Y%m%d-%H%M%S}.log"
LLAVE = os.environ.get("LLAVE", str(pathlib.Path.home() / "labsuser.pem"))


def leer_env(nombre: str) -> dict[str, str]:
    d = {}
    for linea in (RAIZ / nombre).read_text().splitlines():
        if "=" in linea and not linea.lstrip().startswith("#"):
            k, v = linea.split("=", 1)
            d[k.strip()] = v.strip().strip('"')
    return d


COG, AG, INFRA = leer_env("cognito.env"), leer_env("api-gateway.env"), leer_env("infra.env")
BASE = AG["API_GATEWAY_URL_PROD"].rstrip("/")
ADMIN = f"{BASE}/api/admin/mensajeria"
REGION = COG.get("COGNITO_REGION") or "us-east-1"
fallas = 0


def log(texto: str = "") -> None:
    print(texto)
    with EVIDENCIA.open("a") as f:
        f.write(texto + "\n")


def check(descripcion: str, ok: bool, detalle: str = "") -> None:
    global fallas
    if not ok:
        fallas += 1
    log(f"   {'OK   ' if ok else 'FALLA'} {descripcion}" + (f" — {detalle}" if detalle else ""))


def http(metodo: str, url: str, token: str | None = None, cuerpo=None, cabeceras=None):
    datos = None if cuerpo is None else json.dumps(cuerpo).encode()
    cab = {"Content-Type": "application/json", "Version": "1.0", **(cabeceras or {})}
    if token:
        cab["Authorization"] = f"Bearer {token}"
    req = urllib.request.Request(url, datos, cab, method=metodo)
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            texto = r.read().decode()
            return r.status, (json.loads(texto) if texto.strip().startswith(("{", "[")) else texto)
    except urllib.error.HTTPError as e:
        texto = e.read().decode()
        try:
            return e.code, json.loads(texto)
        except ValueError:
            return e.code, texto


def token_usuario(correo: str) -> str:
    r = subprocess.run(
        ["aws", "cognito-idp", "admin-initiate-auth", "--region", REGION,
         "--user-pool-id", COG["COGNITO_USER_POOL_ID"], "--client-id", COG["COGNITO_PRUEBAS_CLIENT_ID"],
         "--auth-flow", "ADMIN_USER_PASSWORD_AUTH",
         "--auth-parameters", f"USERNAME={correo},PASSWORD={COG['COGNITO_CLAVE_PRUEBA']}",
         "--query", "AuthenticationResult.AccessToken", "--output", "text"],
        capture_output=True, text=True)
    if r.returncode != 0:
        sys.exit(f"no se pudo obtener el token de {correo}: {r.stderr.strip()}")
    return r.stdout.strip()


def token_scraper() -> str:
    url = f"https://{COG['COGNITO_DOMINIO']}.auth.{REGION}.amazoncognito.com/oauth2/token"
    basic = base64.b64encode(
        f"{COG['COGNITO_SCRAPER_CLIENT_ID']}:{COG['COGNITO_SCRAPER_CLIENT_SECRET']}".encode()).decode()
    cuerpo = urllib.parse.urlencode({"grant_type": "client_credentials", "scope": COG["COGNITO_SCRAPER_SCOPE"]})
    req = urllib.request.Request(url, cuerpo.encode(), {
        "Authorization": f"Basic {basic}", "Content-Type": "application/x-www-form-urlencoded"})
    return json.load(urllib.request.urlopen(req))["access_token"]


def ingesta(token: str, oferta: dict) -> tuple[int, str]:
    sobre = {"eventId": str(uuid.uuid4()), "type": "oferta.capturada", "version": 1,
             "source": "prueba-t13", "occurredAt": dt.datetime.now(dt.UTC).isoformat(), "data": oferta}
    estado, _ = http("POST", AG["API_GATEWAY_URL_INGESTA"], token, sobre)
    return estado, sobre["eventId"]


def ssh(comando: str) -> str:
    r = subprocess.run(["ssh", "-i", LLAVE, "-o", "LogLevel=ERROR", "-o", "ConnectTimeout=15",
                        f"ec2-user@{INFRA['INFRA_IP']}", f"cd /opt/cacha-el-precio && {comando}"],
                       capture_output=True, text=True)
    return r.stdout


def esperar(descripcion: str, condicion, segundos: int = 90):
    fin = time.time() + segundos
    while time.time() < fin:
        valor = condicion()
        if valor:
            return valor
        time.sleep(4)
    return None


def errores_campos(cuerpo) -> list[str]:
    return [e.get("campo") for e in (cuerpo.get("errores") or [])] if isinstance(cuerpo, dict) else []


# =============================================================================
EVIDENCIA.parent.mkdir(parents=True, exist_ok=True)
log("EVIDENCIA DE LA MENSAJERIA EN LA EC2 — Cacha el Precio · T13")
log(f"Fecha:    {dt.datetime.now().astimezone().isoformat(timespec='seconds')}")
log(f"Cuenta:   {INFRA.get('INFRA_CUENTA')}      EC2: {INFRA['INFRA_IP']}")
log(f"Codigo:   {' '.join(ssh('git log --oneline -1; git branch -r --contains HEAD | grep -v HEAD | head -1').split())}")
log(f"Medido:   A TRAVES del API Gateway ({BASE})")
log("=" * 72)

t_admin, t_usuario, t_scraper = token_usuario(COG["COGNITO_USUARIO_ADMIN"]), token_usuario(
    COG["COGNITO_USUARIO_NORMAL"]), token_scraper()

# ---------------------------------------------------------------------------
log("\nA. ADMINISTRACION DE MENSAJERIA (T11)")
log("   — quien puede entrar —")
check("sin token -> 401", http("GET", f"{ADMIN}/colas")[0] == 401)
check("usuario sin el grupo admin -> 403", http("GET", f"{ADMIN}/colas", t_usuario)[0] == 403)
estado, colas = http("GET", f"{ADMIN}/colas", t_admin)
check("admin -> 200", estado == 200, f"{len(colas)} colas" if estado == 200 else str(colas)[:120])

log("   — consultar —")
if estado == 200:
    for c in colas:
        dlq = f"DLQ {c['dlq']['cola']} tras {c['dlq']['maxIntentos']}" if c.get("dlq") else (
            f"es DLQ de {', '.join(c['esDlqDe'])}" if c.get("esDlqDe") else "sin DLQ")
        log(f"     {c['nombre']:24} visibles {c['mensajesVisibles']:>4} · en proceso {c['mensajesEnVuelo']:>3} · {dlq}")
estado, topicos = http("GET", f"{ADMIN}/topicos", t_admin)
check("GET /topicos con sus reglas", estado == 200 and bool(topicos))
for t in topicos if estado == 200 else []:
    for s in t["suscripciones"]:
        log(f"     {t['nombre']} -> {s['cola']:22} filtro: {json.dumps(s['filtro']) if s['filtro'] else 'recibe todo'}")
estado, topo = http("GET", f"{ADMIN}/topologia", t_admin)
check("GET /topologia: AWS igual a topologia.json", estado == 200 and topo.get("sincronizada") is True,
      "; ".join(f"{d['estado']} {d['nombre']}" for d in topo.get("diferencias", []) if d["estado"] != "extra")
      if isinstance(topo, dict) else str(topo)[:100])

log("   — validacion: 400 con { campo, error } —")
invalidos = [
    ("nombre sin el prefijo cep-", "colas", {"nombre": "ofertas"}, "nombre"),
    ("nombre con mayusculas y guion bajo", "colas", {"nombre": "cep-Ofertas_Crudas"}, "nombre"),
    ("la DLQ indicada no existe", "colas", {"nombre": "cep-x", "dlq": "cep-no-existe", "maxIntentos": 3}, "dlq"),
    ("maxIntentos fuera de 1..10", "colas", {"nombre": "cep-x", "dlq": "cep-ofertas-crudas-dlq", "maxIntentos": 11}, "maxIntentos"),
    ("visibilidad sobre 12 h", "colas", {"nombre": "cep-x", "visibilidadSegundos": 50000}, "visibilidadSegundos"),
    ("filtro sobre un atributo que nadie publica", "suscripciones",
     {"topico": "cep-cambios-oferta", "cola": "cep-auditoria-precios", "filtro": {"tienda": ["paris"]}}, "filtro"),
    ("filtro con un type que no existe", "suscripciones",
     {"topico": "cep-cambios-oferta", "cola": "cep-auditoria-precios", "filtro": {"type": ["precio.subio"]}}, "filtro"),
]
for descripcion, ruta, cuerpo, campo in invalidos:
    estado, r = http("POST", f"{ADMIN}/{ruta}", t_admin, cuerpo)
    error = next((e["error"] for e in (r.get("errores") or []) if e.get("campo") == campo), "") if isinstance(r, dict) else ""
    check(f"400 · {descripcion}", estado == 400 and campo in errores_campos(r), f"{campo}: {error}"[:110])

log("   — conflictos: 409 —")
conflictos = [
    ("la cola ya existe", "POST", "colas", {"nombre": "cep-ofertas-crudas"}),
    ("es la DLQ de otra cola (ni forzando)", "DELETE", "colas/cep-ofertas-crudas-dlq?forzar=true", None),
    ("el topico tiene suscripciones", "DELETE", "topicos/cep-cambios-oferta?forzar=true", None),
    ("mirar una cola con DLQ sumaria intentos", "GET", "colas/cep-ofertas-crudas/mensajes", None),
    ("lo declarado en topologia.json pide ?forzar=true", "DELETE", "suscripciones/cep-cambios-oferta/cep-auditoria-precios", None),
]
for descripcion, metodo, ruta, cuerpo in conflictos:
    estado, r = http(metodo, f"{ADMIN}/{ruta}", t_admin, cuerpo)
    check(f"409 · {descripcion}", estado == 409, (r.get("mensaje", "") if isinstance(r, dict) else "")[:100])

log("   — gestionar: crear y borrar destinos y reglas —")
sufijo = uuid.uuid4().hex[:5]
dlq_demo, cola_demo = f"cep-demo-{sufijo}-dlq", f"cep-demo-{sufijo}"
pasos = [
    ("crear la DLQ", "POST", "colas", {"nombre": dlq_demo, "retencionSegundos": 1209600}, 201),
    ("crear la cola con su DLQ tras 3 intentos", "POST", "colas",
     {"nombre": cola_demo, "visibilidadSegundos": 45, "dlq": dlq_demo, "maxIntentos": 3}, 201),
    ("regla: solo los agotados llegan a la cola", "POST", "suscripciones",
     {"topico": "cep-cambios-oferta", "cola": cola_demo, "filtro": {"type": ["oferta.agotada"]}}, 201),
    ("cambiar el filtro: bajas de 30% o mas", "PUT", f"suscripciones/cep-cambios-oferta/{cola_demo}",
     {"filtro": {"type": ["precio.bajo"], "discountPct": [{"numeric": [">=", 30]}]}}, 200),
    ("borrar la regla", "DELETE", f"suscripciones/cep-cambios-oferta/{cola_demo}", None, 204),
    ("borrar la cola", "DELETE", f"colas/{cola_demo}", None, 204),
]
for descripcion, metodo, ruta, cuerpo, esperado in pasos:
    estado, r = http(metodo, f"{ADMIN}/{ruta}", t_admin, cuerpo)
    check(f"{esperado} · {descripcion}", estado == esperado, "" if estado == esperado else str(r)[:120])
# La DLQ solo se puede borrar cuando el listado de SQS ya no muestra la cola que la usaba.
estado = esperar("borrar la DLQ", lambda: (s := http("DELETE", f"{ADMIN}/colas/{dlq_demo}", t_admin)[0]) in (204, 404) and s, 60)
check("204 · borrar la DLQ (cuando SQS ya no lista la cola que la usaba)", estado == 204)

# ---------------------------------------------------------------------------
log("\nB. FLUJO 1 CON ERROR: una oferta invalida por POST /ingesta")
estado, prods = http("GET", f"{BASE}/productos")
producto = next(p for p in prods if p.get("offers"))
oferta = producto["offers"][0]
base_oferta = {"externalId": oferta["externalId"], "store": oferta["store"], "name": producto["name"],
               "brand": producto["brand"], "category": producto["category"], "gender": producto["gender"],
               "sizes": oferta.get("sizes", []), "url": oferta["url"], "image": producto.get("image"), "active": True}
estado, evento_malo = ingesta(t_scraper, {**base_oferta, "externalId": f"T13-{sufijo}", "price": -1})
check("POST /ingesta con price -1 -> 200 (encolado)", estado == 200, f"eventId {evento_malo}")


def en_dlq(evento: str):
    estado, msgs = http("GET", f"{ADMIN}/colas/cep-ofertas-crudas-dlq/mensajes", t_admin)
    # Si el mismo evento fallo mas de una vez (antes y despues de un redrive), el mas reciente.
    propios = [m for m in msgs if evento in m["cuerpo"]] if estado == 200 else []
    return max(propios, key=lambda m: m["diagnostico"].get("failed-at", ""), default=None)


m = esperar("en la DLQ", lambda: en_dlq(evento_malo))
check("llega a cep-ofertas-crudas-dlq y se ve en el panel", m is not None)
if m:
    d = m["diagnostico"]
    check("con error-type=definitivo al primer intento", d.get("error-type") == "definitivo", d.get("error-reason", ""))
    log(f"     diagnostico: {json.dumps(d, ensure_ascii=False)}")

# ---------------------------------------------------------------------------
log("\nC. FLUJO 2 EN LA EC2: cambio de precio -> SNS -> regla -> correo")
pid, precio = producto["id"], oferta["price"]
log(f"   producto {pid} «{producto['name'][:50]}» · {oferta['store']}/{oferta['externalId']} a ${precio}")
check("el admin sigue el producto (POST /seguimiento/{id})", http("POST", f"{BASE}/seguimiento/{pid}", t_admin)[0] in (200, 201, 204))
desde = dt.datetime.now(dt.UTC).strftime("%Y-%m-%dT%H:%M:%SZ")


def auditoria(tipo: str, nuevo: int):
    estado, msgs = http("GET", f"{ADMIN}/colas/cep-auditoria-precios/mensajes", t_admin)
    for msg in msgs if estado == 200 else []:
        try:
            b = json.loads(msg["cuerpo"])
        except ValueError:
            continue
        if b.get("type") == tipo and b["data"].get("productId") == pid and b["data"].get("newPrice") == nuevo:
            return b
    return None


baja5 = round(precio * 0.95)
estado, _ = ingesta(t_scraper, {**base_oferta, "price": baja5})
check(f"baja de 5% (${precio} -> ${baja5}) por la cola", estado == 200)
b = esperar("auditoria 5%", lambda: auditoria("precio.bajo", baja5), 60)
check("la auditoria (sin filtro) recibe precio.bajo con discountPct 5", b is not None and b["data"].get("discountPct") == 5,
      json.dumps(b["data"], ensure_ascii=False)[:120] if b else "")
time.sleep(10)
correos = ssh(f"docker compose logs --no-color --since {desde} notification-service 2>&1 | grep -c 'CORREO (log)'").strip()
check("notification-service NO avisa la baja de 5% (no pasa el filtro >= 10%)", correos == "0", f"correos: {correos}")

baja30 = round(baja5 * 0.70)
estado, _ = ingesta(t_scraper, {**base_oferta, "price": baja30})
check(f"baja de 30% (${baja5} -> ${baja30}) por la cola", estado == 200)
b = esperar("auditoria 30%", lambda: auditoria("precio.bajo", baja30), 60)
check("la auditoria recibe precio.bajo con discountPct 30", b is not None and b["data"].get("discountPct") == 30)
linea = esperar("correo", lambda: ssh(
    f"docker compose logs --no-color --since {desde} notification-service 2>&1 | grep 'CORREO (log)' | tail -1").strip(), 90)
check("notification-service en la EC2 genera el correo para quien sigue el producto", bool(linea),
      linea.split("->", 1)[-1].split("|", 1)[-1].strip()[:90] if linea else "")

estado, _ = ingesta(t_scraper, {**base_oferta, "price": precio})
check(f"se restaura el precio original (${precio}): una subida no publica nada", estado == 200)
http("DELETE", f"{BASE}/seguimiento/{pid}", t_admin)

# ---------------------------------------------------------------------------
log("\nD. REDRIVE DESDE EL PANEL")
antes_redrive = dt.datetime.now(dt.UTC)
reloj = time.time()
estado, r = http("POST", f"{ADMIN}/colas/cep-ofertas-crudas-dlq/redrive", t_admin)
check(f"POST /colas/cep-ofertas-crudas-dlq/redrive -> 200 en {time.time() - reloj:.1f} s (el borde corta a los 30)",
      estado == 200,
      f"metodo {r.get('metodo')}, {r.get('mensajesMovidos')} movidos a {r.get('destino')}" if isinstance(r, dict) else f"HTTP {estado} {str(r)[:80]}")
check("mueve solo lo que habia (sin ping-pong con el consumidor)",
      isinstance(r, dict) and (r.get("mensajesMovidos") is None or r["mensajesMovidos"] <= 10))


def fallo_de_nuevo():
    m = en_dlq(evento_malo)
    if not m:
        return None
    cuando = dt.datetime.fromisoformat(m["diagnostico"].get("failed-at", "1970-01-01T00:00:00+00:00"))
    return m if cuando > antes_redrive else None


m = esperar("vuelve a la DLQ", fallo_de_nuevo, 90)
check("lo invalido vuelve a la DLQ con una falla NUEVA (reintentar no lo arregla)", m is not None,
      m["diagnostico"].get("failed-at", "") if m else "")

# ---------------------------------------------------------------------------
if "--agotado" in sys.argv:
    log("\nE. ULTIMO INTENTO RECUPERABLE: la base cae y el mensaje se aparta CON diagnostico")
    log(f"   {ssh('docker compose stop postgres 2>&1 | tail -1').strip()}")
    estado, evento = ingesta(t_scraper, {**base_oferta, "externalId": f"T13-BD-{sufijo}", "price": 9990})
    check("con postgres detenido, una oferta valida entra a la cola", estado == 200, f"eventId {evento}")
    m = esperar("recuperable-agotado", lambda: en_dlq(evento), 420)
    log(f"   {ssh('docker compose start postgres 2>&1 | tail -1').strip()}")
    check("tras los reintentos llega a la DLQ (~5 min)", m is not None)
    if m:
        d = m["diagnostico"]
        check("con error-type=recuperable-agotado y la razon del ultimo error",
              d.get("error-type") == "recuperable-agotado", d.get("error-reason", "")[:110])
        log(f"     diagnostico: {json.dumps(d, ensure_ascii=False)[:300]}")
    reintentos = ssh(f"docker compose logs --no-color --since {desde} product-service 2>&1 "
                     f"| grep -E 'Resultado=(reintento|dlq-agotado)' | grep -c .").strip()
    log(f"   intentos registrados por el consumidor (reintento + dlq-agotado): {reintentos}")

log("\n" + "=" * 72)
log(f"RESULTADO: {'TODO OK' if fallas == 0 else f'{fallas} comprobacion(es) fallida(s)'}")
log(f"Evidencia guardada en {EVIDENCIA.relative_to(RAIZ)}")
sys.exit(1 if fallas else 0)
