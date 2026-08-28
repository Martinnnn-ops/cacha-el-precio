# Tareas del proyecto

> **Acá está lo que hay que hacer, en qué semana, y qué tiene que existir al final de cada una.**
> Si tienes 15 minutos y no sabes qué hacer, abre este documento, busca la semana en curso y
> toma cualquier tarea que no esté marcada.
>
> Última revisión: **27-08-2026**

---

## Cómo usar este documento

1. **Busca la semana en curso.** Dentro hay una bolsa de tareas, no carriles con dueño fijo.
2. **En la reunión semanal se reparte** quién toma qué de esa bolsa. Lo que se acuerde se
   anota ahí mismo, entre paréntesis, al lado de la tarea.
3. **Marca el `[x]` cuando termines** y súbelo. Así los otros dos ven el avance sin preguntar.
4. **Anota lo que hiciste en [BITACORA.md](BITACORA.md)** al final de la semana. Son 3 líneas
   y en septiembre esa bitácora es el informe, ya escrito.

**Otros documentos:** la idea y el alcance en [PLAN.md](PLAN.md) · el porqué técnico en
[ARQUITECTURA.md](ARQUITECTURA.md) · lo que evalúa el ramo en [EVALUACIONES.md](EVALUACIONES.md)
· los requisitos en [REQUISITOS.md](REQUISITOS.md).

---

## 📋 Índice

