# AGENTS.md

Guía para agentes de IA que trabajen en este repo. **Lee solo lo que la tarea necesita.**

## Qué es esto

"Cacha el Precio": comparador de precios de **ropa y calzado** en Chile. Proyecto del ramo
**DSY1107 · Desarrollo Cloud Native I**, equipo de 3, primera entrega **13-sep-2026**.
**Ya está en producción**: `www.cacha-el-precio.com` y `api.cacha-el-precio.com` — con el matiz de
que la EC2 **no se mantiene encendida 24/7**, por créditos limitados en la cuenta que la aloja. Que
la API no responda es lo normal fuera de las sesiones de trabajo, no una avería.

Stack: **.NET 10 / ASP.NET Core / C#** para `gateway` (BFF) y `product-service` ·
**Python 3 / FastAPI** para `scraper-service` ·
**Vue 3 + Vite** en `frontend/`, incorporado con `git subtree` desde
[el repositorio de Panditax](https://github.com/Panditax727/Cacha-el-Precio-Frontend), que sigue
siendo su origen y se puede sincronizar en los dos sentidos (ver `docs/INTEGRACION-FRONTEND.md` §11) ·
**AWS Cognito** como IDaaS · **EC2 con Docker Compose**.

> 🔴 **Antes de tocar nada, lee `docs/INTEGRACION.md`.** El sistema desplegado y lo que dicen los
> documentos **no coinciden en todo**, y ese archivo es el que lleva la cuenta: qué decisiones se
> tomaron construyendo, cuáles contradicen un ADR, y qué está roto ahora mismo. Si escribes algo
> dando por buena una parte de `ARQUITECTURA.md` sin cruzarla con ese documento, es probable que
> estés programando contra un sistema que ya no existe.

## Índice de documentos — abre solo el que corresponda

| Si la tarea es sobre… | Lee | Aprox. |
|---|---|---|
| **Qué está roto, qué se decidió construyendo, qué contradice lo escrito** | `docs/INTEGRACION.md` | 386 líneas |
| **El frontend: cómo entró al monorepo, qué contrato habla y el plan por fases** | `docs/INTEGRACION-FRONTEND.md` | 826 líneas |
| La idea, el alcance, las tiendas, el modelo de datos, los riesgos, costos | `docs/PLAN.md` | 407 líneas |
| Historias de usuario, requisitos funcionales y no funcionales | `docs/REQUISITOS.md` | 396 líneas |
| Límites de servicios, BFF, persistencia, escalado y ADR | `docs/ARQUITECTURA.md` y `docs/adr/020-*.md` | — |
| Quién hace qué, estado semana a semana, git flow, checklist | `docs/TAREAS.md` | 399 líneas |
| **Levantar el proyecto en una cuenta de AWS nueva** (o cuando se acaben los tokens) | `docs/MIGRACION.md` | 241 líneas |
| En qué orden se despliega en AWS, restricciones del Learner Lab | `docs/DESPLIEGUE.md` | 201 líneas |
| Cómo funciona el login, qué valida el BFF, cómo se replica la identidad | `docs/IDENTIDAD.md` | 225 líneas |
| Qué evalúa el ramo, equivalencias de vocabulario, guion de la demo | `docs/EVALUACIONES.md` | 188 líneas |
| Qué se hizo cada semana | `docs/BITACORA.md` | 409 líneas |
| Una decisión puntual ya tomada | `docs/adr/NNN-*.md` | 1 pág c/u |
| Presentar el proyecto a alguien de afuera | `README.md` | 177 líneas |

Y el README de cada módulo, que es el más corto y el que más rinde cuando la tarea es sobre ese
módulo en concreto:

| Si la tarea es sobre… | Lee | Aprox. |
|---|---|---|
| Qué valida el BFF en el código, y cómo se prueba | `gateway/README.md` | 113 líneas |
| **El frontend Vue: estructura por módulos, seguridad, pruebas de humo, AdSense** | `frontend/README.md` | 492 líneas |
| Las rutas de productos y catálogos, el versionado por header | `product-service/README.md` | 108 líneas |
| El scraper de Python: cómo se agrega una tienda | `scraper-service/README.md` | 168 líneas |
| El detalle interno del scraper (dominio, imágenes, repositorios) | `scraper-service/README.arquitectura.md` | 120 líneas |
| El capturador de Sparta que tiene el historial acumulado | `tools/scraper-rapido/README.md` | 83 líneas |

**No cargues todos los documentos.** Casi ninguna tarea necesita más de dos.

> 🧹 **Fuera del índice a propósito:** `scraper-service/CONVERS.md` es un diagrama ASCII del
> flujo del scraper de **Converse** —la marca—, no de una conversación. El nombre confunde y el
> contenido cabe dentro de `README.arquitectura.md`. Pendiente de absorber o renombrar; es de
> Panditax, así que se avisa antes de moverlo.
Las rúbricas en PDF están en `docs/rubricas/` — no las leas salvo que se pidan explícitamente.

---

## Reglas del repo

### Comunicación

- **Español de Chile, tratando de "tú".** Sin voseo. Aplica a código, comentarios, commits y docs.
- **Commits en español**, sin firma ni `Co-Authored-By`.
- **Confirmar antes de commitear.** Nunca commitear sin que lo pidan.
- La regla de arriba se aplica **hacia afuera con más fuerza**: commitear en local es
  reversible y solo lo ve quien trabaja. Abrir un PR, comentar o cerrar algo lo ven los tres.

### Git

- `feature/*` → PR a `development` → PR a `main`. Nunca escribir directo a `main`.
- **No abras un pull request sin que te lo pidan.** Deja el trabajo commiteado en su rama y
  avisa que está listo; abrir el PR es decisión de quien trabaja, no del agente. Un PR abierto
  le llega al equipo entero como una notificación y le pide revisión a alguien: no es un paso
  técnico más, es empezarle una conversación a otras personas.
- Lo mismo vale para **cerrar, mergear o rebasar** un PR, y para **tocar la rama de otro**.
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

- Todo cambio del esquema de Product Service va por una **migración de EF Core**. Nunca un
  `ALTER` a mano. El esquema PostgreSQL del scraper se mantiene en
  `infra/postgres/init/01-scraper-schema.sql` hasta introducir una herramienta de migraciones.
- **Agregar una tienda = un paquete nuevo en `scraper-service/src/scraper/scrapers/`**, con su
  `scraper.py` y su `parser.py`, más un fixture HTML real en `tests/fixtures/`. No se tocan
  `product-service`, el BFF ni el frontend, salvo que cambie el contrato compartido.
  (La regla decía "implementar `AdaptadorTienda`", de cuando el scraper era Java. Cambió el
  lenguaje, no la idea: **una tienda nueva no debe obligar a tocar el resto**.)
- El scraper respeta **1 request cada 1–2 segundos**, User-Agent identificable y `robots.txt`.
- Los tests que necesitan PostgreSQL usan **Testcontainers o `docker-compose`**,
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
  ⚠️ **`docs/ARQUITECTURA.md` ya va en 677** y toca partirlo: las secciones 1 a 10 son el
  porqué del diseño y la 11 a 13 son la nube y los ADR. Pendiente, no urgente.
- **Los conteos de la tabla se sacan del archivo, no de memoria.** Estaban todos desfasados
  hasta el 07-09 y faltaba `docs/REQUISITOS.md` entero. Un índice que miente hace perder más
  tiempo que no tener índice.
- `README.md` es la cara pública: se mantiene corto y sin detalle interno del ramo. Su tabla de
  **Estado** dice lo que corre de verdad, no lo planificado — si cambia el sistema, cambia esa
  tabla en el mismo PR.
- **Cuando el código contradiga un documento, no lo arregles en silencio ni lo dejes pasar:**
  anótalo en `docs/INTEGRACION.md` con dueño, o escribe el ADR que falta. Un documento que miente
  es peor que uno que falta, porque en la defensa oral la contradicción la encuentra cualquiera.
