# ADR-018 · El scraper sale de Java y pasa a Python

**Fecha:** 07-09-2026

**Estado:** aceptada

## Contexto

`scraper-service` nació como un módulo Micronaut más, por coherencia con el resto del backend
([ADR-013](013-java-25-maven.md)). Nunca pasó de esqueleto: arrancaba y respondía `/health`.

Mientras tanto, la captura de precios que sí funcionaba —y que lleva el historial acumulado desde
el 27-08— era `tools/scraper-rapido/sparta.py`, un script en **Python**. Es decir, el scraper de
verdad ya estaba en Python antes de que nadie lo decidiera; lo que había en Java era el hueco
donde iba a ir.

Al construir el scraper completo (PR #11) se hizo entero en Python, con seis tiendas, procesado
de imágenes y batería de tests. Esta decisión registra ese cambio, que hasta ahora no estaba
escrito en ninguna parte.

## Alternativas consideradas

- **Reescribirlo en Java para respetar el ADR-013.** Descartada por dos motivos. El primero es de
  fondo: el ecosistema de scraping en Python es sencillamente mejor, y todo lo que ya funcionaba
  estaba ahí. El segundo es de calendario: quedaban días para el code freeze y reescribir algo
  que ya andaba, para que hiciera lo mismo, es el peor uso posible de ese tiempo.
- **Dejar los dos: el scraper Python fuera del repo y un adaptador Java que lo invoque.** Suma
  una capa que solo existe para salvar la coherencia de lenguaje, y deja dos cosas que mantener
  en vez de una.

## Decisión

`scraper-service` es un servicio **Python 3 con FastAPI**. Tiene su propio `pyproject.toml` y su
propio `Dockerfile`, y se levanta desde el
mismo `docker-compose.yml` que el resto.

Sigue siendo **un microservicio del sistema**: un proceso que se despliega solo, con su propio
ciclo de vida y su propia base. Lo único que cambia es en qué está escrito.

## Consecuencias

### Lo que se gana

- **El ecosistema correcto para el problema.** Parsear HTML y JSON-LD, y procesar imágenes, es
  territorio de Python.
- **Un argumento de arquitectura, no una excusa.** El diseño se defiende diciendo que cada
  servicio está separado porque cambia por motivos distintos ([ARQUITECTURA.md §2 y §3](../ARQUITECTURA.md)).
  Haberle cambiado el lenguaje entero al scraper **sin tocar los otros tres servicios** es la
  prueba de que ese desacople es real y no una lámina. En un monolito no se puede hacer.
- **Los tests corren con fixtures HTML reales** de cada tienda, así que un cambio en el sitio de
  una tienda se detecta sin salir a internet.

### Lo que cuesta, y hay que decirlo

- **Dos cadenas de construcción.** `dotnet build` no comprueba Python: hay que correr también
  sus pruebas y linters. Cuando exista CI, son dos trabajos y no uno.
- **Dos ecosistemas de dependencias** que vigilar, y por lo tanto dos superficies distintas.
- **El conocimiento se puede concentrar en una persona.** Si solo uno del equipo toca Python, ese
  servicio queda sin segunda opinión — y en un ramo donde la nota es individual, eso perjudica a
  los tres. Se compensa con el `README.arquitectura.md` del servicio, que explica cómo agregar
  una tienda sin haber escrito el resto.
- **El ADR-013 quedó reemplazado** para los servicios activos por el ADR-020.

### Lo que NO cambia

La frontera del scraper sigue siendo la misma: posee su persistencia y se despliega por separado.
El retiro posterior de `price-service` no cambia esa independencia.