- [Cómo nos repartimos el trabajo](#-cómo-nos-repartimos-el-trabajo)
- [Dónde estamos hoy](#-dónde-estamos-hoy--27-08-2026)
- [Las tres fechas que mandan](#-las-tres-fechas-que-mandan)
- [Semana 1 · 28–30 ago](#-semana-1--2830-ago--destrabar-cognito)
- [Semana 2 · 31 ago – 6 sep](#-semana-2--31-ago--6-sep--todo-a-la-nube)
- [Semana 3 · 7–10 sep](#-semana-3--710-sep--cerrar-y-congelar)
- [Cierre · 11–13 sep](#-cierre--1113-sep--informe-y-entrega)
- [Modo de trabajo](#-modo-de-trabajo)
- [Checklist final](#-checklist-final-lo-que-tiene-que-quedar-funcionando)

---

## 👥 Cómo nos repartimos el trabajo

**No hay carriles fijos.** Cada semana tiene una bolsa de tareas y en la **reunión semanal** se
reparte quién toma cada una, según lo que quedó pendiente y quién tiene tiempo esa semana.

Los tres tienen que **entender el proyecto completo**, no solo lo que les tocó: en la
presentación la nota es individual y la pregunta le puede tocar a cualquiera.

> ⚠️ **Dos cosas que sí necesitan dueño con nombre, y no pueden decidirse la última semana:**
>
> - **El EP1 se califica en dos mitades:** 60% el flujo OIDC del frontend y 40% la validación
>   del token en el BFF. Son dos personas distintas defendiendo dos notas distintas.
> - **El EP2 es nota individual y por indicador** (ver el mapa de puntaje en
>   [EVALUACIONES.md](EVALUACIONES.md#mapa-de-puntaje)). Cada indicador lo presenta alguien.
>
> **Ese reparto hay que cerrarlo a más tardar en la reunión del sábado 6 de septiembre**, para
> que cada uno llegue a la presentación habiendo trabajado de verdad lo que va a defender.

### La reunión semanal

Una hora, los sábados. Tres cosas y se acaba:

1. Cada uno cuenta qué hizo (esto es la bitácora, dicha en voz alta).
2. Se reparte la bolsa de la semana que entra.
3. Se anota en `BITACORA.md` lo que cambió respecto del plan, y por qué.

---

## 📍 Dónde estamos hoy · 27-08-2026

Estado real, verificado contra el repo y contra AWS — no de memoria.

| | Pieza | Estado |
|---|---|---|
| ✅ | Repo, ramas y acuerdos de trabajo | Andando. `main` protegido, `development` libre |
| ✅ | **Scraper de Sparta** | 🟢 **Capturando 3 veces al día desde el 27-08.** 2.088 productos por corrida |
| ✅ | `catalog-service` | Base con rutas versionadas y `/health`, mergeada |
| ✅ | Decisión de cómputo | **EC2 + Docker Compose**, cerrada y escrita en [ADR-008](adr/008-ec2-docker-compose.md) |
| 🔴 | **Cognito en AWS Academy** | **Sin probar.** Es el riesgo número uno del proyecto |
| ⬜ | Frontend | No existe |
| ⬜ | BFF / validación de JWT | No existe |
| ⬜ | API Gateway | No existe |
| ⬜ | RDS, VPC, despliegue | No existe |
| ⬜ | Scraper de Hites | No existe. El endpoint del plan original **da 500** |
| ⬜ | CI en GitHub Actions | **0 workflows.** Estaba escrito como si existiera |
| ⬜ | ADR | 3 de 14 escritos (008, 013, 014) |

> 🔴 **Lo más importante de esta tabla:** todo lo que puntúa en el EP1 —el 60% del frontend y
> el 40% del BFF— **todavía no existe**, y ambos dependen de que Cognito se pueda levantar.

---

## 📅 Las tres fechas que mandan

| Fecha | Qué | Por qué importa |
|---|---|---|
| ☁️ **dom 6-sep** | **El sistema tiene que estar en la nube** | Sin esto no hay EP1, aunque el código esté impecable |
| 🔒 **jue 10-sep** | **Code freeze** | Del 10 al 13 no se toca código: informe, ensayo y grabación |
| 📤 **dom 13-sep 23:59** | **Entrega formal** por GitHub + AVA | La fecha oficial. Se trata como colchón, no como meta |

### Lo que choca en el camino

Estas fechas no son del ramo, pero se comen las mismas horas:

| Fecha | Qué |
|---|---|
| vie 4-sep | Parcial 1 de OCY1102 (práctico, 16:00–19:00) |
| 7–12 sep | Parcial 1 de ISY1102 (día por confirmar) |
| mié 9-sep | Entrega EV1 de GPY1102 |
| jue 10-sep | Defensa EV1 de GPY1102 — **el mismo día del code freeze** |

> ⚠️ **El fin de semana del 5–6 está pedido dos veces:** el despliegue del EP1 y la EV1 de
> GPY1102. El que se puede mover es el despliegue: si se hace **entre semana**, el fin de semana
> queda libre para la EV1. Lo que no se puede es descubrirlo el sábado 5.

---

## 🔓 Semana 1 · 28–30 ago — Destrabar Cognito

### 🎯 Objetivo

Salir del fin de semana **sabiendo si Cognito se puede o no**, y con el login andando en el
computador de cada uno.

### 🔴 Lo primero, antes que cualquier otra cosa

- [ ] **Entrar a la consola de AWS Academy e intentar crear un User Pool de Cognito**
  - ↳ *AWS Academy restringe servicios. **El 100% del EP1 depende de esto**: el 60% del frontend
    y el 40% del BFF. Son 20 minutos y lleva una semana postergándose.*
  - ↳ *Si **no** se puede: el plan B es **Keycloak como un contenedor más del compose**. Esa
    decisión hay que tomarla esta semana, no la otra.*

### La bolsa de la semana

**Identidad**
- [ ] **User Pool oficial** con política de contraseñas y verificación por email
- [ ] Grupos **`admin`** y **`usuario`** — *sin grupos solo hay 200 y 401; el **403** aparece con roles*
- [ ] **Resource Server** con `precios:leer`, `seguimiento:escribir`, `ingesta:escribir`
- [ ] **Google como IdP federado** — *Google y no GitHub: GitHub es OAuth2 pero **no OIDC**, no emite `id_token`*
- [ ] **3 usuarios de prueba**: un admin, uno normal, uno que entre con Google

**Que los tres entiendan el login** *(no es relleno: la nota del EP2 es individual)*
- [ ] Cada uno levanta una página fea con `oidc-client-ts` que haga login
- [ ] Encontrar a mano en DevTools, en la URL de `/authorize`: `code_challenge`,
      `code_challenge_method=S256`, `state` y `nonce`
- [ ] Pegar el `access_token` en [jwt.io](https://jwt.io) y anotar `iss`, `client_id`, `scope`,
      `exp`, `token_use`, `cognito:groups`

**Infraestructura de trabajo**
- [x] **`docker-compose.yml`** con Postgres + RabbitMQ para desarrollo local ✅ 27-08
  - ↳ *Doble valor: es el mismo archivo que después corre en la EC2 ([ADR-008](adr/008-ec2-docker-compose.md))*
  - ↳ *Incluye `pg_trgm`, `unaccent` y los esquemas `catalog` y `price` (ADR-010), creados solos
    al primer arranque desde `infra/postgres/init/`*
- [ ] **CI en GitHub Actions**: que compile y corra los tests en cada PR
  - ↳ *Hoy son 0 workflows. `TAREAS.md` lo daba por hecho desde la semana 0*

**Datos**
- [ ] **Adaptador de Hites** con Jsoup, usando `https://www.hites.com/search?q=`
  - ↳ *El `Search-UpdateGrid` del plan original **devuelve 500 desde el 27-08***
- [x] Revisar el **modelo de datos contra los datos reales** ✅ 27-08
  - ↳ *Se cargaron los 2.088 productos en Postgres y se corrigieron 5 cosas del modelo.
    Migraciones en `infra/db/`. Detalle en [BITACORA.md](BITACORA.md)*
- [ ] 🔴 **Matcher: extraer el número de modelo y exigir coincidencia exacta**
  - ↳ *Medido: `574 Negra` vs `515 Negra` da 0,660 — **son zapatos distintos** y el trigrama no
    los separa. Solo con `pg_trgm` sobre el nombre completo, el comparador va a emparejar mal*

### ✅ Al terminar la semana

Sabemos si Cognito se puede. Hay un User Pool con grupos y usuarios de prueba. Los tres
levantaron un login con sus manos.

---

## ☁️ Semana 2 · 31 ago – 6 sep — Todo a la nube

### 🎯 Objetivo

**Que el sistema exista en internet.** Es la semana crítica: casi todos los indicadores de la
presentación piden mostrarlo andando en la nube, no en `localhost`.

> 🔴 **Adelantar el despliegue a entre semana.** El fin de semana del 5–6 ya está comprometido
> con la EV1 de GPY1102, y el viernes 4 es el parcial de OCY. **El objetivo real es tener el
> sistema arriba el jueves 3.**

### La bolsa de la semana

**Frontend — es el 60% del EP1**
- [ ] React + Vite con `react-oidc-context`: **registro**, login y logout
  - ↳ *Ojo con el **registro**: no basta el login, hay que poder crear una cuenta nueva*
- [ ] **Guard de ruta**: sin sesión, te manda al login
- [ ] **Interceptor**: cada llamada a la API lleva el token automáticamente
- [ ] Mostrar en pantalla los **roles y scopes leídos del token** — *es un punto explícito de la rúbrica*
- [ ] Buscador de modelos y ficha de producto
- [ ] **Diseño real**: paleta, tipografía y distribución — *una app que se ve terminada se defiende mejor*
- [ ] Actualizar las **URLs de redirección** de Cognito al dominio de CloudFront
  - ↳ *Si se olvida, el login deja de funcionar en producción y no es obvio por qué*

**BFF y validación — es el 40% del EP1**
- [ ] `micronaut-security-jwt` en `gateway`, `catalog` y `price`
- [ ] Validar explícitamente **issuer, audience, firma contra el JWKS y vigencia**
  - ↳ *Por qué Cognito no trae `aud`: [ARQUITECTURA.md §9](ARQUITECTURA.md#9-el-detalle-del-audience-en-cognito)*
- [ ] **Autorización por rol** leyendo `cognito:groups` → una ruta que le dé **403** al no-admin
- [ ] Endpoints que devuelvan **200 / 401 / 403** de forma predecible — *son los de la demo*
- [ ] Migraciones con Flyway sobre el modelo ya validado con datos reales

**Nube**
- [ ] **VPC** con subredes públicas y privadas en **dos zonas**, y **NAT Gateway**
      (ver [ARQUITECTURA.md §11](ARQUITECTURA.md#11-dónde-vive-esto-en-aws))
- [ ] **RDS Postgres** en subred privada — *fuera del compose: si se cae, el historial no se recupera*
- [ ] **EC2** con `docker compose up`: BFF, catalog, price y RabbitMQ
- [ ] **API Gateway HTTP API** con **JWT Authorizer** apuntando a Cognito
  - ↳ *Un token inválido se rechaza en el borde, antes de gastar la instancia*
- [ ] Mapear **todas** las rutas a los microservicios
- [ ] **CORS**: orígenes explícitos (CloudFront y `http://localhost:5173`), **sin usar `*`**
- [ ] Stages **`dev`** y **`prod`** + throttling
- [ ] **Frontend en S3 + CloudFront**
- [ ] Secretos en **Secrets Manager** — *nunca en el repo ni en el compose*
- [ ] Mover el **scraper a Lambda + EventBridge** y el crudo a S3
  - ↳ *Hoy corre en el notebook de Martín con un timer de systemd. Funciona, pero no es la nube*
- [ ] Endpoint **`POST /ingesta`** + app client de **client credentials** del scraper
  - ↳ *Es lo que hace que el flujo máquina a máquina se valide de verdad y no sea decorativo*

**Dominio** *(si alcanza — no puntúa en el EP1)*
- [ ] Matching con `pg_trgm` + tabla de candidatos
- [ ] Historial y cálculo del **descuento real**
- [ ] Revisar **30 productos a mano** y anotar el porcentaje de aciertos
  - ↳ *Da un número medido para el informe: "24 de 30 = 80%"*

### ✅ Al terminar la semana

Todo el tráfico entra por el API Gateway, con el token validado en el borde, y ningún servicio
expuesto directo a internet. **La URL de CloudFront abre y el login funciona.**

---

## 🏁 Semana 3 · 7–10 sep — Cerrar y congelar

### 🎯 Objetivo

Terminar lo que falta y **congelar el jueves 10**. Acá no se aprende nada nuevo.

- [ ] Cerrar el [checklist final](#-checklist-final-lo-que-tiene-que-quedar-funcionando):
      cada ruta probada con token, sin token y sin permiso
- [ ] **Gráfico del historial** de precios + botón "Descargar CSV"
- [ ] Test de contrato por scraper ("esta captura trajo más de N productos válidos")
- [ ] **Escribir los ADR que faltan** — los 14 están listados en
      [ARQUITECTURA.md §13](ARQUITECTURA.md#13-registro-de-decisiones-adr)
  - ↳ *Es mayormente copiar lo que ya está escrito. **De acá sale el informe de 5 páginas**, y en
    la defensa un ADR es literalmente la respuesta a «¿por qué?»*
- [ ] **Cerrar quién presenta cada indicador del EP2** (si no se hizo el 6)
- [ ] Revisar el `.gitignore` y que **no haya ningún secreto** en el repo

### 🧊 El 10 se congela

Si algo se cae el día 11 y no hay margen, no hay entrega. Por eso el freeze es tres días antes.

---

## 📤 Cierre · 11–13 sep — Informe y entrega

- [ ] **Informe ejecutivo de 5 páginas** contando la portada — o sea 4 de contenido
  - ↳ *Su punto central es **justificar la elección del IDaaS y del API Manager**. Si la bitácora
    y los ADR están al día, ya está escrito en un 80%*
- [ ] **Grabar un video de respaldo** de la demo completa
  - ↳ *Las demos en vivo se caen y el wifi de la sala falla. Con el video grabado sigues adelante*
- [ ] Ensayo cronometrado: **5 a 10 minutos**, ni uno más
- [ ] Verificar que el ambiente de AWS esté prendido y con crédito
- [ ] 📤 **Entregar: enlaces de GitHub a AVA + copia al correo de la docente**

---

## 📅 Después de la entrega

### Ideas para después (no ahora)

- [ ] **Kafka** — topic `precios.cambiados`. Es EP5 y EP6, **no** el EP1
- [ ] `alert-service` suscrito a `precio.cambiado` → avisos de restock por talla
- [ ] Nike.cl y Falabella con navegador headless
- [ ] **Ampliar a vestuario** además de calzado — es la dirección del proyecto, pero necesita el
      matching medido primero (ver [PLAN.md §2](PLAN.md#2-alcance))
- [ ] Migrar de EC2 a ECS Fargate si el ramo lo pide ([ADR-008](adr/008-ec2-docker-compose.md))
- [ ] Mercado Libre con OAuth

---

## 🛠️ Modo de trabajo

### Las ramas

```
feature/loquesea  ──PR──▶  development  ──PR──▶  main
                          (acá se prueba)      (producción real)
```

- Una **rama por funcionalidad**: `feature/login-google`, `feature/scraper-hites`
- Todo PR llega primero a **`development`**
- A **`main`** solo entra lo que ya funciona

**Protección de ramas, ajustada el 27-08:**

| Rama | Regla |
|---|---|
| `main` | Protegida: **1 revisión**, sin force-push, sin borrado |
| `development` | **Libre.** Se mergea sin esperar aprobación |

> `development` se liberó a propósito para no trabarse a dos semanas del freeze. **El acuerdo de
> que nadie mergea su propio PR sigue en pie** — ahora es un acuerdo entre nosotros, no una regla
> que GitHub imponga. El historial de PRs es la evidencia de que trabajamos los tres.

### Los acuerdos

| Acuerdo | Por qué |
|---|---|
| **Cada uno commitea lo suyo** | Si uno commitea todo, en el historial parece que trabajó una sola persona. Con nota individual, eso perjudica al resto |
| **Reunión de 1 hora los sábados** | Se reparte la bolsa de la semana y cada uno explica lo que hizo |
| **Nadie mergea su propio PR** | Evita errores y deja evidencia de revisión cruzada |
| **Cero secretos en el repo** | Van a Secrets Manager. Un secreto subido queda en el historial para siempre |

### Lo que debería revisar el CI

⚠️ **Todavía no existe: hay 0 workflows en el repo.** Está en la bolsa de la Semana 1.

1. Que **compile** y que **pasen los tests**
2. **gitleaks** — que no se cuele ninguna contraseña ni llave
3. **Trivy** — vulnerabilidades en las imágenes de Docker
4. **Dependabot** activo

---

## ✅ Checklist final: lo que tiene que quedar funcionando

Se marca a medida que avanza el proyecto. **Los tres tienen que poder responder sobre
cualquier fila**, la haya hecho quien la haya hecho.

### Identidad y frontend *(60% del EP1)*

- [ ] **Cognito creado** — consola con usuarios, grupos y política de contraseñas
- [ ] **Registro de usuario** — crear una cuenta nueva desde el frontend, en vivo
- [ ] **Login con Google** — entrar con una cuenta de Google
- [ ] **Authorization Code + PKCE** — DevTools con `code_challenge`, `S256`, `state` y `nonce`
- [ ] **Los tres tokens** — `id_token`, `access_token` y `refresh_token` decodificados
- [ ] **Roles y permisos del token** — la pantalla muestra `cognito:groups` y `scope`
- [ ] **Guard e interceptor** — ruta protegida que redirige · el header `Authorization` en DevTools

### Seguridad y backend *(40% del EP1)*

- [ ] **200 con token, 401 sin token** — probado en Postman
- [ ] **403 con usuario sin permiso** — token válido pero sin el rol necesario
- [ ] **Validación en el servicio** — el código que revisa `iss`, `client_id`, `token_use` y `exp`
- [ ] **Mensajería** — el panel de RabbitMQ con las colas y la DLQ
- [ ] **Historial de precios** — el gráfico con datos reales
  - ↳ ⚠️ *El scraper arrancó el **27-08**, no en la semana 0. Al freeze del 10-09 el historial
    tendrá **14 días**, no las 3 semanas que decía este checklist. No es recuperable: conviene
    decirlo en el informe antes de que lo pregunten.*

### Nube y plataforma

- [ ] **Client credentials** — el scraper saca su token y llama a `POST /ingesta`
- [ ] **API Gateway** — consola con las rutas, los stages `dev`/`prod` y las integraciones
- [ ] **CORS** — orígenes explícitos en consola + el preflight OPTIONS en DevTools
- [ ] **Servicios desplegados** — la EC2 con los cuatro contenedores corriendo
- [ ] **Red** — los servicios en subred privada, sin IP pública
- [ ] **Frontend desplegado** — la URL de CloudFront funcionando
- [ ] **CI/CD** — historial de workflows verdes
