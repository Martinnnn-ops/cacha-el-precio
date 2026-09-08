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

- `/api/products...`: contrato actual, proxy a Product Service;
- `/productos...` y `/catalogos`: aliases temporales para el frontend existente;
- `/seguimiento...`: rutas autenticadas, con persistencia en memoria;
- `/api/yo`: inspección de claims autenticada;
- `/api/admin/diagnostico`: exige el grupo `admin`;
- `/health`: pública.

Las lecturas del catálogo son públicas. `POST`, `PUT` y `DELETE` requieren el scope de escritura.
Si Product Service no responde, el gateway devuelve un `502` JSON estable.

## Deuda conocida

- validar la implementación con tokens reales del User Pool que usará el frontend;
- persistir el seguimiento antes de depender de él en producción;
- retirar los aliases cuando el frontend consuma `/api/products`;
- restringir la EC2 para que no exista una ruta que evite API Gateway.
