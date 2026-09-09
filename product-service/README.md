# Product Service

Catálogo actual de Cacha el Precio, implementado con ASP.NET Core 10, EF Core y SQLite. Recibe los
productos normalizados por el scraper y los expone al gateway. La base pertenece exclusivamente a
este servicio.

## Ejecutar

Desde la raíz del repositorio:

```bash
dotnet restore product-service/Product-Service.csproj
dotnet ef database update --project product-service/Product-Service.csproj
dotnet run --project product-service/Product-Service.csproj --launch-profile http
```

El perfil HTTP escucha en `http://localhost:8081`. En desarrollo están disponibles:

- salud: `GET /health`;
- OpenAPI: `GET /openapi/v1.json`;
- Scalar: `GET /scalar/v1`.

El contenedor ejecuta las migraciones pendientes al arrancar y almacena la base en
`/app/data/product.db`.

## Contrato HTTP v1

Todas las llamadas utilizan el encabezado `Version: 1.0`.

| Método | Ruta | Resultado |
|---|---|---|
| GET | `/api/products` | listado completo |
| GET | `/api/products/{id}` | producto por ID interno |
| GET | `/api/products/by-category/{category}` | filtro por categoría normalizada |
| GET | `/api/products/by-price/{price}` | filtro por precio exacto |
| GET | `/api/products/by-size/{size}` | productos con esa talla disponible |
| POST | `/api/products` | crea y devuelve `201` |
| PUT | `/api/products/{id}` | reemplaza y devuelve `204` |
| DELETE | `/api/products/{id}` | elimina el producto |
| POST | `/api/products/{id}/visits` | suma una visita; `204`, o `404` si no existe |

Ejemplo de escritura:

```json
{
  "externalId": "SKU-123",
  "store": "sparta",
  "name": "Zapatilla urbana",
  "brand": "Ejemplo",
  "category": "Zapatillas",
  "price": 59990,
  "sizes": {
    "xs": false,
    "s": true,
    "m": true,
    "l": false,
    "xl": false,
    "xxl": false
  },
  "description": "Descripción pública",
  "url": "https://tienda.example/producto",
  "image": "https://imagenes.example/producto.webp",
  "active": true
}
```

`(store, externalId)` tiene un índice único filtrado. Los productos creados manualmente pueden
omitir ambos valores, pero la sincronización del scraper siempre los envía.

### Dos campos que la respuesta trae y la petición no acepta

`ProductResponse` incluye `visits` y `createdAt`, y **`ProductRequest` no los tiene**. No es un
olvido:

- **`visits`** solo cambia por `POST /api/products/{id}/visits`, que suma uno con una única
  sentencia `UPDATE ... SET Visits = Visits + 1`. Si se pudiera mandar en el cuerpo, cualquiera con
  el scope de escritura podría poner el número que quisiera; y si se hiciera leyendo, sumando y
  guardando, dos visitas simultáneas leerían el mismo valor y una se perdería.
- **`createdAt`** la pone el servicio en UTC al crear. Si viniera del cliente, el scraper podría
  fechar un producto en el futuro y quedarse para siempre el primer puesto de «Lo más reciente».

Se serializa marcada como UTC (`DateTime.SpecifyKind`), con la `Z` final. Sin eso SQLite la
devuelve con `Kind = Unspecified`, System.Text.Json la escribe sin zona, y un navegador interpreta
una marca ISO sin zona como **hora local**: en Chile un producto recién creado parecía creado tres
horas en el futuro.

## Migraciones

```bash
dotnet tool install --global dotnet-ef --version 10.0.11
dotnet ef migrations add Nombre --project product-service/Product-Service.csproj
dotnet ef database update --project product-service/Product-Service.csproj
dotnet ef migrations list --project product-service/Product-Service.csproj
dotnet ef migrations has-pending-model-changes --project product-service/Product-Service.csproj
```

No edites la base con SQL manual. Cada cambio del modelo debe quedar representado por una
migración versionada.

## Configuración y Docker

La conexión se sobreescribe sin modificar archivos:

```bash
ConnectionStrings__Sqlite="Data Source=/tmp/product.db" \
  dotnet run --project product-service/Product-Service.csproj
```

```bash
docker build -f product-service/Dockerfile -t cachaelprecio/product-service .
docker run --rm -p 8081:8081 -v product-data:/app/data cachaelprecio/product-service
```

## Verificación

```bash
dotnet format product-service/Product-Service.csproj --verify-no-changes --no-restore
dotnet build product-service/Product-Service.csproj --no-restore
dotnet ef migrations has-pending-model-changes --project product-service/Product-Service.csproj
```
