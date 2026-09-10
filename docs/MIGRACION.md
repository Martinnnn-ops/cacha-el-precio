# Migrar el proyecto a otra cuenta de AWS

> **Cuándo se usa esto.** Cuando la cuenta de AWS Academy se queda sin crédito, cuando la
> resetean, o el día que el proyecto se mude a una cuenta de AWS normal. En los tres casos el
> procedimiento es el mismo.
>
> **Por qué existe.** La regla del proyecto desde el 30-08 es *nada se crea a mano en la consola*.
> No es purismo: si nos dan otra cuenta, lo que no se puede improvisar es volver a levantarlo
> todo. Un recurso creado a mano es un recurso que no se sabe recrear.
>
> Última revisión: **07-09-2026**

---

## 0. Lo primero, y no es un comando

**Qué sobrevive a perder la cuenta y qué no.** Esto es lo que la gente no piensa hasta que ya
pasó.

| | Qué | ¿Sobrevive? |
|---|---|---|
| 📁 | El código, los documentos y los scripts | ✅ Están en GitHub |
| 📁 | `caddy/Caddyfile`, `docker-compose.yml`, las migraciones | ✅ En el repo |
| 📊 | El historial que captura `tools/scraper-rapido` | ✅ Vive en el notebook, no en AWS |
| 🔑 | `cognito.env` y `api-gateway.env` | ⚠️ En disco, **no** en el repo. Si se pierde el equipo, se regeneran corriendo los scripts |
| 🗄️ | **El PostgreSQL de `product-service` y del scraper** (esquemas `product` y `scraper`) | 🔴 **NO.** Un solo volumen de Docker dentro de la EC2 |
| 🖼️ | Las imágenes en S3 | 🔴 **NO.** El bucket se va con la cuenta |
| 👤 | Los usuarios registrados en Cognito | 🔴 **NO.** Los de prueba se recrean; los reales se pierden |

> 🔴 **Consecuencia directa: hoy una migración pierde los datos.** No hay respaldo de nada, y el
> historial de precios **no se recupera hacia atrás** — es una serie en el tiempo, no un cálculo.
> Antes de tocar la cuenta, hacer el respaldo del punto 1.

---

## 1. Respaldar, antes de cualquier otra cosa

Se corre **contra la EC2 que está viva**, no después de perderla.

```bash
# En la EC2, o por ssh
cd /ruta/del/proyecto

./tools/respaldar.sh                 # crea respaldos/AAAAMMDD-HHMMSS/
CON_S3=1 ./tools/respaldar.sh        # y además baja las imágenes del bucket
```

Deja una carpeta con `scraper.sql` (productos e historial), `product.db` (el catálogo) y un
`MANIFIESTO.txt` que dice qué se guardó y cuánto pesa.

**El script se verifica solo**, y esa es la parte que importa: comprueba que el volcado de
Postgres traiga filas de productos en los esquemas `scraper` y `product`, no solo la estructura. Si algo
salió vacío lo dice y termina con código distinto de cero. *La falla clásica de un respaldo es que
corre, no da error, y guarda cero bytes* — el problema se descubre el día que hay que restaurar.

Para volver atrás:

```bash
./tools/respaldar.sh --listar
./tools/respaldar.sh --restaurar respaldos/20260907-231927
```

Pide escribir `RESTAURAR` antes de sobrescribir nada.

> ⚠️ **Guarda la carpeta FUERA de la máquina y fuera de la cuenta de AWS.** El repo la ignora a
> propósito: son datos, no código.

---

