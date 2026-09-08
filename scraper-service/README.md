# Cacha el Precio — Scraper

Microservicio de scraping en Python. Recoge productos y precios de
tiendas chilenas, los guarda en PostgreSQL con historial de precios, y
sincroniza el catálogo vigente con Product Service por HTTP y expone una API mínima.

El catalogo acepta unicamente productos identificables como ropa o
calzado. Los productos ambiguos y categorias como electronica, hogar,
toallas o billeteras se descartan antes de persistirlos.

Cuando `AWS_S3_BUCKET` esta configurado, cada imagen se valida y se convierte
a dos WebP: 400 px para tarjetas y 800 px para detalle. Se suben a S3 con una
clave versionada por hash y cache inmutable; PostgreSQL guarda las URLs y las
claves, nunca el binario. Un fallo de imagen no impide guardar el producto.

Los diagramas de arquitectura estan en [README.arquitectura.md](README.arquitectura.md).

## Como funciona

Dos formas de ejecutarlo, por diseno:

- **Job programado** (el grueso): un timer de systemd o cron lanza el
  CLI, que barre las tiendas y escribe en la base.
- **API mínima**: para consultar el estado y forzar
  un barrido puntual sin esperar al cron.

El scrapeo no ocurre dentro de la peticion HTTP: tarda demasiado. La
API responde `202 Accepted` y trabaja en segundo plano.

## Puesta en marcha

```bash
python -m venv .venv && source .venv/bin/activate
pip install -e ".[api,db,s3,imagen,dev]"
cp .env.example .env          # y rellena DATABASE_URL

docker compose up -d postgres # PostgreSQL local, desde la raíz
scrapper esquema              # crea las tablas
```

## Uso

```bash
scrapper tiendas                                   # que se puede scrapear
scrapper descubrir falabella --limite 20           # URLs desde el sitemap
scrapper barrer falabella --sitemap --limite 200   # barrido completo
scrapper barrer converse --url https://...         # una URL suelta

uvicorn scraper.api.app:app --reload               # API en :8000
```

| Ruta | Para que |
|---|---|
| `GET /health` | sonda de vida |
| `GET /tiendas` | tiendas disponibles y si tienen sitemap |
| `POST /scrape/{tienda}?limite=N` | lanza un barrido (202, en segundo plano) |
| `GET /scrape/{tienda}/status` | resultado del ultimo barrido |
| `GET /productos/{tienda}/{id}` | un producto |
| `GET /productos/{tienda}/{id}/historial` | historial de precios |

## Modelo de datos

Un **producto por tienda**, mas un **historial de precios**.

`Product` es el producto tal como lo publica UNA tienda; la identidad
es el par `(store, external_id)`. La misma zapatilla en converse.cl y
en Falabella son dos filas distintas: emparejarlas entre tiendas es un
problema aparte, porque cada una usa su propio SKU y su propio nombre.

`Offer` es un precio observado en un instante. **Solo se anade fila
cuando el precio o el stock cambian.** El job corre varias veces al
dia sobre cientos de productos; guardar una fila por barrido llenaria
la tabla de duplicados y los graficos tendrian miles de puntos
identicos. Guardando solo los cambios, el historial *es* la lista de
cambios de precio, que es justo lo que la web quiere mostrar.

## Como se extraen los datos

Por **JSON-LD (schema.org/Product) y Open Graph**, no por selectores
CSS. Las tiendas publican esos datos para Google, asi que cambian
mucho menos que las clases del maquetado.

Y evita una trampa concreta: las fichas llevan carruseles de productos
recomendados con SUS propios precios en el HTML. Un selector CSS de
precio puede coger el del producto vecino. Hay una prueba que lo
verifica (`test_el_precio_es_el_del_producto_y_no_el_de_un_recomendado`).

## Tiendas: que permite cada una

Comprobado en su `robots.txt` el 03/09/2026.

