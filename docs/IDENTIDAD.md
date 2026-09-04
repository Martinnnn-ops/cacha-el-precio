# La capa de identidad

> **Por qué Cognito y no otro** → [ADR-006](adr/006-cognito-como-idaas.md).
> **En qué orden se despliega** → [DESPLIEGUE.md](DESPLIEGUE.md).
> Este documento es **cómo funciona, cómo se replica y cómo se defiende**.

Es la parte más importante del EP1: la rúbrica reparte **60% al frontend con el flujo OIDC
completo** y **40% al BFF validando el token**. Todo lo demás del sistema —el scraping, el
matching, la comparación de precios— no tiene indicador propio en esa rúbrica.

---

## 1. Qué existe hoy

Creado el **30-08-2026** con `tools/crear-cognito.sh` y verificado end-to-end.

| Pieza | Para qué |
|---|---|
| **User Pool** `cacha-el-precio` | El directorio de usuarios. Se entra con el correo |
| **Dominio del Hosted UI** | La pantalla de login que hospeda AWS. Sin esto no hay flujo de código |
| **Resource server** `https://api.cachaelprecio.cl` | Define permisos propios de *nuestra* API. Tiene el scope `ingesta` |
| **App client `frontend`** | Sin secreto. Authorization Code + PKCE. Es el que usa el React |
| **App client `scraper`** | Con secreto. `client_credentials`. Es el que usa el proceso que captura precios |
| **App client `pruebas`** | Sin secreto, sin OAuth ni callbacks. Solo `ADMIN_USER_PASSWORD_AUTH`, para que los tests saquen un token real sin navegador |
| **Grupos `admin` y `usuario`** | Viajan dentro del token. Es lo que el BFF mira para decidir 403 |
| **Dos usuarios de prueba** | Uno por grupo, para poder demostrar el 200 y el 403 en la defensa |

Los identificadores concretos —que cambian con cada cuenta de AWS— quedan en **`cognito.env`**,
que no se versiona. Se regenera corriendo el script.

> **Por qué existe un tercer app client.** El client `frontend` solo acepta SRP, y el SRP no se
> puede hacer desde la línea de comandos: habría que calcular el `SRP_A` a mano. Sin un client
> aparte, sacar un token de usuario obliga a pasar por el navegador, y **un test no puede hacer
> eso**. Se creó uno nuevo en vez de habilitarle el flujo al `frontend` por dos razones:
> `update-user-pool-client` **reemplaza** la configuración —todo campo que no se le pase vuelve
> al valor por defecto, así que se perderían las callback URLs—, y porque lo que se defiende en
> el EP1 es que el frontend es **PKCE puro y sin secreto**; agregarle autenticación por
> contraseña debilita ese argumento. El client `pruebas` no tiene OAuth ni callbacks: no sirve
> para el flujo del navegador, solo para pedir un token con usuario y clave.

---

## 2. Cómo funciona: el camino de la persona

Es el flujo **Authorization Code con PKCE**, el estándar para aplicaciones que corren en el
navegador.

```
  Navegador                Cognito              API Gateway            BFF
      │                       │                      │                  │
      │ 1. clic en "entrar"   │                      │                  │
      │──────────────────────>│                      │                  │
      │   (con un desafío PKCE)                      │                  │
      │                       │                      │                  │
      │ 2. correo y clave     │                      │                  │
      │──────────────────────>│                      │                  │
      │                       │                      │                  │
      │ 3. vuelve con un CÓDIGO (no un token)        │                  │
      │<──────────────────────│                      │                  │
      │                       │                      │                  │
      │ 4. cambia el código por tokens               │                  │
      │   (probando que el desafío era suyo)         │                  │
      │──────────────────────>│                      │                  │
      │<──────────────────────│                      │                  │
      │   id_token · access_token · refresh_token    │                  │
      │                       │                      │                  │
      │ 5. llama a la API con el access_token        │                  │
      │─────────────────────────────────────────────>│                  │
      │                       │  valida firma con JWKS                  │
      │                       │<─────────────────────│                  │
      │                       │                      │ 6. solo si vale  │
      │                       │                      │─────────────────>│
      │                       │                      │   revalida todo  │
```

