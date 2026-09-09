# ADR-023 · El CDN del frontend es Cloudflare, no CloudFront

**Fecha:** 09-09-2026

**Estado:** aceptada

## Contexto

Desde el diseño inicial, todos los documentos dicen que el frontend va en **S3 + CloudFront**:
el [ADR-008](008-ec2-docker-compose.md) lo da por hecho al describir el despliegue, y
`DESPLIEGUE.md` §6 se titula literalmente *«Frontend a S3 + CloudFront»*.

**Lo desplegado es S3 detrás de Cloudflare.** El propio `DESPLIEGUE.md` lo reconoce doce líneas
más arriba de esa sección, en su tabla de estado. Nunca se escribió por qué, ni quién lo decidió,
ni si CloudFront se descartó o simplemente no se intentó.

Verificado el 09-09:

| Qué | Cómo se comprobó |
|---|---|
| `www.cacha-el-precio.com` resuelve a direcciones de **Cloudflare** | `getent hosts` |
| La cuenta es **AWS Academy Learner Lab** | `~/.aws/credentials` tiene `aws_session_token`; las credenciales son temporales |
| **Nadie ha comprobado nunca si CloudFront funciona en esta cuenta** | `tools/verificar-aws-academy.sh` sondea Cognito, API Gateway, EC2 e IAM — CloudFront no aparece |

Ese último punto es el que importa: **no sabemos si CloudFront está disponible.** El Learner Lab
bloquea servicios sin avisar y no publica la lista; la única forma de saberlo es intentar crear el
recurso, y las credenciales caducan con cada sesión del laboratorio.

## Alternativas consideradas

- **Migrar a CloudFront para el EP1.** Es lo que dicen los documentos y encaja mejor con el resto
  —todo lo demás vive en AWS—. Pero mueve **CDN, DNS, certificado y las *redirect URIs* de Cognito
  a la vez**, sobre un servicio que ni siquiera está verificado en la cuenta, y a un día del code
  freeze. Si CloudFront resulta bloqueado, se pierde la tarde y hay que volver atrás.

- **Quedarse con Cloudflare y no escribir nada.** Es lo que pasa hoy. El costo no es técnico sino
  de defensa: el informe describe una arquitectura que no es la que corre, y esa contradicción la
  encuentra cualquiera que compare el documento con el sitio.

- **Quedarse con Cloudflare y registrarlo como decisión.** Mismo sistema, pero con el porqué
  escrito y la condición para revisarlo.

## Decisión

**El CDN del frontend es Cloudflare para el EP1.** Se conserva lo que ya funciona y se registra el
motivo.

Cloudflare cubre lo que este sistema necesita de un CDN: **red de distribución, TLS gestionado,
caché de archivos estáticos y protección básica de borde**. Para un sitio que son archivos
compilados de Vite, la diferencia funcional con CloudFront es despreciable.

CloudFront tendría dos ventajas reales, y **ninguna de las dos aplica hoy**:

1. **Origin Access Control**, que permite dejar el bucket de S3 completamente privado. Es una
   mejora de seguridad concreta, y entra en la lista de después del EP1.
2. **Repartir por ruta desde un solo dominio** —`/*` al sitio, `/api/*` a la API—, que es lo que
   haría desaparecer el CORS. Esa decisión se trata aparte, en el [ADR-024](024-cors-en-el-api-manager.md).

### Cómo comprobar si CloudFront está disponible

Cuando haya sesión del laboratorio abierta:

```bash
# 1 · lectura: casi siempre pasa, aunque el servicio esté restringido
aws cloudfront list-distributions --max-items 1

# 2 · escritura: este es el que decide
aws cloudfront create-distribution --distribution-config file://prueba-cf.json
```

Si el paso 2 responde `AccessDenied`, `UnauthorizedOperation` u `OptInRequired`, está bloqueado.
**El paso 1 solo no prueba nada.** Conviene añadir esta comprobación a
`tools/verificar-aws-academy.sh`, que hoy no la hace.

## Consecuencias

### Positivas

- no se toca lo que funciona en la semana de la entrega;
- `DESPLIEGUE.md` y `ARQUITECTURA.md` pasan a describir el sistema real;
- el CDN deja de depender del crédito de una cuenta de laboratorio: Cloudflare está fuera de AWS,
  así que **sobrevive a que una cuenta se agote**, que es justo el riesgo del proyecto;
- una pieza menos que replicar en cada una de las tres cuentas.

### Costos y riesgos

- **una dependencia fuera de AWS**, con su propia cuenta y sus propias credenciales, que hay que
  contar entre lo que se necesita para levantar el sistema desde cero;
- el bucket de S3 **queda accesible directamente**, sin el Origin Access Control que daría
  CloudFront: quien conozca su URL se salta el CDN. No expone datos —son archivos públicos por
  naturaleza— pero sí salta la caché y cualquier regla de borde;
- la arquitectura queda **repartida entre dos proveedores**, lo que en un ramo de Cloud Native hay
  que saber explicar en vez de esconder;
- si el evaluador espera ver CloudFront porque los documentos anteriores lo prometían, **hay que
  llegar con este ADR en la mano**.

## Condiciones para revisar la decisión

- **después del EP1**, comprobar CloudFront con el comando de arriba y añadirlo al script de
  sondeo;
- si se decide ir al dominio único del ADR-024, CloudFront deja de ser opcional: es la pieza que
  lo hace posible;
- si el bucket privado con Origin Access Control pasa a ser un requisito, también.
