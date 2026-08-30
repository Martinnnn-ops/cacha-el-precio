# Despliegue en AWS · runbook del EP1

> **Fecha que manda: domingo 6 de septiembre.** El sistema tiene que estar andando en
> internet ese día, no el 13. El 13 es la entrega del encargo; el 10 es el code freeze.

El *qué* y el *por qué* están en [`ARQUITECTURA.md`](ARQUITECTURA.md) §11 y en el
[ADR-008](adr/008-ec2-docker-compose.md). Este documento es el *en qué orden* y el *con qué
comando*. Cada paso depende del anterior: saltarse uno hace fallar el siguiente de una forma
que no dice por qué.

---

## 0. Antes de tocar nada: cómo es el Learner Lab

Esto no es una cuenta de AWS normal y las diferencias muerden justo en el despliegue.

| Restricción | Qué significa para nosotros |
|---|---|
| **La sesión caduca** (el reloj del laboratorio llega a 0:00) | Las credenciales del CLI mueren y **las EC2 se detienen solas**. Los datos y la configuración **sí** sobreviven: al reabrir el lab se vuelve a arrancar la instancia |
| **La IP pública cambia** al reiniciar la EC2 | Por eso el BFF nunca se referencia por IP. Si se necesita una IP fija, va una **Elastic IP** |
| **Región fija** | Todo en `us-east-1`. Un recurso creado en otra región no se ve desde la consola del lab |
| **No se pueden crear roles de IAM** | Se usa **`LabRole`**, el rol que ya viene. Donde un tutorial diga "crea un rol", se elige `LabRole` |
| **Hay servicios bloqueados** | Cuáles exactamente lo dice `tools/verificar-aws-academy.sh`. **Cognito es el que hay que confirmar antes que nada** |

**Credenciales, cada vez que se abre el laboratorio:**
`Start Lab` → esperar el punto verde → `AWS Details` → `AWS CLI: Show` → copiar el bloque
completo y pegarlo en `~/.aws/credentials` reemplazando el perfil `[default]`.
Se comprueba con `aws sts get-caller-identity`.

---

## 1. Cognito User Pool

Todo lo demás apunta acá, así que va primero.

- App client con **Authorization Code + PKCE** (sin secreto para el frontend).
- **Hosted UI** con dominio propio del pool: sin dominio no hay pantalla de login.
- **Google federado** como proveedor de identidad (GitHub no: es OAuth2, no OIDC).
- Grupos **`admin`** y **`usuario`**, más el resource server con el scope de ingesta que usa
  el scraper para `POST /ingesta`.
- Dos o tres **usuarios de prueba**, uno por grupo, para demostrar el 403 en la presentación.

> ✅ **Confirmado el 30-08-2026: Cognito se puede en esta cuenta.** Se sondearon User Pool,
> Hosted UI, resource server con scope propio, app client OIDC y el JWT Authorizer de API
> Gateway apuntando al pool — todo pasó. Evidencia en `docs/evidencia/` y razonamiento en el
> [ADR-006](adr/006-cognito-como-idaas.md). **El plan B de Keycloak queda descartado.**

> ⚠️ **Trampa de `aws-cli v1`, que no es de AWS Academy.** La v1 interpreta cualquier parámetro
> que empiece con `http://` o `https://` como una URL de la que hay que *descargar* el valor, y
> revienta con `Could not connect to the endpoint URL`. Muerde en `--identifier` del resource
> server y en `--callback-urls` del app client. Se resuelve pasando esos comandos con
> `--cli-input-json file://...`, que funciona igual en v1 y v2.

## 2. La red

> Decisión y costos en el [ADR-015](adr/015-red-privada-con-vpc-link.md). Se crea con
> `tools/crear-red.sh`; **nada a mano en la consola**, o se pierde la capacidad de replicarla.

VPC propia, **subredes públicas y privadas en dos zonas de disponibilidad**, NAT Gateway en la
pública, ALB y security groups. Las dos zonas no son adorno: RDS las exige para crear el subnet
group.

Regla que ordena todo el diseño y que hay que poder decir en la defensa:
**nada del backend es alcanzable desde internet.** El NAT Gateway deja *salir* a las subredes
privadas (bajar imágenes de Docker, leer el JWKS de Cognito) y no deja *entrar* a nadie.

### 💸 El NAT y el ALB se apagan cuando no se usan

Son las dos únicas piezas que **cobran por hora aunque el laboratorio esté cerrado** — las EC2
el Learner Lab las apaga solas, estas no. Juntas son ~USD 48/mes; usadas solo los días de
trabajo y la demo, ~USD 17 hasta la entrega.

```bash
./tools/crear-red.sh            # levanta todo, incluidos NAT y ALB
./tools/crear-red.sh --dormir   # borra NAT y ALB, conserva VPC, subredes y RDS
```