**Por qué el rodeo del paso 3.** Cognito no devuelve el token en la URL, devuelve un código de
un solo uso. Si devolviera el token, quedaría en el historial del navegador, en los logs del
servidor y en el `Referer`. El código, sin el desafío PKCE que solo tiene ese navegador, no
sirve de nada aunque alguien lo intercepte.

**Por qué el `frontend` no tiene secreto.** Un React compilado es un archivo que cualquiera
puede abrir y leer. Un secreto ahí no es un secreto. PKCE existe justamente para reemplazarlo:
el navegador inventa un valor al azar, manda su hash al pedir el código, y al canjearlo muestra
el valor original. Nadie más puede completar el canje.

---

## 3. Cómo funciona: el camino de la máquina

El scraper no es una persona: no hay a quién mostrarle una pantalla de login. Usa
**`client_credentials`**, que es "yo soy este proceso y tengo este permiso".

```bash
curl -X POST "$COGNITO_URL_LOGIN/oauth2/token" \
  -u "$COGNITO_SCRAPER_CLIENT_ID:$COGNITO_SCRAPER_CLIENT_SECRET" \
  -d "grant_type=client_credentials&scope=https://api.cachaelprecio.cl/ingesta"
```

Devuelve un `access_token` que vale una hora y trae **solo** el scope `ingesta`. Con ese token
el scraper puede publicar ofertas en `POST /ingesta` y **nada más**: no puede leer datos de
usuarios ni tocar otra ruta. Ese es el punto de tener un scope propio en vez de reutilizar el
token de una persona.

Este cliente **sí** tiene secreto, porque corre en un servidor donde se puede guardar.

---

## 4. Los tres tokens

Cognito devuelve tres cosas y confundirlas es el error clásico.

| Token | Qué dice | Para qué se usa |
|---|---|---|
| **`id_token`** | *Quién es* el usuario: correo, nombre, grupos | Que el frontend muestre "hola, Martín". **Nunca se manda a la API** |
| **`access_token`** | *Qué puede hacer*: scopes y grupos | **Este es el que va en el header `Authorization`** |
| **`refresh_token`** | Permite pedir tokens nuevos sin volver a entrar | Que la sesión no se caiga cada hora |

---

## 5. Cómo valida el BFF (el 40% de la nota)

Cinco comprobaciones, y hay que poder nombrarlas todas:

| # | Qué se comprueba | Con qué |
|---|---|---|
| 1 | **La firma** es de Cognito y nadie alteró el token | Las llaves públicas del **JWKS** |
| 2 | **`iss`** es exactamente nuestro User Pool | `COGNITO_ISSUER` |
| 3 | **La audiencia** es nuestra app | Ver el detalle de abajo |
| 4 | **`exp`** no pasó | La hora actual |
| 5 | **`cognito:groups`** trae el grupo que la ruta exige | Si no, es **403**, no 401 |

El JWKS se descarga una vez y se cachea; no se pide en cada request.

> **401 y 403 no son lo mismo, y en la defensa lo preguntan.**
> **401** = no sé quién eres (falta el token, venció o la firma no cuadra).
> **403** = sé quién eres, pero esto no te toca.

### El detalle del `aud`, medido de verdad

Pedimos un token real el 30-08 y lo decodificamos. El `access_token` de Cognito **no trae el
claim `aud`**; trae `client_id`. El que sí trae `aud` es el `id_token`.

```
scope      https://api.cachaelprecio.cl/ingesta
token_use  access
client_id  1frh7rk3d8k6pf222ts3qp8va4
iss        https://cognito-idp.us-east-1.amazonaws.com/us-east-1_cH76LiA02
aud        (no viene)
```

