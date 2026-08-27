# Scraper de arranque · Sparta

**Esto no es `scraper-service`.** El definitivo es una Lambda de Micronaut y va en su propio
módulo (ver `docs/ARQUITECTURA.md` §2). Esto es un script de una sola pieza que existe por una
razón concreta:

> El historial de precios solo existe si empieza a acumularse. Cada día que no corre es un
> hueco que después **no se recupera**. `TAREAS.md` lo pedía para la semana 0.

Arrancó el **27-08-2026**. Cuando exista el scraper de verdad, estas capturas se re-ingestan
y el historial no se pierde: guardamos el JSON **crudo**, sin normalizar (ADR-011, S3 como
fuente de verdad).

## Cómo se usa

```bash
python3 tools/scraper-rapido/sparta.py     # una captura ahora
```

Sin dependencias: solo la stdlib de Python 3. Un cron que se cae porque se movió un venv no
sirve de nada.

## Programación

3 capturas diarias (08:00 / 15:00 / 22:00), como fija `PLAN.md`. Va por **timer de systemd de
usuario**, no por cron, porque en un portátil cron pierde la corrida si la máquina está apagada
a esa hora; el timer la ejecuta al encender (`Persistent=true`).

```bash
systemctl --user list-timers cachaelprecio-scraper.timer   # ver cuándo corre
systemctl --user start cachaelprecio-scraper.service       # forzar una captura
journalctl --user -u cachaelprecio-scraper -n 50           # ver qué pasó
```

Unidades en `~/.config/systemd/user/`. Copia versionada en `systemd/` de esta carpeta.

> ⚠️ `Linger=no`: el timer corre mientras haya sesión iniciada. Con `Persistent=true` alcanza
> para un equipo de uso diario. Para que corra con la sesión cerrada: `sudo loginctl enable-linger kenny`.

## Qué guarda

```
capturas/sparta/AAAA-MM-DD/sparta_AAAAMMDDTHHMMSSZ.json.gz
capturas/sparta/corridas.log     # una línea por corrida: fecha, estado, productos, duración
```

`capturas/` ya está en `.gitignore` — **el crudo no se commitea**. Cada captura pesa ~142 KB.

Cada archivo trae su propio sobre con metadata (endpoint, hora, duración, marcas, errores,
estado), que es justo lo que después va a la tabla `ejecucion_captura`.

## Lo que se aprendió el 27-08 (importante)

**La búsqueda por texto de Magento no filtra por marca.** Buscar `"nike zapatillas"` devuelve
1126 resultados de los cuales **24 son Nike**; el término "zapatillas" domina y las cuatro
marcas devolvían casi los mismos productos. Hay que usar el atributo `gral_marca`:

```graphql
products(filter: {gral_marca: {eq: "21"}}, pageSize: 50, currentPage: 1)
```

Los ids salen de `aggregations`. Con el filtro puesto, el catálogo real de Sparta es:

| Marca | id | Productos | Zapatillas |
|---|---|---|---|
| New Balance | 21 | 857 | 408 |
| Adidas | 3 | 752 | 239 |
| Joma | 18644 | 267 | 138 |
| Asics | 447 | 96 | 95 |
| Nike | 23 | 89 | 24 |
| Puma | 30 | 27 | 4 |

🔴 **Sparta es una tienda de New Balance y Adidas.** Nike y Puma son marginales — y son
justamente las que `PLAN.md` usa como ejemplo canónico del *style code*. Esto afecta la
elección de las 4 marcas del MVP → ver `docs/BITACORA.md`, semana 1.

## Cortesía con la tienda

- `User-Agent` que nos identifica, con el ramo y el propósito
- 1,5 s entre peticiones y `RandomizedDelaySec=300` en el timer
- 3 reintentos con espera creciente, y tope de 40 páginas por marca
- `robots.txt` de Sparta solo prohíbe `/filtro/` y la navegación facetada (`?amshopby`,
  `?price=`): no tocamos ninguna de esas rutas. `/graphql` no está restringido.