> 🪤 **Una trampa que muerde sin migrar nada, encontrada el 07-09 probando esto.**
> Los archivos de `infra/postgres/init/` **solo se ejecutan cuando el volumen de Postgres se crea
> vacío**. `02-scraper-schema.sql` se agregó el 07-09, pero los volúmenes del equipo son del
> 27-08: en cualquier equipo que ya tuviera el stack levantado, **el esquema `scraper` nunca se
> creó**, y el scraper de Python falla al arrancar contra una base que no tiene sus tablas.
>
> El arreglo, sin perder los datos que ya haya:
>
> ```bash
> docker compose exec -T postgres psql -U "$DB_USER" -d "$DB_NAME" -v ON_ERROR_STOP=1 \
>   < infra/postgres/init/02-scraper-schema.sql
> ```
>
> `docker compose down -v` también lo arregla, pero **borra todos los datos**. No es la salida.

---

## 2. Comprobar que la cuenta nueva sirve

```bash
# Credenciales nuevas en ~/.aws/credentials
#   Learner Lab → Start Lab → AWS Details → AWS CLI: Show
aws sts get-caller-identity          # confirma que responde

./tools/verificar-aws-academy.sh     # crea, prueba y borra
```

Ese script comprueba lo que el EP1 necesita y **limpia lo que crea**: user pool con Hosted UI,
resource server con scope propio, app client OIDC, grupos, HTTP API con JWT Authorizer, permiso
para lanzar EC2, y que exista `LabRole`.

Si algo de eso falla, **para acá**: hay que saberlo antes de construir encima, no después.

---

## 3. La identidad

```bash
./tools/crear-cognito.sh
```

Idempotente. Levanta el user pool, el Hosted UI, el resource server con el scope `ingesta`, el
app client del frontend (sin secreto, PKCE), el del scraper (client credentials), el de pruebas,
los grupos `admin` y `usuario`, y dos usuarios de prueba.

**No hay nada que editar**: el dominio del Hosted UI se deriva del número de cuenta, así que en
otra cuenta sale otro nombre solo. Los identificadores quedan en `cognito.env`, que no se
versiona porque cambia con cada cuenta.

> 🔴 **Lo que este script NO hace todavía: crear el IdP de Google.** No hay ningún
> `create-identity-provider` en él, y ese hueco es la causa de que existan dos user pools — ver
> [`INTEGRACION.md` §0.2](INTEGRACION.md). Mientras no se agregue, en una cuenta nueva el login
> con Google **no va a funcionar**, y hay que crearlo a mano en la consola. Es lo primero que
> habría que arreglar de este documento.

---

## 4. El cómputo

🟢 **Desde el 10-09 esto está en un script, y se probó de verdad** — no está escrito de memoria.

```bash
./tools/crear-infra.sh
```

Crea, y reutiliza lo que ya exista:

| | Qué | Por qué así |
|---|---|---|
| 1 | Grupo de seguridad con 22, 80 y 443 | Caddy necesita 80 y 443 para resolver el certificado |
| 2 | Par de llaves | Usa la `vockey` del laboratorio si está; si no, crea una y la guarda fuera del repo |
| 3 | EC2 con **Docker y compose ya instalados** | La AMI se pide por su parámetro público, no por un id escrito a mano: los ids cambian por región y con cada versión, y es lo primero que se pudre en un script |
| 4 | **Elastic IP, asociada sola** | Sin esto la dirección cambia en cada reinicio del laboratorio |
| 5 | Bucket privado para el sitio | Cloudflare va delante y pone el HTTPS |

Deja los datos en `infra.env` (fuera del repo) y te imprime lo que falta.

**Lo que el script NO hace, y lo dice al terminar:**

```bash
# a. Copiar el .env y levantar la aplicación
scp -i <llave>.pem .env ec2-user@<IP>:/opt/cacha-el-precio/
ssh -i <llave>.pem ec2-user@<IP>
cd /opt/cacha-el-precio && git clone <repo> . && docker compose up -d

# b. Comprobar que el arranque dejó Docker listo
ssh -i <llave>.pem ec2-user@<IP> 'docker --version && docker compose version'
```

No despliega la aplicación porque eso necesita el `.env` con secretos, que no viaja en el
repositorio. Y no toca el DNS porque Cloudflare está fuera de AWS.

