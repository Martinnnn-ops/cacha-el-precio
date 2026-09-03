# Bitácora del proyecto

> Tres líneas por persona por semana. No es burocracia: en septiembre **esta bitácora es el
> informe**, ya escrito. Escribir esto al final de cada semana cuesta 5 minutos y ahorra un día.
>
> Formato: qué hice · qué me costó · qué queda pendiente.

---

## Semana 0 · 19–23 ago 2026

**19-08 · Sesión de planificación (los tres)**

Se armó toda la documentación del proyecto y se creó el repositorio. Todavía no hay código.

Decisiones tomadas:

- **El scraper ingesta por el API Manager**, no directo a RabbitMQ. Antes el token de client
  credentials no se validaba en ninguna parte; ahora entra por `POST /ingesta` con su scope.
- **3 capturas diarias** en vez de una, para que el gráfico de historial tenga densidad.
- **`oferta` es estado (upsert) y `precio_historico` es serie (append)**. Estaba ambiguo y
  habría reventado en septiembre.
- **Idempotencia** con hash + `UNIQUE`, porque RabbitMQ entrega at-least-once.
- **Matching con `pg_trgm`** y umbrales 0.85 / 0.60, más tabla de candidatos para revisión manual.
- **Grupos de Cognito** además de scopes: sin roles no hay 403.
- **Code freeze el 10-sep**, tres días de margen antes de la entrega del 13.
- El repo queda en la cuenta `Martinnnn-ops` (la de los runners de Actions).

**Martín —**

**Orion —**

Se configuró el monorepo Maven con Java 25 y se agregó `catalog-service` como primer módulo
Micronaut, todavía sin lógica de negocio. Se verificó el arranque y el endpoint de salud. Queda
pendiente que el integrante implemente el primer endpoint del catálogo con acompañamiento.

Se decidió usar Semantic Versioning independiente por microservicio porque cada uno avanzará a
un ritmo distinto. `catalog-service` parte en `0.1.0`; la versión del agregador Maven no se hereda
como versión del servicio.

Se habilitó el versionado de rutas mediante headers y se crearon los primeros controladores del
catálogo junto con su DTO de respuesta. Queda pendiente incorporar pruebas HTTP antes de conectar
la capa de aplicación y la persistencia.

**Panditax —**

**Del equipo:** (decisiones tomadas, cosas que cambiaron respecto del plan)

---

## Semana 1 · 24–30 ago 2026

**Martín —** (27-08) Levanté el scraper de arranque de Sparta, que estaba pendiente desde
la semana 0. Está en `tools/scraper-rapido/` y ya corre solo: 3 capturas diarias por timer de
systemd (08:00 / 15:00 / 22:00), guardando el JSON crudo comprimido en `capturas/`. Primera
captura real el 27-08 a las 18:40: **2.088 productos, 908 zapatillas, 9.125 filas talla-stock,
142 KB, 110 segundos**. Lo que me costó fue descubrir que la query que teníamos estaba mal
(ver abajo). Queda pendiente migrarlo a Lambda y que el crudo suba a S3.

También levanté el **entorno local con Docker** (Postgres + RabbitMQ) y de paso validé los
umbrales del matching contra datos reales — los dos detalles están más abajo.

(30-08) 🟢 **Cerrado el bloqueador que llevaba una semana abierto: Cognito SÍ se puede en la
cuenta de AWS Academy.** Se sondeó con `tools/verificar-aws-academy.sh`, que crea todo lo que
el EP1 necesita y después lo borra: User Pool, dominio de Hosted UI, resource server con el
scope `ingesta`, app client OIDC con Authorization Code, grupos, HTTP API y **el JWT Authorizer
de API Gateway apuntando al pool**. También se confirmó permiso para lanzar EC2 (`--dry-run`) y
que existe `LabRole`. **El plan B de Keycloak queda descartado.** Razonamiento en el
[ADR-006](adr/006-cognito-como-idaas.md), evidencia cruda en `docs/evidencia/`.

