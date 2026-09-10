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

**Orion —** Reemplazó `catalog-service` por `product-service` porque ahora el módulo será dueño
de catálogos y productos. Dejó una estructura simple (`model`, `repository`, `service` y
`controller`), SQLite con Flyway y tres versiones reales de los listados HTTP. No se agregaron
DTO todavía; el costo de ese acoplamiento quedó anotado en ADR-016 para no olvidarnos después.

**Panditax —**

**Del equipo:**

---

## Semana 3 · 7–13 sep 2026 · Entrega EP1

**Martín —**

(10-09, madrugada) 🚀 **El sistema completo montado y medido en AWS, y el borde por fin en el
camino.** Se desplegó entero en la cuenta `116813910999`: EC2 `t3.small` con IP fija
`52.200.101.67`, los cinco contenedores arriba, 361 productos servidos y el sitio compilado en
el bucket.

**Lo que resuelve la entrega:** el frontend ya no llama a la EC2 directa sino al API Gateway, y
está verificado *dentro del bundle* —buscando la cadena `qxaa9rl4dj` en `dist/assets/`—, no
mirando la configuración. Con eso los tres indicadores del EP2 pasan de imposibles a medibles.
La evidencia, tomada **a través del borde**, en `docs/evidencia/borde-20260910-045748.log`.

La escalera de autorización queda en tres peldaños: **401** sin token, **403** con token de
usuario sin el scope `ingesta`, y **400** con token de máquina — este último es el buen
resultado, porque significa que atravesó el JWT Authorizer *y* la validación del BFF y lo único
que falló fue el cuerpo, que se mandó vacío a propósito.

🔒 **La puerta de atrás cerrada** ([ADR-026](adr/026-encabezado-secreto-del-borde.md)). El API
Gateway inyecta `X-Borde-Secreto` en sus nueve integraciones y Caddy responde 403 a quien no lo
traiga. Antes, que el tráfico pasara por el borde era una costumbre del frontend; ahora es una
propiedad del sistema.

💾 **El respaldo, después de perder datos de verdad.** Al recrear la infraestructura se
descubrió que los 221 productos y su historial habían desaparecido: la base vivía en un volumen
de Docker sobre el disco de la instancia, y terminar la EC2 lo destruye. La causa de fondo era
de diseño — `respaldar.sh` corre *dentro* de la máquina y `crear-infra.sh --borrar` corre *en el
portátil*: **el que destruye nunca llamaba al que protege**. Ahora hay dos capas: `--borrar`
respalda, se baja la copia al portátil y comprueba que no llegó vacía antes de destruir nada; y
un timer de systemd vuelca la base **cada hora** a un bucket privado aparte, con versionado.

> Por hora y no por día, y la razón es el patrón de uso: el laboratorio no está encendido 24/7.
> Con un timer diario, `Persistent=true` dispara **al arrancar** —o sea que respalda la sesión
> anterior— y no vuelve a correr en las horas que de verdad se trabaja. Todo lo hecho en la
> sesión se perdía igual.

🐛 **Siete defectos que solo aparecieron al desplegarlo**, ninguno visible leyendo el código.
Los tres primeros son el mismo patrón —el script decía «ya existe» y nunca reconciliaba—:

1. El JWT Authorizer no tenía al scraper en su audiencia: **401 con un token perfecto**, y el
   síntoma apuntaba al scope, que no tenía nada que ver.
2. El BFF repetía el fallo una capa más adentro (`COGNITO_CLIENT_IDS_VALIDOS`). Se descubrió
   porque el 401 traía `server: Kestrel`: ya no lo ponía AWS.
3. Los puertos del grupo de seguridad no se revisaban en cada corrida.
4. El swap no sobrevivía al reinicio — `swapon` sin línea en `/etc/fstab`, y desaparecía en
   silencio justo al cambiar el tipo de instancia.
5. La comprobación de salud preguntaba al puerto 80, que Caddy siempre responde con 308:
   **gritaba «no respondió» con los cinco contenedores arriba**.
