# Tareas del proyecto

> **Acá está lo que hay que hacer, quién lo hace y para qué sirve.**
> Si tienes 5 minutos y no sabes qué hacer, abre este documento y busca tu semana.
>
> Última revisión: 19-08-2026

---

## Cómo usar este documento

1. **Busca tu semana** y dentro de ella tu nombre.
2. Cada bloque de tareas dice **para qué sirve** antes de la lista. Si no entiendes por qué
   estás haciendo algo, esa línea te lo explica.
3. **Marca el `[x]`** cuando termines y súbelo. Así los otros dos ven el avance sin preguntar.
4. **Anota lo que hiciste en [BITACORA.md](BITACORA.md)** al final de la semana. Son 3 líneas
   y en septiembre esa bitácora es el informe, ya escrito.

**Otros documentos:** la idea y el alcance están en [PLAN.md](PLAN.md) · el porqué técnico en
[ARQUITECTURA.md](ARQUITECTURA.md) · lo que evalúa el ramo en [EVALUACIONES.md](EVALUACIONES.md).

---

## 📋 Índice

- [El equipo: quién hace qué](#-el-equipo-quién-hace-qué)
- [Las cuatro semanas de un vistazo](#-las-cuatro-semanas-de-un-vistazo)
- [Semana 0 · 19–23 ago](#-semana-0--1923-ago--que-los-tres-entiendan-el-login)
- [Semana 1 · 24–30 ago](#-semana-1--2430-ago--el-login-de-verdad-y-el-backend-que-valida)
- [Semana 2 · 31 ago – 6 sep](#-semana-2--31-ago--6-sep--todo-a-la-nube)
- [Semana 3 · 7–13 sep](#-semana-3--713-sep--cerrar-congelar-y-entregar)
- [Después de la entrega](#-después-de-la-entrega)
- [Modo de trabajo](#-modo-de-trabajo)
- [Checklist final: lo que tiene que quedar funcionando](#-checklist-final-lo-que-tiene-que-quedar-funcionando)

---

## 👥 El equipo: quién hace qué

Tres carriles. Cada uno es **dueño** de lo suyo, pero los tres tienen que entender el proyecto
completo (en la presentación la nota es individual y la pregunta le puede tocar a cualquiera).

### 🔑 Integrante 1 — Identidad y Frontend

> **Nombre:** _por definir_

**Se lleva:** todo lo que el usuario ve y todo lo relacionado con "quién eres".
Cognito, el registro y login, el botón de Google, la pantalla, el diseño.

**Es el carril más pesado de la primera entrega** (vale el 60% de EP1).

### 🛡️ Integrante 2 — Backend y Dominio

> **Nombre:** _por definir_

**Se lleva:** el cerebro del sistema. Que el backend revise que los tokens sean válidos, que
sepa quién puede hacer qué, y toda la lógica de comparar productos y calcular descuentos.

**Es quien demuestra que la seguridad funciona** (el 40% de EP1).

### ☁️ Integrante 3 — Nube y Plataforma

> **Nombre:** _por definir_

**Se lleva:** que todo esto exista de verdad en internet. API Gateway, AWS, el despliegue
automático, y los scrapers que traen los datos.

**Sin su trabajo nadie puede presentar nada**, porque hay que mostrar el sistema andando en
la nube. En puntaje directo defiende menos, pero en horas está parejo con los otros dos.

---

## 🗓️ Las cuatro semanas de un vistazo

| Semana | Fechas | El foco | Al final de la semana tiene que existir |
|---|---|---|---|
| **0** | 19–23 ago | Que los tres entiendan el login | Cada uno con su login andando + el scraper juntando precios |
| **1** | 24–30 ago | El login de verdad y el backend que valida | Un usuario real entra, y la API responde 200 / 401 / 403 |
| **2** | 31 ago – 6 sep | Todo a la nube | El sistema funcionando en internet, no en el notebook |
| **3** | 7–13 sep | Cerrar, congelar y entregar | Código congelado el 10 · entrega el 13 |

**Regla de oro del calendario:** el **10 de septiembre no se toca más código**. Los últimos tres
días son solo para informe, ensayo y grabación. Si algo se cae el día 11 y no hay margen, no hay
entrega.

---

## 🚀 Semana 0 · 19–23 ago — Que los tres entiendan el login

### 🎯 Objetivo

Que los tres puedan explicar cómo funciona el login **porque lo hicieron con sus manos**, no
porque se lo contaron.

### ⚠️ Lo más urgente de toda la semana

> **Hay que echar a andar el scraper HOY, aunque sea un script feo en el notebook.**
>
> El historial de precios **solo existe si empieza a acumularse desde ya**. Si arranca el 22 de
> agosto, la demo tiene 3 semanas de datos reales. Si arranca en septiembre, un gráfico con tres
> puntos tristes. **Esto no se puede recuperar después.**

---

### 🔁 Los tres hacen lo mismo (cada uno en su propia cuenta, sin pisarse)

**Para qué sirve:** en la presentación la nota es individual. Si a alguien le preguntan por PKCE
y responde "eso lo hizo mi compañero", esa persona pierde puntos. Son 2 días y es la forma más
barata de que los tres entiendan de verdad cómo funciona esto.

- [ ] Crear un **User Pool de práctica propio** en Cognito (se borra la semana que viene)
- [ ] Crear tu propio **app client** con Authorization Code + PKCE y las URLs de redirección
  - ↳ *Ojo: las URLs tienen que calzar exactas, incluyendo `http://localhost:5173`*
- [ ] Levantar una **página HTML fea** con `oidc-client-ts` que haga login
  - ↳ *Sin CSS, sin diseño, sin nada. Que entre y muestre el token en pantalla*
- [ ] Abrir **DevTools** y encontrar a mano, en la URL de `/authorize`: `code_challenge`,
      `code_challenge_method=S256`, `state` y `nonce`
  - ↳ *Estos cuatro parámetros son exactamente lo que se evalúa. Verlos con tus ojos vale más
    que leer sobre ellos*
- [ ] Pegar el `access_token` en [jwt.io](https://jwt.io) y **anotar qué trae adentro**:
      `iss`, `client_id`, `scope`, `exp`, `token_use`, `cognito:groups`

---

### 🔑 Integrante 1 — además

- [ ] **Crear el repo en GitHub** con las ramas `main` y `development`
- [ ] Activar **branch protection** en las dos (que nadie pueda subir directo sin PR)
- [ ] Escribir el **`.gitignore`** (el ramo lo pide explícitamente) y una plantilla de PR

### 🛡️ Integrante 2 — además

- [ ] **Monorepo de Micronaut** con los 4 módulos: `scraper`, `catalog`, `price`, `gateway`
- [ ] **`docker-compose.yml`** con Postgres + RabbitMQ para trabajar local
  - ↳ *Para que los tres puedan levantar el proyecto con un solo comando*

### ☁️ Integrante 3 — además

- [ ] 🔥 **Scraper mínimo de Sparta corriendo con cron en tu PC, guardando JSON en S3 cada 8 horas**
  - ↳ *Sin arquitectura, sin cola, sin base de datos. Un script que baje los precios y los guarde.
    **Esta es la tarea más importante de la semana.***
- [ ] Verificar que la cuenta de **AWS Academy deja crear User Pool y API Gateway**
  - ↳ *Si no deja, hay que saberlo ahora y no en septiembre. El plan B es Keycloak*

---

### ✅ Al terminar la semana

Los tres con su login funcionando, y precios acumulándose todos los días.

---

## 🔐 Semana 1 · 24–30 ago — El login de verdad y el backend que valida

### 🎯 Objetivo

Pasar de "prototipos de práctica" al sistema real: un solo Cognito oficial, y un backend que
sepa distinguir entre *"no sé quién eres"* y *"sé quién eres pero no puedes hacer esto"*.

---

### 🔑 Integrante 1 — Identidad y Frontend

**Para qué sirve este bloque:** es el 60% de la primera entrega. Todo lo que el corrector va
a mirar primero.

- [ ] **User Pool oficial** con política de contraseñas y verificación por email
- [ ] Crear los **grupos `admin` y `usuario`**
  - ↳ *Sin grupos la API solo puede responder 200 y 401. Con grupos aparece el **403**, que es
    la diferencia entre "no estás logueado" y "estás logueado pero no te corresponde"*
- [ ] **Resource Server** con los permisos `precios:leer`, `seguimiento:escribir`, `ingesta:escribir`
- [ ] **Google como opción de login**, además del registro normal
- [ ] Crear **3 usuarios de prueba**: uno admin, uno normal, uno que entre con Google
  - ↳ *Se usan en la demo. Tenerlos listos desde ya evita improvisar el día de la presentación*
- [ ] Frontend en **React + Vite** con `react-oidc-context`: **registro**, login y logout
  - ↳ *Ojo con el **registro**: no basta con el login, hay que poder crear una cuenta nueva*
- [ ] **Guard de ruta**: si entras a una página protegida sin sesión, te manda al login
- [ ] **Interceptor**: que cada llamada a la API lleve el token automáticamente
- [ ] Mostrar en pantalla los **roles y permisos leídos del token**
  - ↳ *Suena tonto pero es un punto explícito de la rúbrica: hay que demostrar que el frontend
    sabe leer los claims*

### 🛡️ Integrante 2 — Backend y Dominio

**Para qué sirve este bloque:** es el 40% de la primera entrega, y el 20% más grande de la
presentación.

- [ ] Agregar **`micronaut-security-jwt`** a `gateway`, `catalog` y `price`
- [ ] Validar de forma explícita: **quién emitió el token, para quién es, la firma y que no
      esté vencido**
  - ↳ *En términos técnicos: `iss`, `client_id`, firma contra el JWKS, y `exp`.
    El detalle de por qué Cognito no trae `aud` está en
    [ARQUITECTURA.md §9](ARQUITECTURA.md#9-el-detalle-del-audience-en-cognito)*
- [ ] **Autorización por rol** leyendo `cognito:groups` → una ruta que le dé **403** al no-admin
- [ ] Endpoints de prueba que devuelvan **200 / 401 / 403** de forma predecible
  - ↳ *Estos tres endpoints son los que se muestran en la demo. Que sean fáciles de llamar*
- [ ] Revisar el **modelo de datos con datos reales** de Sparta y escribir las migraciones (Flyway)
- [ ] Consumidor de RabbitMQ con **DLQ** e **idempotencia**
  - ↳ *La idempotencia evita que el historial de precios se llene de duplicados si un mensaje
    llega dos veces. Es media hora de trabajo y responde una pregunta clásica de la defensa*

### ☁️ Integrante 3 — Nube y Plataforma

**Para qué sirve este bloque:** dejar de depender de que tu notebook esté prendido.

- [ ] Mover el scraper de tu PC a **Lambda + EventBridge** (3 veces al día)
  - ↳ *90 invocaciones al mes contra un free tier de 1 millón: cuesta prácticamente cero*
- [ ] Escribir el **adaptador de Hites** con Jsoup
- [ ] Crear el endpoint **`POST /ingesta`** + el app client de **client credentials** del scraper
  - ↳ *Así el scraper entra por la puerta principal como todos, con su propio token. Es lo que
    hace que el flujo "de máquina a máquina" se valide de verdad y no sea decorativo*
- [ ] **CI en GitHub Actions**: que compile y corra los tests en cada PR
- [ ] Agregar `micronaut-management` para tener **`/health` y `/metrics`**
  - ↳ *Es una sola dependencia. Si no se hace ahora, no se hace nunca*

---

### 🤝 Reunión de sincronización — sábado, 1 hora

Cada uno le explica a los otros dos lo que construyó. **Conviene grabarla**: sirve de repaso
antes de la presentación.

### ✅ Al terminar la semana

Un usuario real se registra, entra, y la API le responde 200, 401 o 403 según corresponda.

---

## ☁️ Semana 2 · 31 ago – 6 sep — Todo a la nube

### 🎯 Objetivo

Que el sistema exista en internet. **Esta es la semana crítica**: casi todos los indicadores de
la presentación piden mostrar el sistema andando en la nube, no en `localhost`.

---

### ☁️ Integrante 3 — Nube y Plataforma *(semana más cargada; los otros dos ayudan)*

- [ ] **API Gateway HTTP API** con el **JWT Authorizer** apuntando a Cognito
  - ↳ *Esto hace que un token inválido se rechace **antes** de gastar servidor. Más barato y
    más seguro*
- [ ] Mapear **todas** las rutas a los microservicios
- [ ] Configurar **CORS**: orígenes explícitos (CloudFront y `http://localhost:5173`),
      métodos y headers necesarios, **sin usar `*`**
  - ↳ *CORS es la regla del navegador que decide qué sitios pueden llamar a nuestra API.
    Poner `*` funciona pero es inseguro y resta puntos*
- [ ] Crear los **stages `dev` y `prod`** + throttling
- [ ] Desplegar todo: servicios en **ECS Fargate**, scrapers en **Lambda**, **RDS Postgres**,
      **RabbitMQ en Fargate**, secretos en **Secrets Manager**
- [ ] Frontend en **S3 + CloudFront** (usamos la URL que da CloudFront, no compramos dominio)
- [ ] **Despliegue automático**: merge a `development` → sube a `dev`; merge a `main` → sube a `prod`

### 🔑 Integrante 1 — Frontend

- [ ] **Diseño real**: paleta de colores, tipografía y distribución de la página
  - ↳ *No dejarlo para el final. Una app que se ve terminada se defiende mejor que una que
    se ve a medio hacer, aunque por dentro sean iguales*
- [ ] Buscador de modelos y ficha de producto
- [ ] Actualizar las URLs de redirección de Cognito al dominio de CloudFront
  - ↳ *Si se olvida, el login deja de funcionar en producción y no es obvio por qué*
- [ ] **Lista de seguimiento** del usuario
  - ↳ *Esta es la razón por la que el login existe: los precios son públicos, pero tu lista es tuya*

### 🛡️ Integrante 2 — Dominio

- [ ] **Matching** con `pg_trgm` + tabla de candidatos para revisión manual
- [ ] Historial y cálculo del **descuento real** (los tres números)
- [ ] Revisar **30 productos a mano** y anotar el porcentaje de aciertos
  - ↳ *Esto da un **número medido** para el informe: "24 de 30 correctos = 80%". Un resultado
    concreto vale mucho más que una descripción*
- [ ] Importar el **OpenAPI** que genera Micronaut como documentación de la API

---

### ✅ Al terminar la semana

Todo el tráfico entra por el API Gateway, con el token validado en el borde, y ningún servicio
expuesto directo a internet.

---

## 🏁 Semana 3 · 7–13 sep — Cerrar, congelar y entregar

### 🎯 Objetivo

Terminar lo que falta, **congelar el 10**, y llegar tranquilos al 13.

---

### Del 7 al 10 · los tres

- [ ] Cerrar el [checklist final](#-checklist-final-lo-que-tiene-que-quedar-funcionando):
      cada ruta probada con token, sin token y sin permiso
- [ ] **Gráfico del historial** de precios + botón "Descargar CSV"
- [ ] Test de contrato por scraper ("esta captura trajo más de N productos válidos")
      y tests del matching
- [ ] Verificar que el historial tenga **al menos 3 semanas** de datos
- [ ] Escribir los **ADR** en `adr/` — las 12 decisiones ya están listadas en
      [ARQUITECTURA.md §12](ARQUITECTURA.md#12-registro-de-decisiones-adr)
  - ↳ *Es mayormente copiar y pegar lo que ya está escrito. Y en la defensa, un documento que
    dice "consideramos X, lo descartamos por Y" demuestra criterio*

### 🧊 Del 10 al 13 · código congelado

- [ ] Informe terminado
  - ↳ *Si la bitácora se fue llenando cada semana, el informe ya está escrito en un 80%*
- [ ] **Grabar un video de respaldo de la demo completa**
  - ↳ *Las demos en vivo se caen y el wifi de la sala falla. Con el video grabado, si algo se
    cae sigues adelante y nadie lo nota*
- [ ] Ensayo cronometrado: **5 a 10 minutos**, ni uno más
- [ ] Revisar el `.gitignore` y que **no haya ningún secreto** subido al repo
- [ ] 📤 **Entregar: enlaces de GitHub a AVA + copia al correo de la docente**

---

## 📅 Después de la entrega

### Preparación de la presentación (nota individual)

- [ ] Cada uno ensaya **su parte**, y además practica **responder preguntas de las partes de
      los otros dos**
- [ ] Repasar el guion de la demo en
      [EVALUACIONES.md §3](EVALUACIONES.md#3-guion-de-la-demo-de-ep2)

### Ideas para después (no ahora)

- [ ] `alert-service` suscrito a `precio.cambiado` → avisos de restock por talla
- [ ] Nike.cl y Falabella con navegador headless
- [ ] Vestuario de marca (el matching difícil)
- [ ] Mercado Libre con OAuth

---

## 🛠️ Modo de trabajo

### Las ramas

```
feature/loquesea  ──PR──▶  development  ──PR──▶  main
                          (ambiente dev)       (producción real)
                           acá se prueba        acá va lo que ya funciona
```

- Una **rama por funcionalidad**: `feature/login-google`, `feature/scraper-hites`
- Todo PR llega primero a **`development`**, donde se prueba
- A **`main`** solo entra lo que ya funciona. **Merge a `main` = se despliega a producción**
- **Nadie mergea su propio PR.** Siempre lo revisa otro

### 📓 La bitácora — al final de cada semana, sin falta

**Cada uno escribe 3 líneas en [BITACORA.md](BITACORA.md):** qué hice, qué me costó, qué queda
pendiente.

> **Por qué importa:** el informe hay que escribirlo igual, y escribirlo de memoria en septiembre
> es diez veces más caro que anotarlo cuando pasó. Si la bitácora está al día, **el informe ya
> está escrito** y solo hay que darle formato. Son 5 minutos por semana.

También se anota ahí cuando **una decisión cambia** respecto del plan: qué se cambió y por qué.
Eso es material directo para la defensa.

### Los acuerdos

| Acuerdo | Por qué |
|---|---|
| **Cada uno commitea lo suyo** | Si uno commitea todo, en el historial parece que trabajó una sola persona. Con nota individual de por medio, eso perjudica al resto |
| **Reunión de 1 hora los sábados** | Cada uno explica lo que hizo. En la presentación le pueden preguntar cualquier cosa a cualquiera |
| **Nadie mergea su propio PR** | Además de evitar errores, el historial de PRs es la evidencia de que trabajamos los tres |
| **Cero secretos en el repo** | Van a AWS Secrets Manager. Un secreto subido a GitHub queda en el historial para siempre |

### Lo que revisa el CI automáticamente

En cada PR a `development`:

1. Que **compile** y que **pasen los tests**
2. **gitleaks** — que no se cuele ninguna contraseña ni llave
3. **Trivy** — vulnerabilidades en las imágenes de Docker
4. **Dependabot** activo para mantener las dependencias al día

---

## ✅ Checklist final: lo que tiene que quedar funcionando

Se marca a medida que avanza el proyecto. La columna "explica" es quién lo cuenta en la
presentación — **pero los tres tienen que poder responder sobre cualquier fila.**

### Identidad y frontend — 🔑 Integrante 1

- [ ] **Cognito creado** — consola con usuarios, grupos y política de contraseñas
- [ ] **Registro de usuario** — crear una cuenta nueva desde el frontend, en vivo
- [ ] **Login con Google** — entrar con una cuenta de Google
- [ ] **Authorization Code + PKCE** — DevTools mostrando `code_challenge`, `S256`, `state` y `nonce`
- [ ] **Los tres tokens** — `id_token`, `access_token` y `refresh_token` visibles y decodificados
- [ ] **Roles y permisos del token** — la pantalla muestra `cognito:groups` y `scope`
- [ ] **Guard e interceptor** — ruta protegida que redirige · el header `Authorization` en DevTools

### Seguridad y backend — 🛡️ Integrante 2

- [ ] **200 con token, 401 sin token** — probado en Postman
- [ ] **403 con usuario sin permiso** — token válido pero sin el rol necesario
- [ ] **Validación en el servicio** — el código que revisa `iss`, `client_id`, `token_use` y `exp`
- [ ] **Mensajería** — el panel de RabbitMQ con las colas y la DLQ
- [ ] **Historial de precios** — el gráfico con al menos 3 semanas de datos reales

### Nube y plataforma — ☁️ Integrante 3

- [ ] **Client credentials** — el scraper saca su token y llama a `POST /ingesta`
- [ ] **API Gateway** — consola con las rutas, los stages `dev`/`prod` y las integraciones
- [ ] **CORS** — orígenes explícitos en consola + el preflight OPTIONS en DevTools
- [ ] **Microservicios desplegados** — las tareas de Fargate corriendo
- [ ] **Frontend desplegado** — la URL de CloudFront funcionando
- [ ] **CI/CD** — historial de workflows verdes + un despliegue automático
