#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Prueba de punta a punta del flujo 1 en la EC2 (T06) y guarda la evidencia.
#
#   normal       un barrido -> cada prenda va por /ingesta a la cola ->
#                product-service la consume -> la cola baja a 0 -> las prendas
#                se ven en GET /productos A TRAVES del API Gateway.
#   resiliencia  igual, pero con product-service DETENIDO: las prendas se
#                acumulan en la cola (visibles); al volver a encenderlo, la
#                cola baja a 0 y no se pierde ninguna.
#
# Uso:
#   ./tools/probar-flujo1.sh normal      [tienda] [limite]   (falabella 20)
#   ./tools/probar-flujo1.sh resiliencia [tienda] [limite]
#
# Deja docs/evidencia/flujo1-<prueba>-<fecha>.log. Requiere infra.env,
# api-gateway.env, mensajeria.env y la llave (LLAVE=~/labsuser.pem).
# ---------------------------------------------------------------------------
set -uo pipefail

PRUEBA="${1:-normal}"
TIENDA="${2:-falabella}"
LIMITE="${3:-20}"
[[ "$PRUEBA" == normal || "$PRUEBA" == resiliencia ]] || { echo "uso: $0 normal|resiliencia [tienda] [limite]"; exit 1; }

RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
set -a; . "$RAIZ/infra.env"; . "$RAIZ/api-gateway.env"; . "$RAIZ/mensajeria.env"; set +a
LLAVE="${LLAVE:-$HOME/labsuser.pem}"
SSH="ssh -i $LLAVE -o ConnectTimeout=15 -o LogLevel=ERROR ec2-user@$INFRA_IP"
REMOTO="cd /opt/cacha-el-precio &&"
EVIDENCIA="$RAIZ/docs/evidencia/flujo1-$PRUEBA-$(date +%Y%m%d-%H%M%S).log"
mkdir -p "$RAIZ/docs/evidencia"

log() { echo "$*" | tee -a "$EVIDENCIA"; }

cola() {  # visibles y en proceso de una cola
  aws sqs get-queue-attributes --region "$INFRA_REGION" --cli-input-json \
    "{\"QueueUrl\":\"$1\",\"AttributeNames\":[\"ApproximateNumberOfMessages\",\"ApproximateNumberOfMessagesNotVisible\"]}" \
    --query 'Attributes.[ApproximateNumberOfMessages,ApproximateNumberOfMessagesNotVisible]' --output text
}
estado_colas() {
  local c d; c="$(cola "$SQS_OFERTAS_CRUDAS_URL")"; d="$(cola "$SQS_OFERTAS_CRUDAS_DLQ_URL")"
  log "   $(date +%T)  cep-ofertas-crudas  visibles/en-proceso: ${c//$'\t'/ / }   ·   DLQ visibles: ${d%%$'\t'*}"
}
productos() {  # cuantos productos y ofertas de la tienda ve el sitio (por el API Gateway)
  curl -s -H 'Version: 1.0' "$API_GATEWAY_URL_PROD/productos" | python3 -c "
import json,sys
p=json.load(sys.stdin); t=sys.argv[1]
o=[x for q in p for x in q.get('offers',[]) if x.get('store','').lower()==t]
print(len(p), len(o))" "$TIENDA"
}
procesados() {  # cuantas ofertas guardo el consumidor desde que arranco la prueba
  $SSH "$REMOTO docker compose logs --no-color --since '$INICIO' product-service 2>&1 | grep -c 'Resultado=ok'" || echo 0
}

INICIO="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
log "EVIDENCIA DEL FLUJO 1 ($PRUEBA) — Cacha el Precio · T06"
log "Fecha:    $(date -Is)"
# En la EC2 la copia local siempre se llama como la primera rama clonada: lo
# que cuenta es a que rama de origin apunta el commit desplegado.
log "Cuenta:   $INFRA_CUENTA      EC2: $INFRA_IP"
log "Codigo:   $($SSH "$REMOTO git log --oneline -1; git branch -r --contains HEAD | grep -v HEAD | head -1" | tr '\n' ' ')"
log "Camino:   scraper-api (MODO_PUBLICACION=$($SSH "$REMOTO grep -oP '^MODO_PUBLICACION=\K.*' .env")) -> POST /ingesta -> SQS cep-ofertas-crudas -> product-service -> PostgreSQL"
log "Medido:   el catalogo A TRAVES del API Gateway ($API_GATEWAY_URL_PROD/productos)"
log "Barrido:  $TIENDA, limite $LIMITE"
log "======================================================================="
log ""
log "1. ANTES"
read -r P0 O0 <<<"$(productos)"
log "   productos en el sitio: $P0   ·   ofertas de $TIENDA: $O0"
estado_colas

