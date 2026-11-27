# AGENTS.md

Guía para agentes de IA que trabajen en este repo. **Lee solo lo que la tarea necesita.**

## Qué es esto

"Cacha el Precio": comparador de precios de **ropa y calzado** en Chile. Proyecto del ramo
**DSY1107 · Desarrollo Cloud Native I**, equipo de 3. **Lo que sigue es la EFT, el 2-dic-2026 a
las 17:00.** Está en producción (`www.cacha-el-precio.com` y `api.cacha-el-precio.com`), pero la
EC2 **no se mantiene encendida 24/7** por los créditos de AWS Academy: que la API no responda es lo
normal fuera de las sesiones de trabajo, no una avería.

Stack: **.NET 10 / ASP.NET Core** para `gateway` (BFF), `product-service` y `notification-service` ·
**Python 3.14 / FastAPI** para `scraper-service` · **Vue 3 + Vite** en `frontend/`, incorporado con
`git subtree` desde el repo de Panditax, que es el que se publica (ver `docs/DESARROLLO.md`) ·
**Cognito** · **API Gateway** · **SQS + SNS** · **EC2 con Docker Compose** · **Terraform**.

> 🔴 **Antes de tocar nada, lee `docs/CONTEXTO.md`.** Es una página: qué corre, qué está decidido,
> qué está abierto y qué documento leer para cada tarea. Si das por buena una parte de otro
> documento sin cruzarla con ese, puedes estar programando contra un sistema que ya no existe.

## Índice de documentos — abre solo el que corresponda

| Si la tarea es sobre… | Lee | Aprox. |
|---|---|---|
| **El estado de hoy, las decisiones vigentes y la deuda abierta** | `docs/CONTEXTO.md` | 108 líneas |
| **Qué falta construir para la EFT, en qué orden y por qué** | `docs/PLAN-EFT.md` | 390 líneas |
| Las tareas pendientes, el git flow y los acuerdos del equipo | `docs/TAREAS.md` | 120 líneas |
| La idea, el alcance, las tiendas, el modelo de datos, los roles, lo legal | `docs/EL-PROYECTO.md` | 353 líneas |
| Servicios y sus límites, persistencia, mensajería, **seguridad e identidad**, despliegue | `docs/ARQUITECTURA.md` | 332 líneas |
| **Colas, tópicos, sobres, errores, panel de administración y correo** | `docs/MENSAJERIA.md` | 368 líneas |
| **Montar, prender, apagar y cambiar de cuenta con `tools/tf.sh`** (y mudar el dominio) | `docs/OPERAR-EN-AWS.md` | 527 líneas |
| **Programar en local, lo que se corre antes de un PR y el `subtree` del front** | `docs/DESARROLLO.md` | 169 líneas |
| **El CI, el CD, los secretos de GitHub y cómo llega el front a `www`** | `docs/CICD.md` | 131 líneas |
| Qué evalúa cada entrega, el vocabulario para el informe y el guion de la demo | `docs/EVALUACIONES.md` | 163 líneas |
| Una decisión ya tomada, y el índice de todas | `docs/adr/README.md` y `docs/adr/NNN-*.md` | 1 pág c/u |
| Cómo cambió el proyecto desde agosto y por qué (para la defensa) | `docs/HISTORIA.md` | 134 líneas |
| Qué se hizo un día concreto | `docs/BITACORA.md` (busca por fecha; no la leas entera) | 1802 líneas |
| Presentar el proyecto a alguien de afuera | `README.md` | 250 líneas |

Y el README de cada módulo. Todos siguen la misma forma: qué es, «En 10 segundos», dónde está en el
sistema (diagrama), cómo funciona por dentro, rutas, carpetas, configuración, cómo probarlo y deuda.
**Es lo que más rinde cuando la tarea es sobre ese módulo:**

| Si la tarea es sobre… | Lee | Aprox. |
|---|---|---|
| El BFF: cómo decide 401/403/200, las rutas públicas y privadas, el panel de mensajería | `gateway/README.md` | 185 líneas |
| El catálogo: el modelo producto-ofertas, el flujo 1 (consumir) y el flujo 2 (publicar), el armario | `product-service/README.md` | 170 líneas |
| Los avisos por correo: el consumidor, la idempotencia, los errores y la bandeja | `notification-service/README.md` | 133 líneas |
| El scraper: capas, un barrido paso a paso, las tiendas y cómo se agrega una | `scraper-service/README.md` | 183 líneas |
| **El frontend Vue: estructura por módulos, seguridad, pruebas de humo, AdSense** | `frontend/README.md` | 572 líneas |
| **Terraform: qué reemplazó, `tools/tf.sh`, fases, barreras, `import` y reglas del estado** | `infra/terraform/README.md` | 235 líneas |
| El capturador viejo de Sparta, que tiene el historial más largo | `tools/scraper-rapido/README.md` | 83 líneas |

**No cargues todos los documentos.** Casi ninguna tarea necesita más de dos.

`docs/HISTORIA.md` resume cómo cambió el proyecto desde agosto y dice cómo ver con `git show` los
documentos viejos (`INTEGRACION`, `PLAN`, `REQUISITOS`, `DESPLIEGUE`…). **No describe el sistema de
hoy**: ábrelo solo si la tarea pide historia. Las rúbricas
en PDF están en `docs/rubricas/` — no las leas salvo que se pidan explícitamente.

---

## Reglas del repo

### Comunicación

- **Español de Chile, tratando de "tú".** Sin voseo. Aplica a código, comentarios, commits y docs.
- **Commits en español**, sin firma ni `Co-Authored-By`.
- **Nada de firmas de herramientas en lo que publiques**: ni en commits, ni en descripciones de PR,
  ni en comentarios o issues. Nada de «🤖 Generated with Claude Code», «Co-Authored-By: Claude» ni
  equivalentes, aunque la herramienta lo traiga por defecto. Lo que se publica lo firma la persona
  que trabaja, no el agente.
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

