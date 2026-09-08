# gateway

El **BFF**. Es la única puerta de entrada de los servicios: recibe las llamadas del frontend ya
filtradas por el API Gateway, **valida el JWT** y arma las respuestas que la pantalla necesita,
juntando lo que haga falta de `product` y `price`.

> 🎯 Este módulo **es el 40% del EP1**. La rúbrica pide, textual: validar *issuer* y *audience*,
> verificar *firma* y *vigencia*, autorización por rol, y códigos de error adecuados.

## Estado · 08-09-2026

| | Qué | Dónde |
|---|---|---|
| ✅ | Firma verificada contra el **JWKS** de Cognito | `application.properties` |
| ✅ | **Emisor** (`iss`) validado contra nuestro user pool | `application.properties` |
| ✅ | **Vigencia** (`exp` / `nbf`) | `application.properties` |
| ✅ | **`client_id`** contra la lista de clients nuestros | [`ValidadorDeClientId`](src/main/java/cl/cachaelprecio/gateway/seguridad/ValidadorDeClientId.java) |
| ✅ | **`token_use = access`** — un id_token no sirve para autorizar | [`ValidadorDeTokenUse`](src/main/java/cl/cachaelprecio/gateway/seguridad/ValidadorDeTokenUse.java) |
| ✅ | **Roles** desde `cognito:groups` → `@Secured("admin")` | `application.properties` |
| ✅ | **401 y 403 en JSON**, con formato parejo | [`ManejadorDeErroresDeAcceso`](src/main/java/cl/cachaelprecio/gateway/seguridad/ManejadorDeErroresDeAcceso.java) |
| ✅ | **Consultar de verdad a `product-service`** | [`ClienteDeProductos`](src/main/java/cl/cachaelprecio/gateway/cliente/ClienteDeProductos.java) |
| ⬜ | Componer con `price-service` (historial y descuento real) | pendiente: ese servicio todavía está vacío |
| ⬜ | Test con un token **real** de Cognito (client `pruebas`) | pendiente: necesita credenciales del lab |

## Las rutas

| Ruta | Quién entra | Para qué está |
|---|---|---|
| `GET /health` | cualquiera | Lo consulta el healthcheck del contenedor, que no tiene token |
| `GET /productos` · `/productos/{id}` | **cualquiera** | El catálogo. Consulta a `product-service` |
| `GET /catalogos` | **cualquiera** | Las categorías de prenda |
| `GET /seguimiento` | autenticado | **200** con token · **401** sin token |
| `POST` / `DELETE /seguimiento/{id}` | autenticado | Seguir y dejar de seguir un producto |
| `GET /api/admin/diagnostico` | grupo `admin` | El **403** para un usuario sin el rol |
| `GET /api/yo` | autenticado | Los grupos y scopes que el BFF leyó del token |

### Por qué el catálogo es público

Porque esto es un comparador de precios. Obligar a iniciar sesión para ver un precio que
cualquiera puede leer entrando al sitio de la tienda convierte una herramienta pública en un
servicio con cuenta, y de paso lo saca de los buscadores. Está decidido desde la semana 0 y
escrito en el [ADR-007](../docs/adr/007-catalogo-publico-sin-token.md).

Por eso el **401** de la demo sale de `/seguimiento`, que es privado por su naturaleza, y no de
cerrar el catálogo. Es la diferencia entre proteger lo que hay que proteger y proteger todo para
que la demo salga fácil.

### La cabecera que product-service exige

`product-service` tiene **tres versiones registradas sobre la misma ruta** y sin
`X-API-VERSION` responde **400** — *"More than 1 route matched the incoming request"*. El BFF la
manda siempre: si quien llama pidió una versión se respeta, y si no, usa la que sabe hablar.

Eso último es lo que hace que el BFF sirva de algo y no sea un proxy: el día que
`product-service` jubile la `0.1.0`, se cambia una constante acá y el frontend no se entera.

### La lista de seguimiento

⚠️ Se guarda **en memoria y se pierde al reiniciar**. Está dicho acá y en el código, no
escondido: hoy no hay tabla para esto. El controlador habla con un repositorio y no con un mapa,
así que cuando tenga que sobrevivir a un despliegue se cambia esa clase y nada más.

El dueño de la lista sale del **token**, nunca de un parámetro de la URL. Si viniera en la URL,
cualquiera con sesión podría pedir la lista de otro cambiando un número.

## El detalle que muerde: Cognito no manda `aud`

Cognito **no incluye el claim `aud` en el `access_token`** — solo lo pone en el `id_token`. En su
lugar publica `client_id`. Por eso la validación estándar de *audience* de Micronaut está
**apagada a propósito**: si se encendiera, todo token real daría 401 contra nuestra propia
validación.

El equivalente lo hace `ValidadorDeClientId`, y acepta **más de un** client id: el frontend usa
su client de PKCE y los tests usan el client `pruebas`, que consigue un token con usuario y clave
sin pasar por el navegador. Son dos identificadores distintos para tokens igual de legítimos.

Razonamiento completo en [`ARQUITECTURA.md` §9](../docs/ARQUITECTURA.md) y en
[`IDENTIDAD.md`](../docs/IDENTIDAD.md).

## Levantarlo

Necesita tres variables, que salen de `cognito.env`:

```bash
set -a && source cognito.env && set +a
./mvnw -pl gateway mn:run

curl -i http://localhost:8080/health          # 200
curl -i http://localhost:8080/api/precios     # 401 + JSON
```

| Variable | Qué es |
|---|---|
| `COGNITO_JWKS_URI` | Las llaves públicas con que se verifica la firma |
| `COGNITO_ISSUER` | Nuestro user pool, para que no entre un token de otro |
| `COGNITO_CLIENT_IDS_VALIDOS` | Los client id nuestros, separados por coma |

Puerto por defecto **8080**, configurable con `GATEWAY_PORT`.

## Los tests

```bash
./mvnw -pl gateway test
```

Los de `seguridad/` comprueban que la protección **está encendida de verdad**: es fácil creer que
un servicio está protegido porque la dependencia está en el `pom` y las anotaciones escritas, y
que en realidad responda 200 a todo. Corren sin red y sin AWS, porque un token que no se presenta
se rechaza antes de ir a buscar el JWKS.

## Versión

`0.1.0`, independiente del resto ([ADR-014](../docs/adr/014-versionado-semantico-por-servicio.md)).