**Consecuencia práctica:** si el BFF se configura con una validación de audiencia estándar
—como la de casi todas las librerías por defecto— **rechaza todos los tokens buenos** y el
error dice "audience inválida", que manda a buscar en el lugar equivocado. Hay que validar
contra **`client_id`**, y de paso comprobar que **`token_use` sea `access`** para que nadie
mande un `id_token` en su lugar.

Y se valida contra una **lista** de `client_id`, no contra uno solo: los tokens de las personas
vienen del client `frontend` y los de los tests vienen del client `pruebas`. Con un solo valor
aceptado, los tests darían 401 contra nuestra propia validación. La lista la arma el script en
`COGNITO_CLIENT_IDS_VALIDOS`.

El razonamiento largo está en [`ARQUITECTURA.md` §9](ARQUITECTURA.md#9-el-detalle-del-audience-en-cognito).

---

## 6. Cómo se replica

Todo se creó por script. En una cuenta de AWS nueva —porque esta se agotó, la resetearon o nos
dieron otra— es esto:

```bash
# 1. Credenciales nuevas: Learner Lab -> Start Lab -> AWS Details -> AWS CLI: Show
#    y pegar el bloque en ~/.aws/credentials reemplazando [default]

./tools/verificar-aws-academy.sh   # ¿la cuenta nueva deja hacer todo? crea, prueba y borra
./tools/crear-cognito.sh           # levanta la identidad completa
```

No hay nada que editar: el dominio del Hosted UI se deriva del número de cuenta, así que es
único y a la vez reproducible. El script es **idempotente** — si algo ya existe lo reutiliza,
así que se puede correr las veces que haga falta.

Después de eso hay **un solo paso manual**, y es el que hunde entregas: agregar la URL de
CloudFront a las **callback URLs** del app client (paso 7 de [DESPLIEGUE.md](DESPLIEGUE.md)).
El login sigue funcionando en local y muere en producción sin decir por qué.

Para empezar de cero: `./tools/crear-cognito.sh --borrar`.

---

## 7. Cómo se defiende

Las preguntas que caen, con la respuesta corta.

**¿Por qué un IDaaS y no un login propio con usuarios en la base?**
Porque la gestión de identidad es un problema resuelto y hacerlo mal es caro: hashing, recuperación
de clave, bloqueo por intentos, verificación de correo, MFA. Delegarlo deja el tiempo del equipo
en el problema que sí es nuestro, que es comparar precios.

**¿Por qué Cognito y no Auth0?**
Está dentro de AWS: el JWT Authorizer de API Gateway se conecta al User Pool eligiéndolo de una
lista, mientras que con un proveedor externo hay que configurar issuer y JWKS a mano. La
integración IDaaS ↔ API Manager es justamente lo que evalúa el EP1. (Detalle en el ADR-006.)

**Si API Gateway ya validó el token, ¿por qué el BFF lo valida de nuevo?**
Porque la validación en el borde protege la *puerta*, no el *servicio*. Si mañana algo llega al
BFF por otro camino —una regla mal puesta, una prueba, un servicio interno— el BFF sigue siendo
seguro por sí solo. Defensa en profundidad: ninguna capa asume que la de arriba hizo su trabajo.

**¿Por qué el frontend no tiene secreto de cliente?**
Porque un SPA no puede guardar secretos: el bundle es público. Se usa PKCE, que resuelve el
mismo problema sin secreto.

**¿Por qué el scraper usa otro flujo?**
Porque no hay usuario detrás. `client_credentials` es para procesos, y su token trae solo el
scope `ingesta`: aunque se filtrara, no sirve para leer datos de personas.

**¿Cómo demuestran que la seguridad funciona?**
Con la colección de Postman del paso 8 de DESPLIEGUE.md: **200** con token válido del grupo
correcto, **401** sin token o con la firma alterada, **403** con un token válido del grupo
equivocado. Por eso existen los dos usuarios de prueba.

**¿Qué pasa si se cae Cognito?**
Nadie puede iniciar sesión, pero **la búsqueda de precios sigue funcionando**: es pública por
diseño y no pide token. Se degrada, no se cae.