| Tienda | robots.txt | Como extrae | Estado |
|---|---|---|---|
| **falabella.com** | permite todo | JSON-LD + availability | funcionando |
| **paris.cl** | permite todo | JSON-LD | funcionando |
| **simple.ripley.cl** | permite, excluye reviews y APIs | JSON-LD dentro de `@graph` | funcionando |
| **hites.com** | permite (35 disallow) | JSON-LD + availability | funcionando |
| **sparta.cl** | permite (6 disallow) | Open Graph + bloque de Analytics | funcionando |
| converse.cl | **bloquea todos los bots** salvo Google, Bing, WhatsApp y Facebook | JSON-LD | parser hecho, no se puede barrer |
| nike.cl | devuelve 403 hasta en robots.txt | — | inviable |

Barrido real del 03/09/2026, 8 URLs por tienda:

```
[falabella] 8/8 URLs, 4 guardados, 4 sin producto, 0 fallos
[paris]     8/8 URLs, 8 guardados, 0 sin producto, 0 fallos
[ripley]    8/8 URLs, 8 guardados, 0 sin producto, 0 fallos
[hites]     8/8 URLs, 8 guardados, 0 sin producto, 0 fallos
[sparta]    8/8 URLs, 8 guardados, 0 sin producto, 0 fallos
```

Los "sin producto" de Falabella son articulos descatalogados: publican
`"offers": []`, sin precio. No es un fallo del scraper, y por eso se
cuentan aparte.

### Particularidades que costaron encontrar

- **Ripley** anida el `Product` dentro de `@graph` y ademas publica un
  segundo bloque `Product` que solo lleva valoraciones. Quedarse con el
  primero daba un producto vacio; la base elige el mas completo.
- **Sparta** es la unica que no publica `schema.org/Product`. El sku y
  la marca salen de su bloque de Analytics; el precio, de Open Graph.
  Su bloque de Analytics trae el precio de LISTA (6990) y el descuento
  aparte, mientras Open Graph trae el final (2990), que es el que
  interesa para comparar.
- **Sparta** mezcla fichas, categorias y paginas estaticas en el mismo
  sitemap. Se filtran por patron de URL (el SKU final lleva digitos;
  los slugs de categoria son solo letras) antes de gastar peticiones.
- **Paris** publica su indice de sitemaps como `<urlset>` en vez de
  `<sitemapindex>`, asi que habia que detectarlo o se devolvian rutas
  de sitemaps creyendo que eran fichas.

**Nota sobre Converse.** Su `robots.txt` dice `User-agent: * / Disallow: /`
y el sitio devuelve **403** a cualquier User-Agent que no sea de
navegador. El parser funciona (probado contra HTML guardado), pero
barrer la tienda en vivo significa saltarse una restriccion explicita.
Falabella, Paris y Ripley lo permiten y ademas publican sus URLs en
sitemaps, asi que son el camino recomendado.

El User-Agent por defecto se identifica (`CachaElPrecioBot/1.0`) y las
tres tiendas permitidas lo aceptan sin problema.

## Desarrollo

```bash
pytest              # 66 pruebas (61 sin red + 5 contra PostgreSQL)
ruff check .
mypy
```

Las pruebas de integracion (`tests/integration/`) se saltan solas si no
hay `DATABASE_URL`. Con la base levantada corren de verdad:

```bash
docker compose up -d db
DATABASE_URL=postgresql://cacha:cacha@localhost:5432/cacha_el_precio pytest
```

Las unitarias estan aisladas de la base: `tests/unit/conftest.py` vacia
`DATABASE_URL` para que la API caiga al repositorio en memoria. Sin eso,
importar el modulo de pruebas de la API abria una conexion real y las
pruebas escribian sus datos de mentira en la base de verdad.

## Que falta

- Descarga, conversion a WebP y subida a S3 de las imagenes
- Empaquetar el job en un timer de systemd
