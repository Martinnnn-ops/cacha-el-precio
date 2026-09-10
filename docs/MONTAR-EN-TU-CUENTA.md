# Montar el sistema en tu propia cuenta de AWS

> **Para quién es esto.** Para Orion y Panditax. Martín ya lo tiene montado y es quien
> administra el dominio.
>
> **Cuánto demora.** Unos 15 minutos, y casi todo es esperar.
>
> Última revisión: **10-09-2026**, probado de punta a punta en la cuenta `116813910999`.

---

## 0. Antes de nada: ¿por qué querríamos los tres?

Conviene tenerlo claro, porque es la respuesta si lo preguntan en la defensa:

**Para programar el día a día no hace falta AWS.** Cada uno levanta `docker compose up` en su
notebook y trabaja en `localhost`. Eso es el 95% del tiempo.

**Para que el sistema esté en línea basta con uno.** Para el EP1 y la demo, una sola cuenta
montada alcanza.

Entonces, ¿por qué esto existe? Porque **las cuentas de AWS Academy se acaban** — se quedan sin
crédito o las resetean. Si eso pasa a mitad de semana y el sistema vive en una sola cuenta,
quedamos colgados. Tener los scripts probados en las tres cuentas es **un seguro, no una
obligación de tenerlo prendido los tres.**

Y de hecho no se puede tener los tres a la vez con el dominio. Ahí va la parte importante.

---

## 1. Lo único que de verdad cambia entre nosotros: el dominio

Un dominio es una guía telefónica: traduce un nombre a una dirección IP.

```
api.cacha-el-precio.com  ─────►  una sola IP
```

Ese renglón se llama **registro A**, vive en Cloudflare, y **admite una sola dirección**. No es
que cada uno tenga "su" `api.cacha-el-precio.com`: es un nombre compartido que apunta a una
máquina a la vez. Si tú lo cambias a tu IP, se lo quitas al otro.

De ahí sale el asunto del certificado. Caddy pide un certificado HTTPS gratis a Let's Encrypt, y
Let's Encrypt comprueba que el dominio sea tuyo **conectándose a lo que diga el DNS**. Si el DNS
apunta a la máquina de otro, Let's Encrypt llega allá y no a la tuya: **certificado denegado**.
No es un error de configuración, es el DNS.

### Cómo lo resolvimos: el borde se acomoda solo

`tools/crear-api-gateway.sh` **decide solo** a dónde apuntar, en este orden:

| | Situación | Qué usa |
|---|---|---|
| 1 | Le pasas `BACKEND_URL=...` a mano | eso, y no se discute |
| 2 | El DNS apunta a **tu** IP | `https://api.cacha-el-precio.com` — certificado real, salto cifrado |
| 3 | El DNS apunta a otra máquina | `http://<TU-IP>:8080` — el bloque sin dominio del Caddyfile |

**El caso 3 es el tuyo.** Y no es un parche: ese puerto `:8080` ya estaba en el `Caddyfile` desde
antes, justo para esto, y **también pasa por el gateway**, así que el token se valida igual. Lo
único que cambia es que el salto interno de AWS a tu máquina va sin TLS. **El usuario sigue
navegando con HTTPS**, porque el certificado lo pone el API Gateway.

> 🔑 **Consecuencia práctica: no tienes que editar ningún archivo.** Corres los tres scripts sin
> banderas y te queda todo funcionando sin dominio. El día que el dominio cambie de dueño, ese
> dueño vuelve a correr `crear-api-gateway.sh` y el script solo detecta que ahora sí le toca.

---

## 2. Lo que tienes que hacer a mano, y por qué

Son cuatro cosas. Ninguna se puede automatizar, y conviene entender el motivo de cada una.

### 2.1 Las credenciales del laboratorio · **cada vez que lo enciendes**

```bash
# Learner Lab → Start Lab → AWS Details → AWS CLI: Show → copiar el bloque entero
nano ~/.aws/credentials
aws sts get-caller-identity     # tiene que responder con TU número de cuenta
```

