# Imagen del microservicio de scraping.
#
# Estaba roto de tres formas: "FROM python3.12-slim" no es una imagen
# valida (falta el ":"), PYTHONUNBUGGERED estaba mal escrito, y el
# archivo terminaba en un "COPY" suelto. Ademas pedia 3.12 cuando el
# pyproject exige >=3.14.

FROM python:3.14-slim

# No generar .pyc y no bufferizar la salida: sin lo segundo, los logs
# del contenedor aparecen a trompicones o se pierden si el proceso muere.
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app

# Se copian primero los metadatos para que la capa de dependencias se
# reutilice mientras no cambien: sin esto, cada cambio en el codigo
# reinstalaria todo.
COPY pyproject.toml README.md ./
COPY src/ ./src/

# La misma imagen sirve para API, job, PostgreSQL y procesamiento/subida de imagenes.
RUN pip install --no-cache-dir -e ".[api,db,s3,imagen]"

# Usuario sin privilegios: un scraper procesa HTML de terceros y no
# tiene ningun motivo para correr como root.
RUN useradd --create-home --uid 10001 scraper && chown -R scraper:scraper /app
USER scraper

EXPOSE 8000

# Por defecto levanta la API. Para el job por lotes, se sobrescribe:
#   docker run --rm imagen scrapper barrer falabella --sitemap --limite 200
CMD ["uvicorn", "scraper.api.app:app", "--host", "0.0.0.0", "--port", "8000"]