Al recrear el ALB **cambia su DNS**, así que la integración de API Gateway se vuelve a apuntar.
Eso lo hace el mismo script; no se hace a mano.

## 3. RDS Postgres en la subred privada

Sin acceso público. Credenciales en **Secrets Manager**, nunca en el repo ni en el
`docker-compose.yml`. Va fuera del compose a propósito: si la instancia muere se lleva la base,
y **el historial de precios no se reconstruye hacia atrás**.

## 4. El BFF desplegado

La EC2 en la subred privada, con Docker y el mismo `docker-compose.yml` del desarrollo local.
El BFF revalida el token contra el **JWKS de Cognito**: firma, `iss`, `aud`, vigencia y grupo.
Eso es el 40% de la rúbrica del EP1 — no se delega en el API Gateway "porque ya validó".

## 5. API Gateway

**HTTP API** con:
- **JWT Authorizer** apuntando al User Pool del paso 1.
- **VPC Link + ALB** para alcanzar el BFF, que está en la subred privada.
- Rutas hacia el BFF.
- **CORS con orígenes explícitos**, nunca `*`.
- Stages **`dev`** y **`prod`**.

> ⚠️ **El VPC Link es la pieza que faltaba en el diseño original.** API Gateway vive *fuera* de
> la VPC: sin VPC Link no puede alcanzar nada que esté en una subred privada, por mucho que el
> diagrama los dibuje conectados. Y el VPC Link necesita un balanceador detrás — usamos **ALB**,
> porque sus health checks son HTTP y dicen qué falla.
>
> **Antes de conectar nada, mira que el target del ALB esté `healthy`.** Si está `unhealthy`,
> API Gateway responde **503** y no explica por qué. Es el error que más tiempo hace perder acá.

Este es el *API Manager* que el informe tiene que justificar. Un reverse proxy no valida JWT en
el borde ni tiene stages: por eso se cayeron Caddy y CloudFlare.

## 6. Frontend a S3 + CloudFront

React compilado son archivos estáticos. Se usa la URL que da CloudFront; **no se compra
dominio** (no aporta nada al MVP y cuesta plata).

## 7. Actualizar las redirect URIs de Cognito

Apuntarlas al dominio de CloudFront.

> ⚠️ **Este es el paso que hunde entregas.** El login sigue funcionando perfecto en local,
> muere en producción, y el error de Cognito no dice por qué. Si algo se va a olvidar, es esto.

## 8. Probar 200 / 401 / 403 contra el dominio real

Con Postman, guardado en una **colección exportada al repo**:

| Caso | Qué se manda | Qué tiene que responder |
|---|---|---|
| **200** | Token válido, grupo correcto | La respuesta del recurso |
| **401** | Sin token, o token vencido, o firma alterada | Rechazo en el borde |
| **403** | Token válido pero del grupo equivocado | Autenticado pero sin permiso |

Esa evidencia va en el informe de 5 páginas y en la presentación. Es la prueba de que la capa
de seguridad existe de verdad y no solo en el diagrama.

---

## Contra el diagrama del equipo

La capa de red del diagrama de diagrams.net se toma tal cual: estaba bien pensada y es lo que
le faltaba al repo. El resto usa otro stack del que está escrito en los documentos, y así se
resolvió cada diferencia:

| En el diagrama | En los documentos | Qué queda |
|---|---|---|
| Frontend servido desde una EC2 | S3 + CloudFront | **S3 + CloudFront.** Dedicarle una instancia a archivos estáticos es una máquina más que parchar y vigilar |
| MySQL, una por cada EC2 | Una RDS Postgres | **Postgres, y una sola.** Dos bases parten el dato en dos, y el matching usa `pg_trgm`, que no existe en MySQL |
| CloudFlare + Caddy como «API Manager» | API Gateway | **API Gateway.** Un reverse proxy no valida JWT en el borde ni tiene stages, y es la herramienta que el informe debe justificar |
| Backend en EC2 con Docker | ECS Fargate | **EC2 con Docker Compose** (ADR-008): el EP1 no reparte ni un punto por el cómputo y la fecha es el 6-sep |

---

## Qué se evalúa y qué no

La rúbrica reparte **60% al frontend con el flujo OIDC completo** y **40% al BFF validando
issuer, audience, firma, vigencia y rol**. **No hay ni un indicador sobre en qué corre el
backend** — de ahí sale la decisión de EC2, y de ahí sale también que la capa de seguridad se
construye primero y el resto después.

Fuera del EP1: Kafka (es EP5/EP6), la analítica y la calidad del matching. El scraper igual
tiene que estar capturando desde ya, porque el historial no se recupera hacia atrás.
