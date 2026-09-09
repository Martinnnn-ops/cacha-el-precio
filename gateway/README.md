# Gateway

BFF de Cacha el Precio implementado con ASP.NET Core 10. Es la entrada pública a los servicios de
aplicación; Caddy y API Gateway deben apuntar a él, nunca directamente a Product Service.

## Responsabilidades

- valida firma, emisor y vigencia de los access tokens de Cognito;
- exige `token_use=access` y un `client_id` conocido;
- autoriza escrituras por el scope configurado y administración por `cognito:groups`;
- reenvía el catálogo a Product Service y preserva el encabezado `Version: 1.0`;
- mantiene temporalmente el seguimiento por usuario en memoria.

## Configuración

```bash
PRODUCT_URL=http://localhost:8081
COGNITO_ISSUER=https://cognito-idp.us-east-1.amazonaws.com/us-east-1_xxxxx
COGNITO_CLIENT_IDS_VALIDOS=client_web,client_pruebas
COGNITO_WRITE_SCOPE=https://api.cachaelprecio.cl/ingesta
```

El último valor debe coincidir con `COGNITO_SCRAPER_SCOPE`, generado por
`tools/crear-cognito.sh`. La validación estándar de audience queda desactivada porque Cognito
incluye el identificador en `client_id` para access tokens; el gateway valida ese claim de forma
explícita.

## Ejecución

```bash
dotnet restore gateway/Gateway.csproj
PRODUCT_URL=http://localhost:8081 dotnet run --project gateway/Gateway.csproj
```

El contenedor escucha en `8080`. En el Compose no publica ese puerto directamente: solo Caddy le
habla por la red interna.

## Rutas

El **contrato público es el de este servicio, en español** — decidido en el
[ADR-021](../docs/adr/021-contrato-publico-en-el-bff.md). Las rutas `/api/products...` existen
para uso interno, pero el frontend no las consume: si el navegador pidiera el mismo path que
publica Product Service, este gateway sería un intermediario transparente y el nombre del servicio
interno viajaría hasta el navegador de cada usuario.

| Ruta | Acceso | Sin token |
|---|---|---|
| `GET /productos` · `/productos/{id}` | pública | 200 · 404 si no existe |
| `GET /catalogos` | pública | 200 |
| `POST /productos/{id}/visitas` | **pública** | 204 · 404 si no existe |
| `POST` · `PUT` · `DELETE /productos` | scope de escritura | **401** |
| `GET /seguimiento` · `POST`/`DELETE /seguimiento/{id}` | autenticada | **401** |
| `GET /api/yo` | autenticada | **401** |
| `GET /api/admin/diagnostico` | grupo `admin` | **401** |
| `GET /health` | pública | 200 |

Dos rutas piden explicación:

- **`POST /productos/{id}/visitas` es pública a propósito.** Es la única escritura anónima del
  sistema, y puede serlo porque no escribe nada que el usuario controle: suma uno a un contador, no
  acepta cuerpo y no devuelve datos. Pedir sesión para contar una vista dejaría «Lo más visto»
  midiendo solo a quien inicia sesión.
- **`GET /catalogos` no es un alias.** Deriva las categorías del catálogo real, con su conteo.
  Hasta el 09-09 devolvía seis categorías fijas escritas en `Program.cs`, con ids del 1 al 6 que no
  correspondían a ningún campo del producto. Es también la única ruta que transforma datos en vez
  de reenviarlos, que es la deuda que el ADR-021 dejó anotada: las demás aíslan el nombre, no la
  carga útil.

Si Product Service no responde, el gateway devuelve un `502` JSON estable.

## Deuda conocida

- validar la implementación con tokens reales del User Pool que usará el frontend;
- persistir el seguimiento antes de depender de él en producción;
- que las rutas en español transformen la respuesta y no solo el nombre de la URL: hoy son
  passthrough puro salvo `/catalogos`, así que el aislamiento que consiguen es parcial;
- restringir la EC2 para que no exista una ruta que evite API Gateway.