6. El frontend se compilaba apuntando a la EC2 directa, esquivando el borde.
7. Un `|| npm run build` de respaldo, con las dos salidas a `/dev/null`, compilaba contra el
   user pool de otra cuenta y **subía sin un solo error**.

🖥️ **La máquina pasó a `t3.small`.** Medido con el sistema en marcha: los cinco contenedores
usan ~355 MB, así que en una `t3.micro` quedaban 225 MB libres y ya había 81 MB en swap sin
hacer nada. El que aprieta es compilar las imágenes de .NET. Son 0,0104 USD/hora más.

🌐 **Dos formas de ver el sitio sin tocar el dominio**: `localhost:5173` (el login funciona,
porque Cognito acepta `localhost` como única excepción a su regla de retornos `https`) y la URL
de sitio estático de S3, pública, donde se ve el catálogo pero **no se puede iniciar sesión**.
S3 no puede dar HTTPS por sí solo: haría falta un CDN delante, y para eso hace falta el dominio.

🛡️ **Y tres cosas más de seguridad:** límite de 50 peticiones por segundo en los dos stages del
borde —no protege datos, protege el crédito del laboratorio—; el bucket de respaldos nace
privado y **separado del bucket del sitio**, que es de lectura pública y donde los volcados
quedarían descargables por cualquiera; y versionado activado, para que un respaldo corrupto no
pueda pisar al último bueno.

⚠️ **Lo que queda pendiente y no es código:** el *client secret* de Google. Sin él, `google.env`
no existe y el login con Google no funciona en ninguna cuenta — el script ya sabe crear el
proveedor.

📄 Guía nueva para el equipo: [`MONTAR-EN-TU-CUENTA.md`](MONTAR-EN-TU-CUENTA.md), que arranca con
dos recetas copiables — una para la cuenta que administra el dominio y otra para las que no.

(10-09, cierre) 🔒 **El borde pasó de costumbre a garantía.** El API Gateway inyecta
`X-Borde-Secreto` en sus nueve integraciones y Caddy responde **403** a quien no lo traiga
([ADR-026](adr/026-encabezado-secreto-del-borde.md)). El puerto 8080 no se puede limitar por grupo
de seguridad —AWS no publica un rango fijo para las integraciones de HTTP API— así que la
alternativa era dejarlo abierto.

> 🧪 **Se probó revirtiendo la máquina al `Caddyfile` viejo a propósito** y volviendo a desplegar:
> se cerró sola. Eso demuestra algo que conviene tener presente: **la EC2 clona desde GitHub**, así
> que un cambio en el `Caddyfile` o el compose no surte efecto hasta estar en `development`. Solo
> el `.env` se copia aparte.

🔁 **Y al remontar, la base se restaura sola.** Faltaba el otro extremo del problema de los datos:
estaban respaldados, pero nadie los volvía a poner, y un sistema recién montado arranca perfecto y
responde 200 con la base vacía. Ahora `desplegar.sh` cuenta las filas y, si son cero, baja el
último respaldo del bucket y lo carga. **La condición de «cero filas» es lo que lo hace seguro**:
si no hay ni una fila no se puede pisar nada. Probado con el pipeline real sobre una base limpia:
481 productos, 481 de historial y 361 del catálogo.

🛡️ **Tres cosas más de seguridad:** límite de **50 req/s** en los dos stages —no protege datos,
protege el crédito del laboratorio—; **versionado** en el bucket de respaldos, para que un volcado
corrupto no pise al último bueno; y `pg_trgm` + `unaccent`, que destraban el emparejamiento entre
tiendas del [ADR-022](adr/022-identidad-de-producto-entre-tiendas.md) (`"Nike Air Max 90"` contra
`"NIKE AIRMAX 90 Hombre"` da **0,52** de similitud).

🔌 **AWS apagado y verificado:** 0 instancias, 0 IPs reservadas. El `--borrar` respaldó solo,
**se bajó la copia al portátil** (1325 filas) y solo entonces destruyó. Sobreviven sin costo el
user pool, la HTTP API y los dos buckets.

📌 **Lo único que sigue bloqueado no es código:** el *client secret* de Google, que tiene Panditax.
Sin `google.env` no hay IdP federado en ninguna cuenta, y la rúbrica lo pide explícitamente.