- **Nunca subir al repo:** `.env`, `.env.local`, `*.pem`, `*.p12`, `credentials`, `*.tfvars`,
  `*.tfstate`, ni ningún archivo con llaves de AWS, client secrets o claves de BD. El hook de
  `tools/hooks` lo frena (`git config core.hooksPath tools/hooks`).
- La configuración va en **variables de entorno**; el repo lleva `.env.example` con las llaves
  vacías. Los `.env` reales los escribe `tools/tf.sh` fuera de git. (No hay gestor de secretos en
  AWS todavía: es deuda conocida, en `docs/CONTEXTO.md`.)
- Si un secreto llegó a subirse: **rotarlo**, no basta con borrarlo en el commit siguiente.
- **Nada hardcodeado**: IDs y URLs de Cognito, endpoints de tiendas, URLs de API y strings de
  conexión salen de configuración.

### Infraestructura

- **Nada se crea a mano en la consola de AWS.** Todo va en `infra/terraform/` y se aplica con
  `tools/tf.sh`. Colas, tópicos y reglas se declaran en `infra/mensajeria/topologia.json`.
- Un `tf.sh` que pide escribir `BORRAR` no se acepta sin leer qué borra.

### Código

- Todo cambio del esquema de Product Service o Notification Service va por una **migración de EF
  Core**. Nunca un `ALTER` a mano. El esquema PostgreSQL del scraper se mantiene en
  `infra/postgres/init/01-scraper-schema.sql` hasta introducir una herramienta de migraciones.
- **Agregar una tienda = un paquete nuevo en `scraper-service/src/scraper/scrapers/`**, con su
  `scraper.py` y su `parser.py`, más un fixture HTML real en `tests/fixtures/`. No se tocan
  `product-service`, el BFF ni el frontend, salvo que cambie el contrato compartido: **una tienda
  nueva no debe obligar a tocar el resto**.
- El scraper respeta **1 request cada 1–2 segundos**, User-Agent identificable y `robots.txt`.
- Los tests que necesitan PostgreSQL usan **Testcontainers o `docker-compose`**,
  nunca una instancia compartida.
- **Los tests no deben depender de AWS.** El del BFF no verifica firmas de verdad justamente por
  eso: un token que no se presenta se rechaza antes de ir a buscar el JWKS, así que corren con el
  laboratorio apagado. Los que usan AWS real van aparte y solo corren con `CEP_AWS_REAL=1`.
- Dependencias nuevas: preguntar antes de agregarlas.

### Alcance

- Si la tarea no está en `docs/TAREAS.md`, **pregunta antes de implementarla**.
- No refactorizar ni reformatear código que otra persona tiene 🟡 en curso en `docs/TAREAS.md`
  sin avisar.

---

## Cómo mantener el orden

- **Al cerrar una sesión de trabajo, anota el avance en `docs/BITACORA.md`**: qué se hizo, qué
  costó, qué queda pendiente. También cuando una decisión cambia respecto del plan. Si el usuario
  no lo pide, recuérdaselo.
- **Cada tema vive en un solo documento**, y los demás lo enlazan: el *estado de hoy* en
  `CONTEXTO.md`, el *qué* en `EL-PROYECTO.md`, el *cómo y por qué* en `ARQUITECTURA.md`, el *qué
  falta y en qué orden* en `PLAN-EFT.md`, las *tareas* en `TAREAS.md`, el *qué se evalúa* en
  `EVALUACIONES.md`, y el *cómo operar* en `OPERAR-EN-AWS.md`.
- **Todos los documentos y READMEs siguen la misma forma:** un encabezado con qué es, para quién y
  la fecha de última revisión; «📍 En 10 segundos»; la leyenda de estados (✅ 🟡 ⬜ ⛔ 🔎 ⏸️ 🚫);
  tablas y diagramas Mermaid antes que párrafos largos; lo vigente arriba y la historia abajo; y
  «Ver también» al final.
- **No crees documentos nuevos en `docs/` sin necesidad.** Antes de crear uno, busca dónde cabe lo
  que quieres escribir. Un tema nuevo grande puede justificar uno; una nota, no.
- **Una decisión técnica nueva es un ADR**, no un párrafo suelto. Formato y numeración en
  `docs/adr/README.md`: contexto → alternativas → decisión → **consecuencias, incluidas las malas**.
- Al modificar un documento, **actualiza su índice** y la fecha de «última revisión».
- **Un documento que pasa de ~600 líneas se resume o se parte**, porque se lee entero. **Excepción:
  `docs/BITACORA.md`**, que solo se agrega por fecha y se busca, no se lee entera; y `docs/HISTORIA.md`,
  que no se mantiene.
- **Los conteos de las tablas de arriba se sacan del archivo (`wc -l`), no de memoria.** Un índice
  que miente hace perder más tiempo que no tener índice.
- `README.md` es la cara pública: corto, **sin comandos** y sin detalle interno del ramo. Su tabla de
  **Estado** dice lo que corre de verdad, no lo planificado: si cambia el sistema, cambia esa tabla
  en el mismo PR.
- **Cuando el código contradiga un documento, no lo arregles en silencio ni lo dejes pasar:**
  anótalo en la deuda abierta de `docs/CONTEXTO.md`, o escribe el ADR que falta. Un documento que
  miente es peor que uno que falta, porque en la defensa oral la contradicción la encuentra cualquiera.
- **La evidencia nueva (logs de pruebas, capturas) no va al repo.** Se guarda fuera, con el
  material de los informes. La de `docs/evidencia/` es la que citan los informes ya entregados.