Escribí además [`DESPLIEGUE.md`](DESPLIEGUE.md), que faltaba: los 8 pasos en orden para el
6 de septiembre y las restricciones del Learner Lab que muerden en el despliegue (la sesión
caduca y **apaga las EC2 solas**, la IP pública cambia al reiniciar, no se pueden crear roles
de IAM). El paso 7 —actualizar las redirect URIs de Cognito al dominio de CloudFront— es el que
hunde entregas: el login sigue andando en local y muere en producción sin decir por qué.

(30-08) 🔑 **Identidad levantada de verdad, no en papel.** `tools/crear-cognito.sh` deja el
User Pool completo y es **idempotente**: si algo ya existe lo reutiliza, así que se puede correr
las veces que haga falta. Crea Hosted UI, resource server con el scope `ingesta`, el app client
del frontend (sin secreto, PKCE), el del scraper (con secreto, `client_credentials`), los grupos
`admin` y `usuario`, y dos usuarios de prueba para poder mostrar el 200 y el 403 en la defensa.
Los identificadores quedan en `cognito.env`, que no se versiona porque cambia con cada cuenta.

**Verificado end-to-end, no solo creado:** el JWKS entrega 2 llaves RS256, el Hosted UI responde
200, y pedimos un `access_token` real por `client_credentials` que llegó con el scope correcto.
De paso quedó **medida** la particularidad del `aud`: el `access_token` de Cognito **no trae ese
claim**, trae `client_id`. Si el BFF usa la validación de audiencia por defecto de cualquier
librería, **rechaza todos los tokens buenos** y el mensaje de error manda a buscar al lugar
equivocado. Está escrito en [`IDENTIDAD.md`](IDENTIDAD.md) §5.

**Hueco de diseño encontrado y cerrado.** El diagrama pone el backend en subred privada, pero
API Gateway vive *fuera* de la VPC: sin **VPC Link** no llega, y el VPC Link necesita un
balanceador detrás. No estaba en ningún documento. Se decidió la arquitectura completa
([ADR-015](adr/015-red-privada-con-vpc-link.md)) con dos ajustes: **ALB en vez de NLB** (sus
health checks dicen qué falla; con NLB es un 503 mudo) y **NAT y ALB se crean y se destruyen por
script**, porque son las únicas piezas que cobran por hora aunque el laboratorio esté cerrado.

Documentos nuevos: [`IDENTIDAD.md`](IDENTIDAD.md) — cómo funciona el login, qué valida el BFF,
cómo se replica y cómo se defiende en el oral.

⚠️ **Lo que falta y es todo lo que puntúa:** el frontend **no existe** (0 líneas, y es el 60%) y
el `gateway` son 2 `.java` sin nada de seguridad (el 40%). La infraestructura no reparte puntos
por sí sola.

**Orion —**

**Panditax —**

**Del equipo:**

### 🐳 Entorno local levantado (27-08)

Ya existe `docker-compose.yml` con **Postgres 16 + RabbitMQ 3.13**. Antes no había ninguno, así
que cada uno iba a terminar armándose el suyo distinto.

Tres cosas que quedaron decididas al escribirlo:

- **El mismo archivo va a correr en la EC2** ([ADR-008](adr/008-ec2-docker-compose.md)). Todo sale
  del `.env`, así que cambiar de entorno es cambiar ese archivo, no el compose.
- **Los puertos escuchan solo en `127.0.0.1`**, no en toda la red — un Postgres de pruebas
  expuesto en el wifi de la universidad es un problema evitable.
- **Las contraseñas usan `${VAR:?}`**: si faltan, el compose no arranca. Es preferible que falle
  a que levante con una contraseña adivinable.

`infra/postgres/init/` crea solo, al primer arranque, las extensiones `pg_trgm` y `unaccent` y
los esquemas `catalog` y `price` (ADR-010).

**Lo que se decidió NO hacer:** pre-declarar los exchanges y colas con un `definitions.json`.
Habría dejado la DLQ visible en el panel desde el día uno —que es evidencia del EP2— pero
RabbitMQ obliga a declarar también el usuario, con su contraseña hasheada **dentro de un archivo
versionado**, y eso choca con el acuerdo de cero secretos en el repo. La topología la declara
Micronaut con `@RabbitClient` y `@RabbitListener`, como ya decía `ARQUITECTURA.md §6`.
Consecuencia: **las colas aparecen en el panel recién cuando los servicios se conecten.**