if [[ "$PRUEBA" == resiliencia ]]; then
  log ""
  log "2. SE DETIENE product-service (el consumidor de la cola)"
  $SSH "$REMOTO docker compose stop product-service" 2>&1 | sed 's/^/   /' | tee -a "$EVIDENCIA"
  log "   $($SSH "$REMOTO docker compose ps --all --format '{{.Service}}: {{.State}}' product-service")"
fi

log ""
ANTERIOR="$($SSH "curl -s localhost:8000/scrape/$TIENDA/status")"
log "$([[ $PRUEBA == normal ]] && echo 2 || echo 3). SE LANZA EL BARRIDO (POST /scrape/$TIENDA?limite=$LIMITE en scraper-api)"
log "   $($SSH "curl -s -X POST 'localhost:8000/scrape/$TIENDA?limite=$LIMITE'")"

# El barrido corre en segundo plano en scraper-api y su resultado aparece en
# /status recien al terminar. Se espera uno DISTINTO al que habia antes.
for _ in $(seq 1 90); do
  sleep 10
  ESTADO="$($SSH "curl -s localhost:8000/scrape/$TIENDA/status")"
  estado_colas
  [[ "$ESTADO" != "$ANTERIOR" ]] && echo "$ESTADO" | grep -q '"sincronizados"\|"error"' && break
done
log "   barrido terminado: $ESTADO"

if [[ "$PRUEBA" == resiliencia ]]; then
  log ""
  log "4. CON product-service DETENIDO, las prendas esperan en la cola (no se pierden)"
  sleep 5; estado_colas
  read -r P1 O1 <<<"$(productos 2>/dev/null || echo '- -')"
  log "   GET /productos con el servicio caido: ${P1:-sin respuesta} (se espera error: el catalogo esta apagado)"
  log ""
  log "5. SE VUELVE A ENCENDER product-service"
  $SSH "$REMOTO docker compose start product-service" 2>&1 | sed 's/^/   /' | tee -a "$EVIDENCIA"
fi

log ""
log "$([[ $PRUEBA == normal ]] && echo 3 || echo 6). LA COLA SE VACIA"
# El contador de SQS es aproximado: se exige ver la cola en 0 dos veces seguidas.
CEROS=0
for _ in $(seq 1 60); do
  sleep 5
  C="$(cola "$SQS_OFERTAS_CRUDAS_URL")"
  log "   $(date +%T)  cep-ofertas-crudas  visibles/en-proceso: ${C//$'\t'/ / }"
  if [[ "$C" == $'0\t0' ]]; then CEROS=$((CEROS + 1)); else CEROS=0; fi
  [[ $CEROS -ge 2 ]] && break
done
estado_colas

log ""
log "$([[ $PRUEBA == normal ]] && echo 4 || echo 7). DESPUES"
sleep 3
read -r P2 O2 <<<"$(productos)"
N="$(procesados)"
log "   productos en el sitio: $P0 -> $P2   ·   ofertas de $TIENDA: $O0 -> $O2"
BD="$($SSH "$REMOTO set -a; . ./.env; set +a; docker compose exec -T postgres psql -U \$DB_USER -d \$DB_NAME -tAc \"select count(*) from product.\\\"ProductOffers\\\" where lower(\\\"Store\\\")='$TIENDA'\"")"
log "   prendas publicadas por el scraper (sincronizados): $(echo "$ESTADO" | grep -oP '"sincronizados":\K[0-9]+')"
log "   ofertas guardadas por el consumidor (Resultado=ok en product-service): $N"
log "   ofertas distintas de $TIENDA en la base (tienda + SKU): ${BD//[[:space:]]/}"
log "   (publicadas = guardadas: no se perdio ninguna. Las distintas son menos porque un"
log "    mismo SKU aparece en varias paginas del listado y la segunda vez se actualiza;"
log "    el sitio muestra menos productos porque agrupa las ofertas de un mismo modelo.)"
log "   ultimas lineas del consumidor:"
$SSH "$REMOTO docker compose logs --no-color --since '$INICIO' product-service 2>&1 | grep -E 'Resultado=' | tail -3" \
  | sed -E 's/^cep-product-service *\| */     /' | tee -a "$EVIDENCIA"
log ""
log "Evidencia guardada en ${EVIDENCIA#$RAIZ/}"
