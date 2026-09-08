# AGENTS.md

Guía para agentes de IA que trabajen en este repo. **Lee solo lo que la tarea necesita.**

## Qué es esto

"Cacha el Precio": comparador de precios de **ropa y calzado** en Chile. Proyecto del ramo
**DSY1107 · Desarrollo Cloud Native I**, equipo de 3, primera entrega **13-sep-2026**.
**Ya está en producción**: `www.cacha-el-precio.com` y `api.cacha-el-precio.com`.

Stack: **Micronaut 5 / Java 25** para `gateway` (BFF), `product-service` y `price-service` ·
**Python 3 / FastAPI** para `scraper-service`, que está fuera del monorepo Maven ·
**Vue 3** en el frontend, que hoy vive en [otro repositorio](https://github.com/Panditax727/Cacha-el-Precio-Frontend) ·
**AWS Cognito** como IDaaS · **EC2 con Docker Compose**.

> 🔴 **Antes de tocar nada, lee `docs/INTEGRACION.md`.** El sistema desplegado y lo que dicen los
> documentos **no coinciden en todo**, y ese archivo es el que lleva la cuenta: qué decisiones se
> tomaron construyendo, cuáles contradicen un ADR, y qué está roto ahora mismo. Si escribes algo
> dando por buena una parte de `ARQUITECTURA.md` sin cruzarla con ese documento, es probable que
> estés programando contra un sistema que ya no existe.

## Índice de documentos — abre solo el que corresponda

| Si la tarea es sobre… | Lee | Aprox. |
|---|---|---|
| **Qué está roto, qué se decidió construyendo, qué contradice lo escrito** | `docs/INTEGRACION.md` | ~310 líneas |
| La idea, el alcance, las tiendas, el modelo de datos, los riesgos, costos | `docs/PLAN.md` | ~300 líneas |
| Por qué hay microservicios / BFF / cola, por qué Micronaut, cómo escala, ADRs | `docs/ARQUITECTURA.md` | ~570 líneas |
| Quién hace qué, roadmap semanal, git flow, checklist | `docs/TAREAS.md` | ~410 líneas |
| En qué orden se despliega en AWS, restricciones del Learner Lab | `docs/DESPLIEGUE.md` | ~130 líneas |
| Cómo funciona el login, qué valida el BFF, cómo se replica la identidad | `docs/IDENTIDAD.md` | ~210 líneas |
| Qué valida el BFF en el código, y cómo se prueba | `gateway/README.md` | ~90 líneas |
| Qué evalúa el ramo, equivalencias de vocabulario, guion de la demo | `docs/EVALUACIONES.md` | ~145 líneas |
| Qué se hizo cada semana | `docs/BITACORA.md` | corto |
| Una decisión puntual ya tomada | `docs/adr/NNN-*.md` | 1 pág c/u |
| Presentar el proyecto a alguien de afuera | `README.md` | ~95 líneas |

**No cargues todos los documentos.** Casi ninguna tarea necesita más de dos.
Las rúbricas en PDF están en `docs/rubricas/` — no las leas salvo que se pidan explícitamente.

---

## Reglas del repo

### Comunicación

- **Español de Chile, tratando de "tú".** Sin voseo. Aplica a código, comentarios, commits y docs.
- **Commits en español**, sin firma ni `Co-Authored-By`.
- **Confirmar antes de commitear.** Nunca commitear sin que lo pidan.

### Git

- `feature/*` → PR a `development` → PR a `main`. Nunca escribir directo a `main`.
- Nadie mergea su propio PR.

### Secretos y configuración

- **Nunca subir al repo:** `.env`, `.env.local`, `*.pem`, `*.p12`, `credentials`,
  `application-local.yml`, ni ningún archivo con llaves de AWS, client secrets o claves de BD.
- La configuración va en **variables de entorno**; el repo lleva `.env.example` con las llaves
  vacías. En AWS, en **Secrets Manager**.
- Si un secreto llegó a subirse: **rotarlo**, no basta con borrarlo en el commit siguiente.
- **Nada hardcodeado**: IDs y URLs de Cognito, endpoints de tiendas, URLs de API y strings de
  conexión salen de configuración.

### Código

- Todo cambio de esquema va por una **migración de Flyway**. Nunca un `ALTER` a mano.
- **Agregar una tienda = un paquete nuevo en `scraper-service/src/scraper/scrapers/`**, con su
  `scraper.py` y su `parser.py`, más un fixture HTML real en `tests/fixtures/`. No se tocan
  `product-service`, `price-service`, el BFF ni el frontend.
  (La regla decía "implementar `AdaptadorTienda`", de cuando el scraper era Java. Cambió el
  lenguaje, no la idea: **una tienda nueva no debe obligar a tocar el resto**.)
- El scraper respeta **1 request cada 1–2 segundos**, User-Agent identificable y `robots.txt`.
- Los tests que necesitan Postgres o RabbitMQ usan **Testcontainers o `docker-compose`**,
  nunca una instancia compartida.
- **Los tests no deben depender de AWS.** El del BFF no verifica firmas de verdad justamente por
  eso: un token que no se presenta se rechaza antes de ir a buscar el JWKS, así que corren con el
  laboratorio apagado. El que sí usa un token real de Cognito va aparte y se salta si no hay
  credenciales.
- Dependencias nuevas: preguntar antes de agregarlas.

### Alcance

- Si la tarea no está en `docs/TAREAS.md`, **pregunta antes de implementarla**.
- No refactorizar ni reformatear código de otro carril (ver el reparto en `docs/TAREAS.md`)
  sin avisar.

---

## Cómo mantener el orden

- **Al cerrar una sesión de trabajo, anota el avance en `docs/BITACORA.md`**: qué se hizo, qué
  costó, qué queda pendiente. También cuando una decisión cambia respecto del plan. Si el usuario
  no lo pide, recuérdaselo.
- Los documentos de `docs/` **no se duplican entre sí**. Cada tema vive en un solo lugar:
  el *qué* en `PLAN.md`, el *por qué* en `ARQUITECTURA.md`, el *quién y cuándo* en `TAREAS.md`,
  el *qué se evalúa* en `EVALUACIONES.md`. Si algo calza en dos, va en uno y el otro lo enlaza.
- **Una decisión técnica nueva es un ADR**, no un párrafo suelto. Formato en
  `docs/ARQUITECTURA.md` §13: contexto → alternativas → decisión → **consecuencias, incluidas las malas**.
- Al modificar un documento, **actualiza su índice** y la fecha de "última revisión" del encabezado.
- Si un documento pasa de ~600 líneas, pártelo y actualiza la tabla de arriba.
- `README.md` es la cara pública: se mantiene corto y sin detalle interno del ramo. Su tabla de
  **Estado** dice lo que corre de verdad, no lo planificado — si cambia el sistema, cambia esa
  tabla en el mismo PR.
- **Cuando el código contradiga un documento, no lo arregles en silencio ni lo dejes pasar:**
  anótalo en `docs/INTEGRACION.md` con dueño, o escribe el ADR que falta. Un documento que miente
  es peor que uno que falta, porque en la defensa oral la contradicción la encuentra cualquiera.
