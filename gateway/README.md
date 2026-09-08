# gateway

El **BFF**. Es la única puerta de entrada de los servicios: recibe las llamadas del frontend ya
filtradas por el API Gateway, **valida el JWT** y arma las respuestas que la pantalla necesita,
juntando lo que haga falta de `product` y `price`.

> 🎯 Este módulo **es el 40% del EP1**. La rúbrica pide, textual: validar *issuer* y *audience*,
> verificar *firma* y *vigencia*, autorización por rol, y códigos de error adecuados.

## Estado · 07-09-2026

| | Qué | Dónde |
|---|---|---|
| ✅ | Firma verificada contra el **JWKS** de Cognito | `application.properties` |
| ✅ | **Emisor** (`iss`) validado contra nuestro user pool | `application.properties` |
| ✅ | **Vigencia** (`exp` / `nbf`) | `application.properties` |
| ✅ | **`client_id`** contra la lista de clients nuestros | [`ValidadorDeClientId`](src/main/java/cl/cachaelprecio/gateway/seguridad/ValidadorDeClientId.java) |
| ✅ | **`token_use = access`** — un id_token no sirve para autorizar | [`ValidadorDeTokenUse`](src/main/java/cl/cachaelprecio/gateway/seguridad/ValidadorDeTokenUse.java) |
| ✅ | **Roles** desde `cognito:groups` → `@Secured("admin")` | `application.properties` |
| ✅ | **401 y 403 en JSON**, con formato parejo | [`ManejadorDeErroresDeAcceso`](src/main/java/cl/cachaelprecio/gateway/seguridad/ManejadorDeErroresDeAcceso.java) |
| ⬜ | Consultar de verdad a `product-service` y `price-service` | pendiente |
| ⬜ | Test con un token **real** de Cognito (client `pruebas`) | pendiente: necesita credenciales del lab |

## Las rutas

| Ruta | Quién entra | Para qué está |
|---|---|---|
| `GET /health` | cualquiera | Lo consulta el healthcheck del contenedor, que no tiene token |
| `GET /api/precios` | autenticado | **200** con token · **401** sin token |
| `GET /api/admin/diagnostico` | grupo `admin` | **403** para un usuario sin el rol |
| `GET /api/yo` | autenticado | Devuelve los grupos y scopes que el BFF leyó del token |

⚠️ `/api/precios` todavía devuelve datos de ejemplo. Existe para probar la capa de seguridad,
que es lo que se califica; el paso siguiente es que consulte a los servicios de verdad.

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