### ⚠️ Umbrales de matching: primero los di por buenos, y no lo son

Al levantar el entorno probé los umbrales de `PLAN.md §5` (0,85 acepta / 0,60 rechaza) con un
par de nombres que escribí a mano, dieron 0,87 y 0,57, y los di por validados. **Eso estaba
mal: un par inventado por uno mismo no valida nada.**

Al cargar los 1.496 modelos reales de la captura y consultarlos de verdad:

| Comparación | Similitud | Debería | Pasa |
|---|---|---|---|
| `574 Negra` vs `574 Negro` — **el mismo zapato** | **0,696** | aceptar | revisión manual |
| `574 Negra` vs `515 Negra` — **otro zapato** | **0,660** | rechazar | revisión manual |

El correcto queda **bajo** el 0,85 y el equivocado **sobre** el 0,60. Entre acertar y
equivocarse hay **0,04**, y con nombres reales casi todo cae en la banda gris.

**La causa:** el número del modelo (574 vs 515) es lo único que distingue dos zapatillas
completamente distintas, y el trigrama casi no lo pesa porque comparten todas las demás
palabras.

**Qué hay que hacer:** el matcher no puede ser solo trigrama sobre el nombre completo. Tiene que
**extraer el número/token de modelo y exigir coincidencia exacta**, y usar el trigrama solo para
el resto. Si no, el comparador va a emparejar zapatos distintos con total seguridad — que es
exactamente lo que el producto promete no hacer.

`unaccent` sí funciona: «Básquetbol» vs «Basquetbol» da 1,00.

### 🧱 Los 4 módulos del backend, creados (27-08)

El monorepo tenía **solo `catalog-service`**. Se agregaron los tres que faltaban —`gateway`
(el BFF), `price-service` y `scraper-service`— siguiendo exactamente la misma estructura, para
que nadie tenga que inventar andamiaje en la semana crítica.

Cada uno arranca, responde `/health` y **nada más**: la lógica se escribe encima. Los cuatro
compilan y pasan sus tests con Java 25 (`./mvnw clean test`, exit 0).

| Módulo | Puerto | Qué será |
|---|---|---|
| `gateway` | 8080 | El BFF: valida el JWT y arma las respuestas de la pantalla |
| `catalog-service` | 8081 | Modelos, variantes y matching |
| `price-service` | 8082 | Ofertas, historial y descuento real |
| `scraper-service` | 8083 | Trae los datos; el único que sale a internet |

Los puertos salen del `.env`, así que el mismo build sirve en local y en la EC2. Verificado:
`PRICE_PORT=9099` levanta el servicio en 9099. `/health` responde sin autenticación a propósito
—lo consulta el healthcheck del contenedor, que no tiene token ni debería tenerlo—.

Se dejó a propósito **sin controladores de relleno**: endpoints que no hacen nada son código que
igual hay que mantener. `/health` ya prueba que el servicio está vivo.

### 🗄️ Modelo de datos validado contra datos reales (27-08)

`PLAN.md §4` decía que el modelo era *"propuesta base"* y que había que contrastarlo con datos
reales antes de escribir migraciones. Hecho: se cargaron los **2.088 productos** de la captura
en Postgres (1.496 modelos, 7.355 variantes y ofertas) y se ejecutaron las consultas del
producto. **Cinco cosas del modelo no aguantaron.**

**1. El style code no puede ser el mecanismo de comparación.** Solo Nike lo trae extraíble
(86/89 = 96%); New Balance, Adidas, Joma, Asics y Puma dan **0%**, y Hites no publica ninguno.
Pasa a ser `NULL`able. Donde sí está hace bien su trabajo: agrupa los colores de un modelo
—verificado con las 4 versiones del Nike G.T. Hustle Academy, que comparten `FJ7791`—.

**2. La talla no es un campo simple.** Sparta usa **20 atributos distintos** de talla. Se parte
en `talla_original` + `escala` + `talla_valor`.

