# ADR-019 · API Gateway es el API Manager; Caddy se queda con el TLS

**Fecha:** 07-09-2026

**Estado:** aceptada

## Contexto

El enunciado del EP1 nombra el **API Gateway** con esas palabras, y el punto central del informe
ejecutivo es *justificar la elección de la herramienta de IDaaS y la del API Manager*.

Al desplegar el sistema se instaló **Caddy** en la EC2 para resolver TLS y enrutado, y quedó
cumpliendo el papel de API Manager sin que nadie lo decidiera así. No fue una desobediencia: la
decisión "API Gateway, no Caddy" quedó **con la casilla sin marcar** en la reunión del 2 de
septiembre, y quien construyó la resolvió construyendo.

El problema es que las dos piezas no hacen lo mismo:

| | Caddy | API Gateway HTTP API |
|---|---|---|
| TLS y enrutado | ✅ | ✅ |
| **Valida el JWT en el borde** | ❌ | ✅ con JWT Authorizer |
| Stages `dev` / `prod` | ❌ | ✅ |
| Throttling y cuotas | ❌ | ✅ |
| CORS declarativo | ✅ a mano | ✅ configurado |

## Alternativas consideradas

- **Quedarnos con Caddy y defenderlo en el informe.** Cuesta cero y ya funciona. Se descarta
  porque obliga a argumentar en contra de una rúbrica que nombra el API Gateway, y porque el
  argumento honesto —"un reverse proxy no valida tokens en el borde"— juega en contra nuestra.
- **API Gateway con VPC Link a un ALB**, que es lo que pide el [ADR-015](015-red-privada-con-vpc-link.md).
  Es el diseño correcto y sigue siendo el destino. Se descarta **para el EP1** por tiempo: exige
  VPC propia, subredes en dos zonas, NAT y balanceador, y son piezas que además cobran por hora
  aunque el laboratorio esté cerrado.

## Decisión

**API Gateway HTTP API es el API Manager**, e integra por **HTTP_PROXY** contra la URL pública
del backend. Caddy se queda en la cadena, pero con otro papel:

```
internet → API Gateway  (valida el JWT, CORS, stages)
              ↓
           Caddy        (TLS y enrutado dentro de la EC2)
              ↓
        product-service
```

Se elige **HTTP API** y no REST API: cuesta alrededor de un tercio, trae JWT Authorizer nativo
—en la REST API habría que montar un authorizer Lambda o más plomería— y no necesitamos nada de
lo que la REST API agrega.

Todo se crea con **`tools/crear-api-gateway.sh`**, idempotente y con modo `--borrar`, por la
misma razón que Cognito: la cuenta es de AWS Academy y se puede agotar.

### Qué quedó funcionando, medido

Probado el 07-09 con un token real del app client `pruebas`. Evidencia completa en
[`docs/evidencia/`](../evidencia/).

| Llamada | Resultado |
|---|---|
| `GET /health` sin token | **200** |
| `GET /productos` sin token | **401** |
| `GET /productos` con un token inventado | **401** |
| `GET /productos` con token válido | **200** |
| `POST /productos` con token válido **sin** el scope `ingesta` | **403** |
| `DELETE /productos/{id}` sin token | **401** |
| Preflight desde `https://www.cacha-el-precio.com` | **204**, con el origen explícito |

Los tres códigos que pide la rúbrica salen **en el borde**, antes de que la petición llegue a la
EC2. Un token vencido o falso no gasta instancia.

## Consecuencias

### Lo bueno

- **El innegociable del EP1 queda cumplido**, y con evidencia medida en vez de una promesa.
- **CORS con orígenes explícitos**, nunca `*`. Es un indicador propio del EP2 y además es lo
  correcto: con comodín, cualquier sitio puede llamar a la API desde el navegador de alguien que
  ya inició sesión.
- **Las escrituras quedan detrás de un scope.** `POST`, `PUT` y `DELETE` exigen `ingesta`, que
  solo tiene el app client del scraper. Un usuario normal, aunque sea del grupo `admin`, recibe
  403.
- **El 403 se puede demostrar sin construir nada más**, porque el scope ya existía en el resource
  server desde el 30-08.

### Lo malo, y hay que decirlo

- 🔴 **La EC2 sigue siendo alcanzable directo.** `api.cacha-el-precio.com` responde sin pasar por
  el API Gateway, así que las escrituras abiertas **siguen abiertas** por esa puerta. El API
  Gateway protege el camino nuevo, no cierra el viejo. Eso se cierra con el `@Secured` en
  `product-service` y, después, restringiendo el security group de la EC2.
- **No hay VPC Link ni subred privada**, o sea que este ADR se aparta del 015 a propósito. El 015
  no se anula: sigue siendo el destino, y migrar a él es cambiar el tipo de integración sin tocar
  rutas ni authorizer.
- **El backend está en otra cuenta de AWS** que la del API Gateway. Funciona porque la
  integración es contra una URL pública, pero es una rareza que hay que resolver o explicar.
  Ver [`INTEGRACION.md` §0.2](../INTEGRACION.md).
- **El authorizer confía en un solo user pool.** Mientras el frontend emita tokens del otro, sus
  llamadas por esta puerta darán 401. No es un defecto del API Gateway: es el problema de los dos
  pools, que se decide aparte.
- **Aparece una URL nueva** (`https://<id>.execute-api…`) que cambia si se recrea la API. Para la
  demo sirve tal cual; apuntarle el dominio propio exige dominio personalizado y certificado.

## La trampa que costó media hora, anotada para el que venga

La `aws-cli v1` —la que trae AWS Academy— interpreta **cualquier parámetro que empiece con
`http://` o `https://` como una URL de la que hay que descargar el valor**, y falla con
`Could not connect to the endpoint URL`. Muerde en `--authorization-scopes`, porque nuestro scope
es `https://api.cachaelprecio.cl/ingesta`.

Es la misma trampa que ya estaba anotada en [`DESPLIEGUE.md`](../DESPLIEGUE.md) para el resource
server de Cognito. Se resuelve pasando el cuerpo con `--cli-input-json`, que funciona igual en la
v1 y en la v2.
