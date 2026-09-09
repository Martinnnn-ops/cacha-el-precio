#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Respalda y restaura los datos de "Cacha el Precio".
#
# POR QUE ESTE SCRIPT EXISTE
#   La cuenta de AWS es de Academy y se puede agotar. El codigo esta en GitHub
#   y la infraestructura se recrea con scripts, pero LOS DATOS NO: viven en
#   volumenes de Docker dentro de la EC2 y se van con la cuenta.
#
#   Y el historial de precios no se recupera hacia atras. Es una serie en el
#   tiempo, no un calculo: si se pierde el 27-08 al 07-09, esos dias no vuelven
#   por mucho que se vuelva a correr el scraper. Ver docs/MIGRACION.md.
#
# SE CORRE DONDE ESTA EL docker-compose CORRIENDO, o sea en la EC2.
#
# Uso:
#   ./respaldar.sh                     crea un respaldo nuevo
#   ./respaldar.sh --listar            muestra los respaldos que hay
#   ./respaldar.sh --restaurar CARPETA vuelve a cargar un respaldo
#
# Variables opcionales:
#   DESTINO   donde se guardan  (por defecto ./respaldos)
#   CON_S3    =1 para bajar tambien las imagenes del bucket
# ---------------------------------------------------------------------------
set -uo pipefail

RAIZ="$(cd "$(dirname "$0")/.." && pwd)"
cd "$RAIZ" || exit 1
DESTINO="${DESTINO:-$RAIZ/respaldos}"

verde()  { printf '\033[0;32m%s\033[0m\n' "$1"; }
rojo()   { printf '\033[0;31m%s\033[0m\n' "$1"; }
gris()   { printf '\033[0;90m%s\033[0m\n' "$1"; }
titulo() { printf '\n\033[1m== %s ==\033[0m\n' "$1"; }

# El .env trae DB_USER y DB_NAME, que pg_dump necesita.
if [[ -f "$RAIZ/.env" ]]; then set -a; source "$RAIZ/.env"; set +a; fi
DB_USER="${DB_USER:-cachaelprecio}"
DB_NAME="${DB_NAME:-cachaelprecio}"