---


(07-09) 🔀 **Integradas a `development` las dos ramas del equipo** (PR #10 de Orion, PR #11 de
Panditax). Las dos traían el mismo conflicto en esta bitácora —eran anteriores a la entrada del
03-09— y se resolvió conservando las dos partes.

Se mergearon **con deuda conocida y anotada**, no porque estuvieran listas: a tres días del
freeze, integrar y corregir arriba cuesta menos que dejar dos ramas divergiendo, y el BFF
necesita un servicio real detrás que proteger.

Revisando los dos PR aparecieron **ocho decisiones de arquitectura** que se tomaron en el
camino, de las cuales **solo una está en un ADR** (la 016) y **cuatro contradicen algo que ya
está escrito y que vamos a defender oralmente**: SQLite contra el ADR-010, el scraper en Python
contra el ADR-013, Caddy contra el ADR-015, y la ingesta por HTTP directo contra el argumento de
mensajería de `ARQUITECTURA.md` §6.

> 📌 **Nota del 10-09:** de esas cuatro, **la de SQLite ya no aplica**. El PR #20 migró
> `product-service` a PostgreSQL ([ADR-025](adr/025-catalogo-multi-oferta-postgresql.md)) y no
> queda una sola línea de SQLite en el repositorio. **No hay que defenderla ni mencionarla como
> limitación:** hacerlo sería reconocer un techo de escalado que ya no tenemos. Las otras tres
> siguen en pie. Eso último es lo que más pesa: si en la defensa preguntan
«¿por qué mensajería y no llamadas directas?», el documento tiene tres párrafos de respuesta y
el código hace lo contrario.

🔴 **Y apareció un agujero:** `ProductoController` expone `POST`, `PUT` y `DELETE` sin ninguna
anotación de seguridad, y el `Caddyfile` publica ese servicio en internet. No hay riesgo hoy
porque nada está desplegado, pero es el commit que no puede llegar a la EC2 sin arreglarse —y
arreglarlo es exactamente el 40% del EP1 que me toca.

Todo quedó con dueño y fecha en [`INTEGRACION.md`](INTEGRACION.md).

(07-09 · noche) 🌐 **Descubierto revisando el dominio: el sistema YA está en internet.** No
estaba anotado en ninguna parte. `www.cacha-el-precio.com` sirve el frontend desde S3 detrás de
Cloudflare, y `api.cacha-el-precio.com` apunta a una EC2 en `us-east-1` con Caddy delante de
`product-service`. Hay 12 productos y 12 catálogos vivos, y el bundle del frontend trae PKCE de
verdad (`code_challenge`, `S256`, `state`, `identity_provider=Google`). **La fecha del 6-sep no se
incumplió del todo**, y eso conviene decirlo en el informe.

🔴 **Pero la escritura está abierta.** `DELETE /productos/999999999` responde **404, no 401** —lo
probé con un id inexistente para no borrar nada—, o sea que la petición llega a la lógica de
negocio sin pasar por ninguna autorización. Cualquiera puede borrar el catálogo con un `curl`.

🔴 **Y hay dos user pools de Cognito.** El frontend desplegado entra a uno distinto del que crea
`tools/crear-cognito.sh`. La causa la encontré en mi propio script: **nunca crea el IdP de
Google**, no hay un solo `create-identity-provider`. `TAREAS.md` lo tiene sin marcar desde la
Semana 1, pero `README.md` y `ARQUITECTURA.md` ya lo daban por hecho, así que quien hizo el
frontend necesitó Google, no lo encontró, y levantó un pool que sí lo tenía. Es un error mío de
documentación, no de nadie más. Se decide en la reunión de mañana.

(07-09 · noche) 🔐 **El BFF ya valida el token — el 40% del EP1 dejó de ser un `Application.java`
vacío.** Verifica la firma contra el JWKS del pool, el emisor, la vigencia, el `client_id` y que
el `token_use` sea `access`; los roles salen de `cognito:groups`.