**Por qué a mano:** son credenciales temporales que vencen en horas. No se pueden guardar en el
repo ni derivar de nada — hay que ir a buscarlas.

### 2.2 La llave `.pem` · **cada vez que lo enciendes**

```bash
# Learner Lab → AWS Details → Download PEM
cp ~/Descargas/labsuser.pem ~/labsuser.pem
chmod 400 ~/labsuser.pem
```

**Por qué `400`:** significa *solo tú puedes leerlo, nadie puede escribirlo*. SSH **se niega** a
usar una llave que otros puedan leer, y el navegador la baja como `644`, que sí lo permite.

**Por qué a mano:** cada laboratorio genera su propia `vockey`, y la parte privada solo se puede
descargar desde la consola del lab. No es del proyecto, es de tu sesión.

### 2.3 `google.env` · **una vez, y es compartido**

```bash
# en la raíz del repo, NO se sube a git
cat > google.env <<'EOF'
GOOGLE_CLIENT_ID=...
GOOGLE_CLIENT_SECRET=...
EOF
```

**Por qué a mano:** hay **una sola aplicación registrada en Google**, con un secreto que no se
puede derivar ni regenerar por script. Los tres usamos el mismo archivo; hay que pasárselo por
un medio privado, no por el repo.

Si no está, el script **avisa y sigue sin Google**. Todo lo demás se monta igual y Google se
agrega después volviendo a correrlo.

> **El truco que hace que esto no duela.** El dominio del Hosted UI se arma como
> `cacha-el-precio-<TU-CUENTA>`, o sea que es **predecible sin haber creado nada**. Como Google
> acepta varias direcciones de retorno, se declaran **las tres de una vez** en Google Cloud
> Console y no se vuelve a entrar nunca más:
>
> ```
> https://cacha-el-precio-<CUENTA-1>.auth.us-east-1.amazoncognito.com/oauth2/idpresponse
> https://cacha-el-precio-<CUENTA-2>.auth.us-east-1.amazoncognito.com/oauth2/idpresponse
> https://cacha-el-precio-<CUENTA-3>.auth.us-east-1.amazoncognito.com/oauth2/idpresponse
> ```
>
> ⚠️ Solo funciona si **los tres usan el script**. Un pool creado a mano recibe un dominio
> automático (`us-east-1xxxxxxx`) que nadie puede adivinar, y ahí el truco se cae. Es justo lo
> que pasó y lo que causó el lío de "los dos user pools".

### 2.4 El DNS de Cloudflare · **solo el dueño del dominio**

Tú **no haces nada acá**. Es el único paso que cruza fuera de AWS y le toca a una sola persona.

---

## 3. Los tres comandos

Con las credenciales cargadas y la llave en su sitio:

```bash
cd cacha-el-precio

./tools/crear-infra.sh            # máquina + IP fija + bucket   (~2 min)
./tools/crear-cognito.sh          # identidad + Google           (~1 min)
./tools/crear-api-gateway.sh      # el borde                     (~1 min)

LLAVE=~/labsuser.pem ./tools/desplegar.sh     # la aplicación    (~8 min la 1ª vez)
```

**El orden importa:** `crear-api-gateway.sh` lee `cognito.env`, y `desplegar.sh` lee los tres.

Cada script deja un archivo `.env` con lo que creó (`infra.env`, `cognito.env`,
`api-gateway.env`). **Ninguno se sube al repo**, porque cambian con cada cuenta.

---

## 4. Lo que se deriva solo de tu número de cuenta

Esto es lo que hace que no tengas que editar nada. Todo sale de
`aws sts get-caller-identity`:

