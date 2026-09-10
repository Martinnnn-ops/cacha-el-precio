# Montar «Cacha el Precio» en una cuenta de AWS · paso a paso

> **Qué es esto.** La guía para levantar el sistema entero en una cuenta de AWS desde cero. Sirve
> para las tres cuentas del equipo y para el día que haya que migrar porque una se agotó.
>
> **Cuánto demora.** Unos 15 minutos, y la mayoría es esperar a que compile.
>
> **Los dos caminos.** Hay **una sola diferencia** entre montar esto en la cuenta que administra
> el dominio y montarlo en cualquier otra, y los scripts la detectan solos. Está explicada en el
> [paso 0](#0-lo-primero-el-dominio) y marcada 🅐 / 🅑 en cada paso donde importa.
>
> Última revisión: **10-09-2026**, probado de punta a punta en la cuenta `116813910999`.

---

## Índice

- [0. Lo primero: el dominio](#0-lo-primero-el-dominio)
- [1. Antes de empezar](#1-antes-de-empezar)
- [2. Los cuatro comandos](#2-los-cuatro-comandos)
- [3. Qué hace cada script](#3-qué-hace-cada-script)
- [4. Comprobar que quedó bien](#4-comprobar-que-quedó-bien)
- [5. Ver el sitio](#5-ver-el-sitio)
- [6. Apagar sin perder datos](#6-apagar-sin-perder-datos)
- [7. Si algo falla](#7-si-algo-falla)
- [Apéndice: qué se deriva solo](#apéndice-qué-se-deriva-solo-y-qué-no)

---

## 0. Lo primero: el dominio

Antes de tocar nada hay que saber en cuál de los dos casos estás, porque es lo único que cambia.

Un dominio es una guía telefónica: traduce un nombre a una dirección IP. Ese renglón se llama
**registro A**, vive en Cloudflare, y **admite una sola dirección**. No es que cada uno tenga «su»
`api.cacha-el-precio.com`: es un nombre compartido que apunta a una máquina a la vez.

```
api.cacha-el-precio.com  ─────►  una sola IP
```

De ahí sale el asunto del certificado. Caddy pide un certificado HTTPS gratis a Let's Encrypt, y
Let's Encrypt comprueba que el dominio sea tuyo **conectándose a lo que diga el DNS**. Si el DNS
apunta a otra máquina, Let's Encrypt llega allá y no a la tuya: **certificado denegado**. No es un
error de configuración, es el DNS.

### Los dos casos

| | 🅐 **Con dominio** | 🅑 **Sin dominio** |
|---|---|---|
| ¿Quién? | La cuenta cuyo registro A apunta a su propia IP | Las otras dos |
| El borde llama a | `https://api.cacha-el-precio.com` | `http://<TU-IP>:8080` |
| Salto borde → EC2 | cifrado, con certificado real | sin cifrar, **con encabezado secreto** |
| Ver el sitio | `www.cacha-el-precio.com` | la URL de S3, o `localhost` |
| Iniciar sesión | funciona en todas partes | **solo en `localhost:5173`** |
| **Qué editas** | **nada** | **nada** |

> 🔑 **Lo importante: no hay que editar ningún archivo en ninguno de los dos casos.**
> `crear-api-gateway.sh` comprueba si el dominio resuelve a *tu* IP y decide solo. El día que el
> dominio cambie de dueño, el nuevo dueño vuelve a correr el script y este detecta que ahora sí
> le toca.

### Por qué el caso 🅑 es seguro igual

El puerto 8080 está abierto a internet —el grupo de seguridad no puede limitarse a las
direcciones del API Gateway, porque AWS no publica un rango fijo—, así que **Caddy exige un
encabezado secreto** que solo el API Gateway inyecta, y responde **403** a quien no lo traiga.
Ver [ADR-026](adr/026-encabezado-secreto-del-borde.md).

Y el usuario **sigue navegando con HTTPS**, porque el certificado lo pone el API Gateway. Lo
único sin cifrar es el salto interno de AWS a tu máquina.

---

## 1. Antes de empezar

Cuatro cosas manuales. Son las únicas de toda la guía, y ninguna se puede automatizar.

### 1.1 Las credenciales del laboratorio · 🅐🅑 · **cada vez que lo enciendes**

```bash
# Learner Lab → Start Lab → AWS Details → AWS CLI: Show → copiar el bloque entero
nano ~/.aws/credentials

aws sts get-caller-identity     # tiene que responder con TU número de cuenta
```

**Por qué a mano:** son credenciales temporales que vencen en horas. No se pueden guardar en el
repositorio ni derivar de nada.

### 1.2 La llave `.pem` · 🅐🅑 · **cada vez que lo enciendes**

```bash
# Learner Lab → AWS Details → Download PEM
cp ~/Descargas/labsuser.pem ~/labsuser.pem
chmod 400 ~/labsuser.pem
```

**Por qué `400`:** significa *solo tú puedes leerlo, nadie puede escribirlo*. SSH **se niega** a
usar una llave que otros puedan leer, y el navegador la baja como `644`, que sí lo permite.

**Por qué a mano:** cada laboratorio genera su propia `vockey` y la parte privada solo se
descarga desde la consola. No es del proyecto, es de tu sesión.

### 1.3 `google.env` · 🅐🅑 · **una vez, y es compartido**

```bash
# en la raíz del repositorio. NO se sube a git.
cat > google.env <<'EOF'
GOOGLE_CLIENT_ID=...
GOOGLE_CLIENT_SECRET=...
EOF
```

**Por qué a mano:** hay **una sola aplicación registrada en Google**, con un secreto que no se
puede derivar ni regenerar por script. Los tres usan el mismo archivo; hay que pasarlo por un
medio privado, nunca por el repositorio.

Si falta, el script **avisa y sigue sin Google**. Todo lo demás se monta igual y Google se agrega
después volviendo a correrlo.

> **El truco que evita entrar a Google en cada migración.** El dominio del Hosted UI se arma como
> `cacha-el-precio-<TU-CUENTA>`, o sea que es **predecible sin haber creado nada**. Como Google
> acepta varias direcciones de retorno, se declaran **las tres de una vez** y no se vuelve a
> entrar nunca más:
>
> ```
> https://cacha-el-precio-<CUENTA-1>.auth.us-east-1.amazoncognito.com/oauth2/idpresponse
> https://cacha-el-precio-<CUENTA-2>.auth.us-east-1.amazoncognito.com/oauth2/idpresponse
> https://cacha-el-precio-<CUENTA-3>.auth.us-east-1.amazoncognito.com/oauth2/idpresponse
> ```
>
> ⚠️ Solo funciona si **las tres usan el script**. Un pool creado a mano recibe un dominio
> automático (`us-east-1xxxxxxx`) que nadie puede adivinar. Fue justo lo que pasó y lo que causó
> el lío de «los dos user pools».

### 1.4 El DNS de Cloudflare · **🅐 solamente**

Si eres el dueño del dominio, al terminar el paso 2 apuntas el registro A de `api` a tu IP nueva.
Está en el [paso 2.5](#25-solo-🅐-el-dns). **Si eres 🅑, no haces nada aquí.**

---

## 2. Los cuatro comandos

Con las credenciales cargadas y la llave en su sitio:

```bash
cd cacha-el-precio

./tools/crear-infra.sh            # máquina + IP fija + buckets      (~2 min)
./tools/crear-cognito.sh          # identidad + Google               (~1 min)
./tools/crear-api-gateway.sh      # el borde                         (~1 min)

LLAVE=~/labsuser.pem ./tools/desplegar.sh    # la aplicación y el sitio  (~8 min la 1ª vez)
```

**El orden importa.** `crear-api-gateway.sh` lee `cognito.env`; `desplegar.sh` lee los tres
archivos anteriores. Cada script deja el suyo (`infra.env`, `cognito.env`, `api-gateway.env`,
`borde.env`) y **ninguno se sube al repositorio**, porque cambian con cada cuenta.

### 2.5 Solo 🅐 · el DNS

Entre el tercer y el cuarto comando, si administras el dominio:

1. En Cloudflare, registro **A** de `api` → la IP que imprimió `crear-infra.sh`
2. Registro de `www` → el bucket del sitio
3. Vuelve a correr `./tools/crear-api-gateway.sh` — ahora detectará que el dominio es tuyo y
   cambiará el borde a `https://api.cacha-el-precio.com`

> Si lo corres **antes** de tocar el DNS, apuntará a tu IP por el puerto 8080. No está mal
> —funciona igual— pero pierdes el salto cifrado. Con volver a correrlo se arregla.

---

## 3. Qué hace cada script

### `crear-infra.sh` — el cómputo

| | Qué crea | Nota |
|---|---|---|
| 1 | Grupo de seguridad: **22, 80, 443 y 8080** | Los puertos se declaran **en cada corrida**, no solo al crear |
| 2 | Par de llaves | Usa la `vockey` del laboratorio si existe |
| 3 | EC2 **t3.small** con Docker y compose ya instalados | La AMI se pide por su parámetro público, no por un id escrito a mano |
| 4 | **Elastic IP**, asociada sola | Sin esto la dirección cambia en cada reinicio del laboratorio |
| 5 | Bucket del sitio, **público** | Lleva la web dentro |
| 6 | Bucket de respaldos, **privado y con versionado** | Separado a propósito: en el público, los volcados serían descargables |

**Por qué t3.small y no micro:** medido con el sistema en marcha, los cinco contenedores usan
~355 MB. En una `t3.micro` (1 GB) quedaban 225 MB libres y ya había 81 MB en swap sin hacer nada.
El que aprieta es compilar las imágenes de .NET. Son 0,0104 USD/hora más.

### `crear-cognito.sh` — la identidad

Un user pool, el Hosted UI, el resource server con el scope `ingesta`, **tres** app clients
(frontend, scraper, pruebas), los grupos `admin` y `usuario`, dos usuarios de prueba y —si hay
`google.env`— el proveedor de Google.

El dominio del Hosted UI **se deriva del número de cuenta**, así que en otra cuenta sale otro
nombre solo. No hay nada que editar.

### `crear-api-gateway.sh` — el borde

La HTTP API, el JWT Authorizer contra el pool del paso anterior, **13 rutas** con su nivel de
acceso, CORS de orígenes explícitos, los stages `dev` y `prod`, límite de **50 req/s** y el
**secreto del borde**.

**Aquí es donde se decide 🅐 o 🅑**, en este orden:

1. Si le pasas `BACKEND_URL=...` a mano, manda eso.
2. Si el dominio resuelve a **tu** IP → `https://api.cacha-el-precio.com`.
3. Si no → `http://<TU-IP>:8080`.

### `desplegar.sh` — la aplicación

1. Arma el `.env` del servidor desde `cognito.env`, `infra.env` y `borde.env`
2. Lo copia, clona el repositorio (rama `development`) y levanta el compose
3. Comprueba que responda por el 8080 **con el encabezado**
4. Instala el respaldo automático **cada hora** hacia el bucket privado
5. Compila el frontend con los datos de **esta** cuenta, **mira dentro del bundle** y lo sube

> ⚠️ **La máquina clona desde GitHub.** Los cambios locales al `Caddyfile` o al
> `docker-compose.yml` **no llegan** hasta que estén en `development`. Solo el `.env` se copia
> aparte.

---

## 4. Comprobar que quedó bien

```bash
source api-gateway.env
API=$API_GATEWAY_URL_PROD

curl -s -o /dev/null -w '%{http_code}\n' $API/health        # 200
curl -s -o /dev/null -w '%{http_code}\n' $API/productos     # 200  (público, ADR-007)
curl -s -o /dev/null -w '%{http_code}\n' $API/seguimiento   # 401  (el borde corta)
curl -s -o /dev/null -w '%{http_code}\n' $API/api/yo        # 401
```

Y la puerta de atrás, que debe estar cerrada:

```bash
source infra.env
curl -s -o /dev/null -w '%{http_code}\n' http://$INFRA_IP:8080/health   # 403
```

### El catálogo parte vacío

Es normal en una cuenta nueva: la base nace en cero. Se llena con el scraper:

```bash
ssh -i ~/labsuser.pem ec2-user@$INFRA_IP \
  'for T in paris hites sparta; do curl -s -X POST "http://127.0.0.1:8000/scrape/$T?limite=250"; done'
```

Tarda un rato y va entrando poco a poco. El **catálogo se repone solo**; lo que no vuelve es el
**historial de precios** de los días que nadie capturó.

---

## 5. Ver el sitio

### 🅐 Con dominio

`https://www.cacha-el-precio.com` — y el login funciona.

### 🅑 Sin dominio · dos opciones

**Para probar, incluido el login:**

```bash
cd frontend && npx vite preview --port 5173
```

→ `http://localhost:5173`. El login **sí funciona** aquí, porque `localhost` es la única
excepción de Cognito a su regla de retornos `https`.

**Para enseñárselo a alguien:** la URL de sitio estático del bucket,
`http://<bucket>.s3-website-<región>.amazonaws.com`. Se ve el catálogo completo, pero **el login
no funciona**.

> **Por qué.** Cognito solo acepta retornos que empiecen por `https://`. Y S3 **no puede dar
> HTTPS a un bucket por sí solo**: el certificado tendría que ir a nombre de una dirección que es
> de Amazon, no tuya. Para tener HTTPS hace falta un CDN delante (Cloudflare), y para el CDN hace
> falta el dominio. Es una regla que no se rodea con configuración.
>
> Lo que sí va cifrado en los dos casos son **los datos**: el API Gateway es `https://`. Lo que
> viaja sin cifrar es el HTML y el JavaScript, que son públicos de todos modos.

---

## 6. Apagar sin perder datos

```bash
./tools/crear-infra.sh --borrar
```

**Qué cobra y qué no:**

| | |
|---|---|
| 🔴 EC2 encendida | **por hora**, la uses o no |
| 🔴 Elastic IP suelta | **justo cuando NO está pegada a una máquina viva** |
| 🟢 Buckets, user pool, API Gateway | prácticamente nada con nuestro volumen |

Lo caro es exactamente lo que borra ese comando. Como recrearlo son menos de dos minutos, no
tiene sentido dejarlo encendido de un día para otro.

### Las tres capas que protegen los datos

1. **`--borrar` respalda antes de destruir.** Se conecta por SSH, corre el respaldo, **se lo baja
   al portátil**, comprueba que no llegó vacío y solo entonces borra. Si algo falla, no borra
   nada. Saltárselo exige pedirlo a mano: `--borrar --sin-respaldo`.
2. **Volcado cada hora al bucket privado**, que sobrevive a la instancia. Por hora y no por día
   porque el laboratorio no está encendido 24/7: con un timer diario, `Persistent=true` dispara
   *al arrancar* —respaldando la sesión anterior— y no vuelve a correr en las horas que de verdad
   se trabaja.
3. **Versionado en el bucket**, para que un respaldo corrupto no pise al último bueno.

> Esto se añadió después de perder **221 productos con su historial**. La causa era de diseño:
> `respaldar.sh` corre *dentro* de la EC2 y `crear-infra.sh` corre *en el portátil*, así que
> **el que destruye nunca llamaba al que protege**.

### Restaurar

```bash
./tools/respaldar.sh --listar
./tools/respaldar.sh --restaurar respaldos/20260910-080657
```

Pide escribir `RESTAURAR` antes de sobrescribir nada.

---

## 7. Si algo falla

| Síntoma | Qué pasa de verdad |
|---|---|
| `ExpiredToken` en cualquier comando | Las credenciales vencieron. Vuelve al [1.1](#11-las-credenciales-del-laboratorio--🅐🅑--cada-vez-que-lo-enciendes) |
| SSH: `UNPROTECTED PRIVATE KEY FILE` | Falta el `chmod 400` del [1.2](#12-la-llave-pem--🅐🅑--cada-vez-que-lo-enciendes) |
| `curl http://<IP>/health` da **308** | Correcto. Caddy solo atiende el dominio; usa el **8080** con el encabezado |
| `http://<IP>:8080` da **403** | Correcto también. Es la puerta cerrada: solo entra el borde |
| **Todo** da 403, incluso por el borde | Falta `BORDE_SECRETO` en la máquina, o no coincide. Corre `crear-api-gateway.sh` y luego `desplegar.sh` |
| `/productos` devuelve `[]` | Base nueva sin datos. Corre el scraper ([paso 4](#el-catálogo-parte-vacío)) |
| Las rutas dan **503** | El borde apunta a una máquina que no es la tuya. Vuelve a correr `crear-api-gateway.sh` |
| El login lleva a un Cognito ajeno | Se compiló con `frontend/.env.production`. `desplegar.sh` ahora lo detecta y **se niega a subirlo** |
| Un contenedor muere al compilar | Sin memoria. Comprueba el swap: `swapon --show` |
| El `Caddyfile` no hace efecto | La máquina clona de GitHub: tiene que estar en `development` |

---

## Apéndice: qué se deriva solo y qué no

Todo lo de arriba sale de `aws sts get-caller-identity`. Por eso no hay que editar nada.

| Cosa | Cómo se arma | Ejemplo en `116813910999` |
|---|---|---|
| Bucket del sitio | `cacha-el-precio-web-<CUENTA>` | `cacha-el-precio-web-116813910999` |
| Bucket de respaldos | `cacha-el-precio-respaldos-<CUENTA>` | `cacha-el-precio-respaldos-116813910999` |
| Dominio del Hosted UI | `cacha-el-precio-<CUENTA>` | `cacha-el-precio-116813910999` |
| AMI de la máquina | por su parámetro público, no por id | `ami-0354c98ae10b02961` |
| A dónde apunta el borde | de tu `infra.env` + el DNS | `http://52.200.101.67:8080` |
| A dónde llama el frontend | de tu `api-gateway.env` | `https://qxaa9rl4dj.execute-api…/prod` |
| El secreto del borde | generado y guardado en `borde.env` | se reutiliza entre corridas |

> **Por qué la AMI se pide por su parámetro y no por su id:** los identificadores de imagen
> cambian por región y con cada versión nueva. Un id escrito a mano funciona hoy y falla en tres
> meses sin que nadie haya tocado nada.

**Y lo que no se deriva, con su motivo:**

| Pieza | Por qué es manual |
|---|---|
| Credenciales del lab | Temporales, vencen en horas |
| La llave `.pem` | Cada laboratorio genera la suya; solo se baja de la consola |
| `google.env` | Hay **una sola** app de Google; el secreto no se deriva |
| DNS de Cloudflare | Un registro, una IP, **un solo dueño** |

---

## Por qué los tres, si con uno basta

Conviene tenerlo claro, porque es la respuesta si lo preguntan en la defensa.

Para programar el día a día **no hace falta AWS**: cada uno levanta `docker compose up` en su
notebook y trabaja en `localhost`. Eso es el 95% del tiempo. Y para que el sistema esté en línea
**basta con una cuenta**.

Esto existe porque **las cuentas de AWS Academy se acaban** — se quedan sin crédito o las
resetean. Si eso pasa a mitad de semana y el sistema vive en una sola cuenta, el equipo queda
colgado. Tener los scripts probados en las tres es **un seguro, no una obligación de tenerlo
encendido los tres.**

---

## Ver también

- [`MIGRACION.md`](MIGRACION.md) — qué sobrevive a perder una cuenta y qué no
- [`DESPLIEGUE.md`](DESPLIEGUE.md) — qué hay desplegado y por qué así
- [`IDENTIDAD.md`](IDENTIDAD.md) — cómo funcionan los tokens y los scopes
- [ADR-026](adr/026-encabezado-secreto-del-borde.md) — el encabezado del borde