> **Probado el 10-09 en la cuenta `116813910999`:** instancia `t3.micro` corriendo, Elastic IP
> asociada, bucket creado y puerto 22 respondiendo. El script tardó menos de dos minutos.

> El [ADR-015](adr/015-red-privada-con-vpc-link.md) describe el diseño correcto —VPC propia,
> subredes privadas en dos zonas, NAT y ALB— y promete un `tools/crear-red.sh` que **no existe**.
> Lo que hay desplegado es la opción B de ese mismo ADR: EC2 con IP pública en la VPC por
> defecto, porque el NAT y el balanceador cobran por hora. Está anotado en
> [`DESPLIEGUE.md`](DESPLIEGUE.md).

---

## 5. El API Manager

```bash
./tools/crear-api-gateway.sh
```

Idempotente, con modo `--borrar`. Crea la HTTP API, el JWT Authorizer apuntando al user pool que
acaba de crear el paso 3, las rutas con su nivel de acceso, CORS de orígenes explícitos y los
stages `dev` y `prod`. Deja los datos en `api-gateway.env`.

Lee el emisor y los client id de `cognito.env`, así que **el orden importa**: primero el 3, después
este.

Variables si el backend cambia de dirección:

```bash
BACKEND_URL=https://api.tu-dominio.com ./tools/crear-api-gateway.sh
ORIGEN_WEB=https://www.tu-dominio.com  ./tools/crear-api-gateway.sh
```

Se comprueba solo con las llamadas que el propio script imprime al terminar. La evidencia de la
corrida del 07-09 está en [`evidencia/`](evidencia/) y sirve de referencia de qué debe dar cada
ruta.

---

## 6. El frontend

A mano hoy:

1. Crear un bucket de S3 y habilitarlo como sitio estático.
2. En el repo del frontend, apuntar `VITE_API_BASE_URL` a la API nueva.
3. **Actualizar el user pool y el client id de Cognito** con los que salieron del paso 3.
4. `npm run build` y `npm run desplegar` (el script del frontend sube a S3 e invalida la caché).
5. Poner el CDN por delante (Cloudflare hoy; CloudFront si se prefiere quedarse dentro de AWS).

---

## 7. Google y las URL de retorno

🟢 **Desde el 10-09 esto ya no es un paso suelto: lo hace `crear-cognito.sh`.**

### Las dos listas, que se confunden

Son dos cosas distintas y fallan distinto:

| Lista | Dónde vive | Quién la usa |
|---|---|---|
| **Retornos del app client** | Cognito | A dónde vuelve el usuario **a tu sitio** |
| **Retornos autorizados** | Google Cloud Console | A dónde vuelve Google **a Cognito** |

La primera la declara el script con todas sus direcciones de una vez —`localhost` y producción—,
así que ya no hay que volver a tocarla al desplegar.

### La segunda es el truco de la migración

Google siempre devuelve a la misma forma de dirección:

```
https://<dominio-de-cognito>/oauth2/idpresponse
```

Y el dominio lo arma el script como `cacha-el-precio-<id-de-cuenta>`, o sea que **es predecible
sin haber creado nada todavía**. Como un cliente de Google acepta **varias** direcciones de
retorno, se declaran **las tres de una vez**:

```
https://cacha-el-precio-<CUENTA-1>.auth.us-east-1.amazoncognito.com/oauth2/idpresponse
https://cacha-el-precio-<CUENTA-2>.auth.us-east-1.amazoncognito.com/oauth2/idpresponse
https://cacha-el-precio-<CUENTA-3>.auth.us-east-1.amazoncognito.com/oauth2/idpresponse
```

**Hecho eso una vez, el día de la migración no se entra a Google.** Es lo que convierte el salto
de cuenta en minutos en vez de una tarde.