**3. La escala no se puede deducir del nombre del atributo.** Sparta llama a un campo
`talla_us_nb_mujer` pero le mete 37.5, 38, 40 — números **europeos**. De 3.993 variantes
rotuladas "US", **3.229 traían numeración europea**. Hay un `CHECK` de rango por escala que
impide que ese error entre en silencio.

**4. Sparta nunca devuelve productos agotados.** Los 2.088 vienen `IN_STOCK` sin excepción:
Magento los filtra. El campo `stock` no sirve. Se reemplaza por `vista_en` + `activa`, porque
**la señal de agotado es la ausencia**. Ya se ve en los datos: entre dos capturas New Balance
pasó de 857 a 856.

**5. Faltaba dónde poner lo que aún no se matchea.** Se agrega `producto_tienda`: sin ella, un
producto que llega de una tienda y todavía no se sabe a qué modelo corresponde habría que
descartarlo.

**Lo que sí resistió:** la clave del upsert. `(variante, tienda)` es única en las 9.125 filas,
sin un solo duplicado. Y la separación `oferta` = estado / `precio_historico` = serie.

Las restricciones se probaron insertando datos malos a propósito: las cuatro rechazan (talla US
con número europeo, talla ALFA con número, precio 0 —hay 3 en la captura— y style code repetido).

Migraciones en `infra/db/`, con su README.

### 👕 Alcance aclarado (27-08)

**Zapatillas es el recorte del EP1, no la ambición del proyecto.** El plan es cubrir ropa además
de calzado. Se parte por calzado porque es donde el problema se resuelve bien —hay style code de
fábrica y el mismo modelo se vende en varias tiendas—; en vestuario los nombres son genéricos y
el matching es el problema difícil. El modelo de datos ya soporta ropa sin cambios.

### 🔴 Hallazgo del 27-08 · la búsqueda de Sparta no filtra por marca

`PLAN.md §3` da por verificado que se consulta Sparta buscando por texto. **No sirve.** La
búsqueda de Magento es difusa y el término "zapatillas" domina al de marca: pedir
`"nike zapatillas"` devuelve **1126 resultados de los cuales solo 24 son Nike**, y las cuatro
marcas devolvían prácticamente los mismos productos. La primera captura trajo 4.000 productos
que en su mayoría eran ruido.

Se corrige filtrando por el atributo `gral_marca`, cuyos ids salen de `aggregations`:

```graphql
products(filter: {gral_marca: {eq: "21"}}, pageSize: 50, currentPage: 1)
```

Con el filtro puesto, el catálogo real de Sparta es este — y **contradice la elección de marcas
del MVP**:

| Marca | id | Productos | Zapatillas |
|---|---|---|---|
| New Balance | 21 | 857 | 408 |
| Adidas | 3 | 752 | 239 |
| Joma | 18644 | 267 | 138 |
| Asics | 447 | 96 | 95 |
| Nike | 23 | 89 | 24 |
| Puma | 30 | 27 | 4 |

**Sparta es una tienda de New Balance y Adidas.** Nike y Puma son marginales, y varios de los
89 productos Nike son balones y accesorios, no calzado.

Esto importa porque `PLAN.md` usa Nike como **ejemplo canónico del style code** (`HV9774`) y
declara a Sparta *fuente canónica del catálogo*. Si el catálogo canónico se arma sobre Nike,
se arma sobre 24 productos. **Decidido el 27-08, pero NO como decía acá.** Mirar solo el volumen de Sparta era el criterio
equivocado para un comparador: una marca sirve solo si está en **las dos** tiendas. La faceta de
marcas de Hites muestra que **Joma y Asics no las vende**, así que aportarían cero pares
comparables pese a tener catálogo en Sparta. Las 4 del MVP vuelven a ser **New Balance, Adidas,
Nike y Puma** — las originales del plan, ahora por un motivo medido.

Dato a favor de la tesis del proyecto: **1.277 de los 2.088 productos tienen descuento activo
declarado**, con casos de 38% — o sea que hay bastante que contrastar contra el mínimo
observado en cuanto el historial tenga fondo.