| Cosa | Cómo se arma | Ejemplo en la cuenta `116813910999` |
|---|---|---|
| Bucket del sitio | `cacha-el-precio-web-<CUENTA>` | `cacha-el-precio-web-116813910999` |
| Dominio del Hosted UI | `cacha-el-precio-<CUENTA>` | `cacha-el-precio-116813910999` |
| AMI de la máquina | por su parámetro público, no por id | `ami-0354c98ae10b02961` |
| A dónde apunta el borde | de tu `infra.env` + el DNS | `http://52.200.101.67:8080` |
| A dónde llama el frontend | de tu `api-gateway.env` | `https://qxaa9rl4dj.execute-api.../prod` |

> **Por qué la AMI se pide por su parámetro y no por su id:** los identificadores de imagen
> cambian por región y con cada versión nueva. Un id escrito a mano es lo primero que se pudre en
> un script — funciona hoy y falla en tres meses sin que nadie haya tocado nada.

---

## 5. Comprobar que quedó bien

```bash
source api-gateway.env
curl -s -o /dev/null -w '%{http_code}\n' $API_GATEWAY_URL_PROD/health        # 200
curl -s -o /dev/null -w '%{http_code}\n' $API_GATEWAY_URL_PROD/productos     # 200 (público)
curl -s -o /dev/null -w '%{http_code}\n' $API_GATEWAY_URL_PROD/seguimiento   # 401 (protegido)
curl -s -o /dev/null -w '%{http_code}\n' $API_GATEWAY_URL_PROD/api/yo        # 401 (protegido)
```

Si el catálogo sale vacío (`[]`), es normal en una cuenta nueva: **la base parte en cero**.
Se llena con el scraper:

```bash
ssh -i ~/labsuser.pem ec2-user@<TU-IP> \
  'for T in paris hites sparta; do curl -s -X POST "http://127.0.0.1:8000/scrape/$T?limite=250"; done'
```

---

## 6. Apagar cuando termines · **esto sí que importa**

```bash
./tools/crear-infra.sh --borrar
```

**Qué cobra y qué no:**

| | Cobra |
|---|---|
| 🔴 EC2 encendida | **por hora**, la uses o no |
| 🔴 Elastic IP suelta | **justo cuando NO está pegada a una máquina viva** |
| 🟢 Bucket, user pool, API Gateway | prácticamente nada con nuestro volumen |

O sea que lo caro es exactamente lo que borra ese comando, y lo gratis se queda. Como recrearlo
son **menos de dos minutos**, no tiene ningún sentido dejarlo prendido de un día para otro.

> 🔴 **Antes de borrar, respalda si hay datos que te importen.**
> ```bash
> ./tools/respaldar.sh
> ```
> El volumen de Postgres **se va con la máquina**. El catálogo lo repone el scraper, pero
> **el historial de precios no se recupera hacia atrás**: es una serie en el tiempo, no un
> cálculo. Ya nos pasó el 10-09 y perdimos los 221 productos con su historial.

---

## 7. Si algo falla

| Síntoma | Qué pasa de verdad |
|---|---|
| `ExpiredToken` en cualquier comando | Las credenciales vencieron. Vuelve al 2.1 |
| SSH dice `UNPROTECTED PRIVATE KEY FILE` | Te faltó el `chmod 400` del 2.2 |
| `curl http://<TU-IP>/health` da **308** | Es correcto. Caddy solo atiende el dominio; usa el **:8080** |
| `/productos` devuelve `[]` | Base nueva sin datos. Corre el scraper (punto 5) |
| El login manda a un Cognito que no es el tuyo | Se compiló con `frontend/.env.production`. `desplegar.sh` ahora lo detecta y **se niega a subirlo** |
| Un contenedor muere al compilar | Se quedó sin memoria. Comprueba el swap: `swapon --show` |

---

## Ver también

- [`MIGRACION.md`](MIGRACION.md) — mover el sistema entero de una cuenta a otra
- [`DESPLIEGUE.md`](DESPLIEGUE.md) — qué hay desplegado y por qué así
- [`IDENTIDAD.md`](IDENTIDAD.md) — cómo funcionan los tokens y los scopes
