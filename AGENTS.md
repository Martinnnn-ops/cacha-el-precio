# AGENTS.md

Guía para agentes de IA que trabajen en este repo. **Lee solo lo que la tarea necesita.**

## Qué es esto

"Cacha el Precio": comparador de precios de zapatillas en Chile. Proyecto del ramo
**DSY1107 · Desarrollo Cloud Native I**, equipo de 3, primera entrega **13-sep-2026**.
Stack: **Micronaut (Java) + AWS + Cognito + RabbitMQ + React**. 4 microservicios:
`scraper`, `catalog`, `price`, `gateway` (BFF).

## Índice de documentos — abre solo el que corresponda

| Si la tarea es sobre… | Lee | Aprox. |
|---|---|---|
| La idea, el alcance, las tiendas, el modelo de datos, los riesgos, costos | `docs/PLAN.md` | ~300 líneas |
| Por qué hay microservicios / BFF / cola, por qué Micronaut, cómo escala, ADRs | `docs/ARQUITECTURA.md` | ~570 líneas |
| Quién hace qué, roadmap semanal, git flow, checklist | `docs/TAREAS.md` | ~410 líneas |
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
- **Agregar una tienda = implementar `AdaptadorTienda`.** No se tocan `catalog`, `price`,
  el BFF ni el frontend.
- El scraper respeta **1 request cada 1–2 segundos**, User-Agent identificable y `robots.txt`.
- Los tests que necesitan Postgres o RabbitMQ usan **Testcontainers o `docker-compose`**,
  nunca RDS ni una instancia compartida.
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
  `docs/ARQUITECTURA.md` §12: contexto → alternativas → decisión → **consecuencias, incluidas las malas**.
- Al modificar un documento, **actualiza su índice** y la fecha de "última revisión" del encabezado.
- Si un documento pasa de ~600 líneas, pártelo y actualiza la tabla de arriba.
- `README.md` es la cara pública: se mantiene corto y sin detalle interno del ramo.