### 🟠 Hallazgo del 27-08 · el endpoint de Hites que teníamos ya no responde

`PLAN.md §3` registra `Search-UpdateGrid` como la vía para Hites, verificada el 18-08. Hoy ese
endpoint devuelve **500** para cualquier búsqueda. La home sí responde 200.

Alternativa que sí funciona, probada hoy: la página de búsqueda normal
`https://www.hites.com/search?q=<término>`, que trae la grilla con 24 productos por página y
sus `data-pid`. Sigue siendo parseo con Jsoup, así que el plan del Integrante 3 no cambia de
forma — cambia la URL. Conviene confirmarlo antes de escribir el adaptador.

### ⏱️ Sobre el historial

El scraper arrancó el **27-08**, no en la semana 0. Al code freeze del 10-09 el historial va a
tener **14 días**, no las 3 semanas que pide el checklist final de `TAREAS.md`. No es
recuperable; conviene ajustar esa casilla y decirlo en el informe en vez de que lo pregunten.

---

## Semana 2 · 31 ago – 6 sep 2026

**Martín —**

(03-09) 🐛 **El script de Cognito prometía una reproducibilidad que no tenía.** Al ir a sacar un
token de usuario para probar el BFF, el login falló con `NotAuthorizedException: Incorrect
username or password` — con los dos usuarios de prueba, y también usando el UUID en vez del
correo, así que no era un problema de alias.

La causa estaba en `crear_usuario()`: el `admin-set-user-password` vivía **dentro del `else`**,
o sea que solo se ejecutaba al crear el usuario. Pero si no se le pasa `CLAVE_PRUEBA`, el script
**genera una clave nueva en cada corrida y la escribe en `cognito.env`**. Como el script es
idempotente a propósito y se corrió varias veces, el archivo terminó declarando una clave que
nunca se le aplicó a nadie.

Es peor de lo que suena: [`IDENTIDAD.md`](IDENTIDAD.md) §6 promete que en una cuenta de AWS
nueva basta correr el script y entrar. Eso **no era cierto** desde la segunda corrida, y se
habría descubierto el día que la cuenta del Learner Lab se resetee — probablemente durante la
semana de la entrega. Arreglado: la clave y el grupo se aplican siempre, exista o no el usuario.
Verificado corriendo el script completo sin `CLAVE_PRUEBA` (el escenario que fallaba) y entrando
con la clave recién generada.

(03-09) 🧪 **App client `pruebas`, para poder testear sin navegador.** El client `frontend` solo
acepta `ALLOW_USER_SRP_AUTH`, y el SRP no se puede hacer desde la CLI: habría que calcular el
`SRP_A` a mano. Sin un client aparte, conseguir un token de usuario obliga a pasar por el Hosted
UI, y **un test automatizado no puede hacer eso**.

Se creó uno nuevo en vez de habilitarle el flujo al `frontend` por dos motivos:
`update-user-pool-client` **reemplaza** la configuración entera —todo campo que no se le pase
vuelve al valor por defecto, así que se habrían perdido las callback URLs— y porque el argumento
que se defiende en el EP1 es que el frontend es PKCE puro y sin secreto. El client `pruebas` no
tiene OAuth ni callbacks: solo sirve para pedir un token con usuario y clave.

**Consecuencia de diseño para el BFF, encontrada antes de escribir el código:** el token de ese
client trae **otro `client_id`**. Como Cognito no manda `aud` en el `access_token` y hay que
validar contra `client_id`, validar contra un solo valor haría que los tests con tokens reales
dieran **401 contra nuestra propia validación**. El script ahora emite
`COGNITO_CLIENT_IDS_VALIDOS` con los dos.

Reconfirmado de paso, con un token medido hoy: `aud` no viene, `token_use` es `access` y
`cognito:groups` trae `["admin"]` — o sea que el 403 de la demo se puede construir de verdad.

**Orion —**

**Panditax —**

**Del equipo:**

---

## Semana 3 · 7–13 sep 2026 · Entrega EP1

**Martín —**

**Orion —**

**Panditax —**

**Del equipo:**