Dos cosas necesitaron validador propio porque Cognito no se comporta como Micronaut espera. La
primera ya la sabía del 03-09: **no manda `aud` en el `access_token`**, manda `client_id`, así que
la validación estándar de audience quedó apagada a propósito y el equivalente acepta los **dos**
clients nuestros. La segunda apareció escribiendo: los tres tokens del login están firmados por el
mismo pool, así que **un `id_token` pasa firma y emisor sin problema**; se rechaza por
`token_use`, porque el `id_token` dice quién eres y no qué puedes hacer.

Los 401 y 403 salen en JSON con formato parejo — la rúbrica del EP2 pide mostrar el JSON esperado
ruta por ruta, y una respuesta vacía no es evidencia de nada. **7 tests verdes**, y los de
seguridad están escritos para fallar si la protección se apaga: es fácil creer que un servicio
está protegido porque la dependencia está en el `pom`, y que responda 200 a todo.

Falta el test con un token real (necesita credenciales del lab) y que el gateway consulte de
verdad a `product-service`.

**Orion —**

(08-09) ♻️ **Migración del núcleo de aplicación a C# y simplificación de la arquitectura.** Se
actualizó desde `development` y se reemplazó el Product Service Java por el proyecto ASP.NET Core
10. El gateway también se reescribió en ASP.NET Core, conservando validación de Cognito,
autorización por scope/grupo, aliases del frontend y respuestas 401/403 en JSON.

El contrato scraper-producto quedó alineado: `Version: 1.0`, rutas `/api/products`, identidad
`(store, externalId)` y campos de tienda, imagen, URL, descripción y estado. EF Core agregó la
migración `IntegrateScraperContract` y un índice único. Se corrigió además el cliente Python para
aceptar el `204 No Content` de las actualizaciones. Las cinco pruebas unitarias del adaptador
quedaron verdes y la solución C# compila sin advertencias.

Se eliminaron `price-service`, RabbitMQ, el agregador Maven y migraciones `catalog`/`price` sin
consumidor. PostgreSQL **no** se eliminó: el scraper todavía lo necesita para sus productos e
historial. La decisión y sus costos —HTTP sin cola durable, SQLite sin escalado horizontal y
seguimiento en memoria— están en el [ADR-020](adr/020-csharp-y-simplificacion-de-servicios.md).

Comandos principales usados para verificar:

```bash
dotnet restore CachaElPrecio.slnx
dotnet format CachaElPrecio.slnx --no-restore
dotnet build CachaElPrecio.slnx --no-restore
dotnet ef migrations has-pending-model-changes --project product-service/Product-Service.csproj
PYTHONPATH=scraper-service/src uv run --no-project --with pytest --with pydantic \
  --with pydantic-settings --with httpx --with beautifulsoup4 \
  pytest -q scraper-service/tests/unit/services/test_product_service_sync.py
```

(08-09 · catálogo multi-oferta) Product Service pasó de SQLite al esquema PostgreSQL `product`
y separó producto canónico de oferta. `canonicalKey` agrupa el mismo modelo entre tiendas y
`(store, externalId)` hace el POST idempotente; la respuesta entrega `offers[]`, imágenes por
oferta y tallas abiertas como `S`, `38` o `42.5`. La decisión y el riesgo del matching heurístico
quedaron en el ADR-025.

El scraper amplió las categorías de ropa, accesorios y baño, lee JSON-LD anidado,
`ProductGroup`, `AggregateOffer`, precios chilenos y tallas de variantes. El frontend ya traduce
varias ofertas a una sola ficha. Docker Engine rootless quedó instalado para ejecutar Postgres,
scraper y Product Service sin depender de privilegios del sistema.

(08-09 · catálogo visible) Se reemplazó en Falabella el barrido de fichas —bloqueado con `403`—
por la lectura de `__NEXT_DATA__` en listados públicos de Poleras y Zapatillas. Un barrido de
seis páginas guardó y sincronizó 357 observaciones sin fallos; después del agrupamiento canónico,
el API entregó 221 productos reales, todos con imagen. También se retiró de la base el producto
manual de prueba que apuntaba a `images.example`.

Comandos de verificación nuevos:

```bash
dotnet build CachaElPrecio.slnx --configuration Release
dotnet format CachaElPrecio.slnx --verify-no-changes
cd scraper-service && uv run pytest -m "not red"
docker compose up --build -d postgres product-service scraper-api
```

**Panditax —**

**Del equipo:**

---

### 09-09 · El frontend entra al monorepo, y el arnés de pruebas vuelve a servir

**Martín —** sesión larga, en la rama `feature/frontend-en-monorepo`. Diecisiete commits.

**Qué se hizo, en orden:**

1. **Decisión de contrato** ([ADR-021](adr/021-contrato-publico-en-el-bff.md)). El PR #1 del
   frontend lo dejó pidiendo `/api/products`, el mismo path que publica `product-service`. Eso
   convierte al gateway en intermediario transparente y manda el nombre del servicio interno al
   navegador de cada usuario. El contrato público pasa a ser el del BFF, en español.
2. **El frontend entró con `git subtree`**, sin `--squash`: los 18 commits conservan su autor
   (15 de Panditax). En un trabajo evaluado en parte por el aporte individual, `git log` es la
   evidencia de quién hizo qué.
3. **`npm run humo` pasó de 55 fallos a 0.** Ya fallaba antes —el mismo número en el repositorio
   original y en el commit anterior al PR #1—, así que llevaba tiempo sin avisar de nada.
4. **El backend C# se ejecutó por primera vez.** Compila, arranca y aplica sus migraciones solo.
   Nadie lo había corrido nunca: el PR #18 decía que Docker no estaba instalado donde se preparó.
5. **Fase 3:** el frontend conectado al backend, commit a commit, cada uno probado contra el
   sistema corriendo.
6. **Revisión de la rama entera**, que encontró ocho defectos —tres graves, los tres introducidos
   esa misma sesión— y se arreglaron todos.
7. **Fase 4:** vuelven el contador de visitas y la fecha de alta que había borrado el PR #18.

**Lo que costó, que es lo que conviene recordar:**

- **Cuatro archivos que el `.gitignore` desaparecía en silencio** (`.env.fallo`, `.env.anuncios`,
  `.env.humo` y `caddy/Caddyfile.local`). Reglas heredadas de plantillas —el `*.local` de Vite, el
  `.env.*` genérico— que en un monorepo salen más anchas de lo que quien las escribió pensaba. Una
  de esas ausencias tenía una prueba **en verde sin probar nada**: corría con los datos de ejemplo
  creyendo que hablaba con el backend.
- **SQLite no acepta `DEFAULT CURRENT_TIMESTAMP` al añadir una columna.** La migración de
  `CreatedAt` falló con «Cannot add a column with non-constant default» y el contenedor entró en
  bucle. Es el ejemplo concreto que le faltaba a la deuda «SQLite vs PostgreSQL»: allí habría
  funcionado tal cual.
- **Una fecha sin zona horaria vale menos que ninguna.** `createdAt` llegaba sin la `Z`, y un
  navegador lee una marca ISO sin zona como hora local: en Chile un producto creado hace un minuto
  parecía creado tres horas en el futuro.
- **Levantar el proyecto en local peleaba con producción.** El `Caddyfile` del repositorio es el de
  la EC2, así que Caddy pedía certificado a Let's Encrypt para el dominio real desde el portátil.
  Los intentos fallidos consumen la cuota **del dominio de verdad**.

**Lo que queda pendiente y no depende de una sola persona:**

- 🗣️ **La identidad de producto compartida entre tiendas.** Hoy la aplicación lista ofertas
  sueltas y no compara precios, que es su razón de ser. El `externalId` es el código interno de
  cada tienda, así que agrupar por él no junta nada. Es tema de equipo.
- 🗣️ **Retirar las rutas `/api/products*` del gateway.** El ADR-021 dice que no son contrato
  público y el frontend ya no las usa, pero siguen mapeadas: quitarlas toca el carril de Orion.
- 🗣️ **Reconciliar los dos User Pools de Cognito**, y conseguir el *client secret* de Google.

**Panditax —**

**Del equipo:**

### 09-09, segunda parte · revisión, Fase 4 y la arquitectura v1