# --------------------------------------------------------------------------
# --listar
# --------------------------------------------------------------------------
if [[ "${1:-}" == "--listar" ]]; then
  titulo "Respaldos en $DESTINO"
  if [[ ! -d "$DESTINO" ]]; then gris "  todavia no hay ninguno"; exit 0; fi
  for d in "$DESTINO"/*/; do
    [[ -d "$d" ]] || continue
    printf '  %-22s %6s' "$(basename "$d")" "$(du -sh "$d" 2>/dev/null | cut -f1)"
    [[ -f "$d/MANIFIESTO.txt" ]] \
      && printf '  %s\n' "$(grep -m1 'productos:' "$d/MANIFIESTO.txt" 2>/dev/null || echo '')" \
      || printf '  (sin manifiesto)\n'
  done
  exit 0
fi

# --------------------------------------------------------------------------
# 0. Que el stack este arriba
# --------------------------------------------------------------------------
titulo "0. Comprobaciones"
if ! docker compose ps --format '{{.Service}}' >/dev/null 2>&1; then
  rojo "Docker no responde o no estas en la carpeta del proyecto."
  rojo "Si es tu equipo: sudo systemctl start docker   (alias docker-on)"
  exit 1
fi

vivo() { docker compose ps --status running --format '{{.Service}}' 2>/dev/null | grep -qx "$1"; }

vivo postgres && verde "  postgres arriba" || { rojo "  postgres no esta corriendo: no hay que respaldar"; exit 1; }
vivo product-service && verde "  product-service arriba" \
  || gris  "  product-service apagado: su esquema PostgreSQL se respalda igual"

# --------------------------------------------------------------------------
# --restaurar
# --------------------------------------------------------------------------
if [[ "${1:-}" == "--restaurar" ]]; then
  CARPETA="${2:-}"
  [[ -d "$CARPETA" ]] || { rojo "Uso: ./respaldar.sh --restaurar CARPETA"; exit 1; }
  # A ruta absoluta: docker se niega a montar rutas relativas, y el error que da
  # ("invalid mount path") no dice que el problema sea eso.
  CARPETA="$(cd "$CARPETA" && pwd)"

  titulo "Restaurando desde $CARPETA"
  rojo "  Esto SOBRESCRIBE los datos actuales."
  read -r -p "  Escribe RESTAURAR para continuar: " confirmacion
  [[ "$confirmacion" == "RESTAURAR" ]] || { gris "  cancelado"; exit 0; }

  if [[ -f "$CARPETA/database.sql" ]]; then
    docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" \
      -v ON_ERROR_STOP=1 < "$CARPETA/database.sql" >/dev/null \
      && verde "  esquemas scraper y product restaurados" || rojo "  fallo el restore de Postgres"
  else
    rojo "  el respaldo no trae database.sql"
  fi

  verde "Listo. Comprueba con: curl -H 'Version: 1.0' http://localhost:8081/api/products"
  exit 0
fi

# --------------------------------------------------------------------------
# 1. El respaldo
# --------------------------------------------------------------------------
SELLO="$(date +%Y%m%d-%H%M%S)"
CARPETA="$DESTINO/$SELLO"
mkdir -p "$CARPETA" || { rojo "No se pudo crear $CARPETA"; exit 1; }
titulo "1. Respaldando en $CARPETA"

# --- Postgres: esquemas propios del scraper y Product Service --------------
# pg_dump se ejecuta DENTRO del contenedor a proposito: asi la version de la
# herramienta siempre coincide con la del servidor. Con el pg_dump del equipo,
# una diferencia de version mayor aborta el volcado.
# --clean --if-exists no es opcional: sin eso el volcado solo sirve para una
# base vacia, y justo cuando hace falta restaurar es porque la base tiene algo
# que salio mal. Con esto, el restore borra y recrea el esquema.
if docker compose exec -T postgres pg_dump -U "$DB_USER" -d "$DB_NAME" \
     --schema=scraper --schema=product --no-owner --clean --if-exists \
     > "$CARPETA/database.sql" 2>"$CARPETA/.pg.err"; then
  FILAS="$(grep -c '^[0-9]' "$CARPETA/database.sql" 2>/dev/null || echo 0)"
  verde "  database.sql  $(du -h "$CARPETA/database.sql" | cut -f1)"
  rm -f "$CARPETA/.pg.err"
else
  rojo "  fallo el pg_dump: $(head -1 "$CARPETA/.pg.err" 2>/dev/null)"
  FILAS=0
fi

# --- S3: las imagenes, solo si se pide -------------------------------------
if [[ "${CON_S3:-0}" == "1" && -n "${AWS_S3_BUCKET:-}" ]]; then
  if aws s3 sync "s3://$AWS_S3_BUCKET" "$CARPETA/imagenes/" --only-show-errors 2>/dev/null; then
    verde "  imagenes     $(du -sh "$CARPETA/imagenes" 2>/dev/null | cut -f1)"
  else
    rojo "  no se pudieron bajar las imagenes (credenciales o bucket)"
  fi
else
  gris "  imagenes     omitidas (CON_S3=1 para incluirlas)"
fi

# --------------------------------------------------------------------------
# 2. Verificar
#    Un respaldo que nadie miro no es un respaldo. Aca se comprueba que los
#    archivos tengan forma de lo que dicen ser y que no vengan vacios, que es
#    la falla clasica: el script corre, no da error, y guarda 0 bytes.
# --------------------------------------------------------------------------
titulo "2. Verificacion"
PROBLEMAS=0

if [[ -s "$CARPETA/database.sql" ]] && grep -q 'CREATE TABLE' "$CARPETA/database.sql"; then
  verde "  database.sql trae definiciones de tabla"
  for esquema in scraper product; do
    if grep -q "Schema: $esquema" "$CARPETA/database.sql"; then
      verde "  incluye el esquema $esquema"
    else
      rojo "  falta el esquema $esquema"; PROBLEMAS=$((PROBLEMAS + 1))
    fi
  done
else
  rojo "  database.sql falta o esta vacio"; PROBLEMAS=$((PROBLEMAS + 1))
fi

# --------------------------------------------------------------------------
# 3. Manifiesto
# --------------------------------------------------------------------------
{
  echo "Respaldo de Cacha el Precio"
  echo "fecha:      $(date '+%d-%m-%Y %H:%M:%S %Z')"
  echo "equipo:     $(uname -n)"
  echo "cuenta AWS: $(aws sts get-caller-identity --query Account --output text 2>/dev/null || echo 'sin credenciales')"
  echo
  echo "contenido:"
  for f in "$CARPETA"/*; do
    [[ -f "$f" ]] && printf '  %-18s %s\n' "$(basename "$f")" "$(du -h "$f" | cut -f1)"
  done
  echo
  echo "productos:  $FILAS lineas de datos en el volcado de Postgres"
  echo "problemas:  $PROBLEMAS"
  echo
  echo "Restaurar con:"
  echo "  ./tools/respaldar.sh --restaurar $CARPETA"
} > "$CARPETA/MANIFIESTO.txt"

titulo "Listo"
if [[ "$PROBLEMAS" -eq 0 ]]; then
  verde "  respaldo completo en $CARPETA"
else
  rojo  "  respaldo con $PROBLEMAS problema(s): NO lo des por bueno"
fi
gris "  guardalo FUERA de esta maquina y fuera de la cuenta de AWS."
gris "  El repo lo ignora a proposito: son datos, no codigo."
exit "$PROBLEMAS"