> ⚠️ Solo funciona si **las tres cuentas usan el script**. Un pool creado a mano recibe un dominio
> automático (`us-east-1xxxxxxx`) que nadie puede predecir, y ahí el truco se cae. Fue justamente
> lo que pasó: el pool que usa el sitio no se creó con el script.

### Las credenciales

Van en `google.env`, en la raíz, **fuera del repositorio**:

```bash
GOOGLE_CLIENT_ID=...
GOOGLE_CLIENT_SECRET=...
```

Si no están, el script **avisa y sigue sin Google** en vez de fallar: todo lo demás se monta igual
y Google se agrega después volviendo a correrlo.

---

## Resumen honesto: qué está automatizado y qué no

| Pieza | Estado |
|---|---|
| Comprobar la cuenta | ✅ `tools/verificar-aws-academy.sh` |
| **EC2, grupo de seguridad, llaves** | ✅ `tools/crear-infra.sh` · **nuevo el 10-09** |
| **Elastic IP, asociada sola** | ✅ `tools/crear-infra.sh` |
| **Bucket del sitio** | ✅ `tools/crear-infra.sh` |
| Identidad (Cognito) | ✅ `tools/crear-cognito.sh` |
| **Proveedor de Google** | ✅ `tools/crear-cognito.sh` · **arreglado el 10-09** |
| **URL de retorno de producción** | ✅ `tools/crear-cognito.sh` |
| API Manager | ✅ `tools/crear-api-gateway.sh` |
| Respaldo y restauración | ✅ `tools/respaldar.sh`, probado de punta a punta |
| Desplegar el compose en la EC2 | 🔴 a mano — necesita el `.env` con secretos |
| Compilar y subir el frontend | 🔴 dos comandos, ver el paso 6 |
| **DNS en Cloudflare** | 🔴 a mano — está fuera de AWS |
| Red privada (VPC, NAT) | 🔴 `tools/crear-red.sh` está prometido y **no existe**. Decidido que no entra: el NAT cobra por hora |

**Nueve de trece, y las cuatro que faltan son cortas.** Lo que queda a mano es: copiar un archivo
de secretos, dos comandos de compilación, y **un cambio de DNS**, que es el único paso que cruza
fuera de AWS.

### La migración completa, en orden

```bash
# En la cuenta nueva, con sus credenciales cargadas:
./tools/crear-infra.sh          # máquina + IP fija + bucket
./tools/crear-cognito.sh        # identidad + Google
./tools/crear-api-gateway.sh    # el borde

# Después, a mano:
#  1. copiar .env a la máquina y  docker compose up -d
#  2. npm run build  +  aws s3 sync dist/ s3://<bucket>/ --delete
#  3. en Cloudflare: 'api' → la IP nueva,  'www' → el bucket nuevo
```

**El dominio y el cliente de Google no se tocan.** Solo cambia a dónde apuntan.

### Lo siguiente, en orden de lo que más duele

1. **El IdP de Google en `crear-cognito.sh`.** Es el hueco que ya causó un problema real.
2. **`tools/crear-ec2.sh`**, aunque sea la versión simple: lanzar la instancia, abrir el security
   group e instalar Docker.
3. `tools/crear-red.sh`, cuando se retome el ADR-015.

---

## Checklist de que la migración quedó bien

- [ ] `aws sts get-caller-identity` responde con la cuenta nueva
- [ ] `verificar-aws-academy.sh` pasa entero
- [ ] `cognito.env` regenerado, y el Hosted UI abre en el navegador
- [ ] Los dos usuarios de prueba entran con su clave
- [ ] `api-gateway.env` regenerado
- [ ] `GET /health` por el API Gateway → **200**
- [ ] `GET /productos` sin token → **401**
- [ ] `GET /productos` con token → **200**
- [ ] `POST /productos` con token sin scope → **403**
- [ ] El preflight de CORS devuelve el origen del sitio, no `*`
- [ ] El frontend abre y el login completa el ciclo
- [ ] Los datos restaurados: los productos aparecen y el historial tiene las fechas viejas