**Martín —** la sesión siguió después de la entrada de arriba. Lo que pasó, en orden:

**Una revisión de la rama entera** encontró **ocho defectos, y acertó en los ocho**. Tres eran
graves y **los tres los había introducido el trabajo de esa misma sesión**: con la API caída todo
enlace válido acababa en 404; el guard dejaba la página en blanco al abrir un enlace compartido; y
el `sitemap.xml` emitía N veces la misma URL inexistente. Los tres se comprobaron **ejecutando**
—apagando el gateway, entrando en frío, generando el sitemap contra el backend—, no leyendo.

> **Lección para el informe:** ninguno de los tres se veía leyendo el código. Aparecieron al correr
> el sistema en condiciones que no son la feliz.

**Fase 4:** vuelven el contador de visitas y la fecha de alta que había borrado el PR #18. El
incremento va con `ExecuteUpdateAsync` —una sola sentencia `UPDATE … SET Visits = Visits + 1`—
para que dos visitas simultáneas no se pisen. Dos tropiezos que valen para la defensa: **SQLite no
acepta añadir una columna con un valor por defecto no constante** (`CURRENT_TIMESTAMP` falla), que
es el ejemplo concreto que le faltaba a la deuda SQLite-vs-Postgres; y **una fecha sin zona horaria
vale menos que ninguna** — llegaba sin la `Z` y el navegador la leía como hora local, así que en
Chile un producto recién creado parecía creado tres horas en el futuro.

**El PR #19 se mergeó a `development`** el 09-09 a las 04:18 UTC, **sin revisiones**. Lo mergeó su
propio autor. Queda anotado sin más: `AGENTS.md` dice que nadie mergea su propio PR, y la regla
existe para que un segundo par de ojos mire antes. A un día del freeze es entendible; conviene
recordarlo la próxima.

**Auditoría de arquitectura y definición de la v1.** El hallazgo que reordenó todo:
**el API Gateway está creado, probado y fuera del camino.** `frontend/.env.production` apunta a
`api.cacha-el-precio.com`, ese nombre resuelve a la IP de la EC2, y **ningún archivo del repo
menciona `execute-api`**. No es que se *pueda* saltar el API Manager: lo salta todo el mundo,
siempre. De él dependen tres indicadores del EP2 (20% + 13% + 7%).

**Cuatro decisiones cerradas**, dos de ellas con ADR nuevo:

| Decisión | Resultado |
|---|---|
| CDN | **Cloudflare**, no CloudFront ([ADR-023](adr/023-cloudflare-como-cdn.md)) |
| CORS | **solo en el API Manager**; dominio único pospuesto ([ADR-024](adr/024-cors-en-el-api-manager.md)) |
| Persistencia | ~~SQLite se queda hasta después del EP1~~ → **quedó obsoleta el mismo día:** el PR #20 migró a PostgreSQL ([ADR-025](adr/025-catalogo-multi-oferta-postgresql.md)) |
| CI/CD y emparejamiento | fuera del EP1 ([ADR-022](adr/022-identidad-de-producto-entre-tiendas.md)) |

Dato que nadie tenía: **nunca se comprobó si CloudFront está disponible en el Learner Lab.**
`tools/verificar-aws-academy.sh` sondea Cognito, API Gateway, EC2 e IAM, y CloudFront no aparece.
El ADR-023 deja el comando exacto para comprobarlo — y el aviso de que la lectura no prueba nada,
decide `create-distribution`.

**El backlog de la v1** quedó en 9,5 h obligatorias (identidad, meter el borde en el camino,
cerrarlo por detrás, CORS en un solo sitio, la ruta de visitas) + 11,5 h de calidad. **Todo lo
obligatorio cuesta cero dólares:** se arregla con configuración y scripts, no con infraestructura.
Tablero visual actualizado en el mismo enlace de siempre.

⚠️ **Los ADR 022, 023 y 024 quedaron fuera del merge del PR #19** — se commitearon después de subir
la rama. Están en `feature/frontend-en-monorepo`, respaldados en origin, y necesitan su propio PR.

**Lo primero al retomar:** pedirle a Panditax el *client secret* de Google y que declare la URL de
retorno de Cognito en Google Cloud. La fase 1 del plan no arranca sin eso, y es la primera porque
el API Gateway valida contra ese pool.

### 10-09 · La cadena de scripts, probada de verdad contra AWS

**Martín —** primera sesión con credenciales de AWS en la mano. El objetivo no era desplegar:
era que **el sistema se pueda rehacer entero en otra cuenta en minutos**, porque las cuentas de
laboratorio se agotan y tarde o temprano hay que saltar.

**Lo que el sondeo respondió, y llevaba semanas sin respuesta:**

| | |
|---|---|
| 🔴 **CloudFront** | **bloqueado**, ni siquiera deja *leer* (`AccessDenied` en `ListDistributions`) |
| 🟢 Elastic IP | permitida — `allocate-address --dry-run` responde *«would have succeeded»* |
| 🟢 Cognito · API Gateway · S3 · CloudWatch · Parameter Store · RDS · EC2 | permitidos |

Eso **cierra el ADR-023 con evidencia dura**: Cloudflare no era solo lo prudente. Y de paso
responde el ADR-024 — el dominio único con CloudFront no es «mal momento», es **imposible** en
cuenta de laboratorio.

🔑 **El «problema de los dos user pools» no es de pools: es de cuentas.** En la cuenta de Martín
hay un pool y **cero instancias EC2**; el frontend desplegado apunta a un pool que **no existe
ahí**. La máquina, el bucket y el pool que usa el sitio están en otra cuenta. Por eso la API se
cae cuando esa cuenta se apaga, y por eso no era «elegir qué pool gana».

**El API Gateway estaba viejo, no mal.** Tenía 7 rutas y el script declaraba 12. Las horas lo
cuentan solas: la API se creó el 07-09 a las 22:57 y el script se arregló a las **23:51, 54
minutos después**, y nunca se volvió a correr. El arreglo vivía en el código y no en AWS.
Peor: `GET /productos` y `/catalogos` estaban **detrás del token**, que es justo lo que el
ADR-007 prohíbe. El propio ADR decía que se había detectado a tiempo; nunca se corrigió en la
API desplegada. Hoy son 13 rutas, catálogo público y `AllowCredentials=false`.

📏 **Evidencia del EP2 medida a través del borde, con el backend apagado** — y eso la hace más
fuerte, no más débil: el **401** de `/seguimiento` y `/api/yo` solo puede venir del API Gateway,
porque detrás no había nadie que lo generara. Las públicas dan **503**, que prueba que pasaron el
borde sin token. El preflight devuelve cabeceras con el origen permitido y **ninguna** con uno
ajeno.

**Dos scripts nuevos** cierran la cadena: `crear-infra.sh` (máquina, IP fija, bucket) y
`desplegar.sh` (la aplicación dentro). Migrar de cuenta pasa a ser **cuatro comandos y un cambio
de DNS**, que es el único paso fuera de AWS.

**Se desplegó entero y funcionó:** cinco contenedores corriendo y el BFF respondiendo 200, más
53 archivos del sitio en el bucket. Después se apagó todo — la EC2 gasta crédito.

> **Lección, y es la misma de la Fase 3:** probar los scripts encontró **cuatro defectos que no se
> veían leyéndolos**. El perfil de instancia asociado tarde (el agente SSM solo pide credenciales
> al arrancar). `mkswap -q`, que no existe en Amazon Linux 2023 — y el script **decía que había
> funcionado igual**, porque la cadena de comandos se cortaba sin avisar. `buildx` 0.12.1 en la
> AMI cuando compose exige 0.17: comprobar que *exista* no basta, hay que mirar la **versión**. Y
> un heredoc sin comillas que expandía las variables del bloque remoto **en el portátil**.
>
> Un runbook que nunca se ejecutó no es un runbook. Estos cuatro habrían costado la tarde el día
> de la migración de verdad.

**Lo que queda, y no es técnico:** decidir **en qué cuenta vive el sistema**. Hasta entonces el
despliegue depende de que otra persona encienda su laboratorio, y eso no se arregla con código.
