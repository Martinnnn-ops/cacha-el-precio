# Product Service

Catálogo canónico de Cacha el Precio, implementado con ASP.NET Core 10, EF Core y PostgreSQL.
Cada producto representa una marca/modelo y contiene una o más ofertas de tiendas distintas.
El servicio es dueño del esquema `product`; el scraper conserva sus datos en `scraper` y publica
ofertas exclusivamente por HTTP.

## Ejecutar

Desde la raíz, con PostgreSQL disponible:

```bash
dotnet restore product-service/Product-Service.csproj
ConnectionStrings__PostgreSql='Host=localhost;Port=5432;Database=cachaelprecio;Username=cachaelprecio;Password=TU_PASSWORD;Search Path=product,public' \
  dotnet ef database update --project product-service/Product-Service.csproj
ConnectionStrings__PostgreSql='Host=localhost;Port=5432;Database=cachaelprecio;Username=cachaelprecio;Password=TU_PASSWORD;Search Path=product,public' \
  dotnet run --project product-service/Product-Service.csproj --launch-profile http
```

El perfil HTTP escucha en `http://localhost:8081`. En desarrollo están disponibles `/health`,
`/openapi/v1.json` y `/scalar/v1`. El contenedor aplica las migraciones pendientes al arrancar.

## Contrato HTTP v1

Todas las llamadas utilizan `Version: 1.0`.

| Método | Ruta | Resultado |
|---|---|---|
| GET | `/api/products` | productos con todas sus ofertas |
| GET | `/api/products/{id}` | producto canónico por ID |
| GET | `/api/products/by-category/{category}` | categoría libre, sin enum cerrado |
| GET | `/api/products/by-price/{price}` | cualquier oferta activa con ese precio |
| GET | `/api/products/by-size/{size}` | cualquier oferta activa con esa talla |
| POST | `/api/products` | upsert idempotente de producto/oferta; devuelve `200` |
| PUT | `/api/products/{id}` | actualiza el producto y su oferta; devuelve `204` |
| DELETE | `/api/products/{id}` | elimina el producto y sus ofertas |

Ejemplo de una tercera oferta para el mismo modelo:

```json
{
  "canonicalKey": "converse:chuck-taylor-all-star",
  "externalId": "SKU-123",
  "store": "paris",
  "name": "Zapatilla Converse Chuck Taylor All Star negra",
  "brand": "Converse",
  "category": "Zapatillas",
  "price": 59990,
  "sizes": ["38", "39", "42.5", "M"],
  "description": "Descripción pública",
  "url": "https://tienda.example/producto",
  "image": "https://imagenes.example/producto.webp",
  "active": true
}
```

`canonicalKey` agrupa marca/modelo; si se omite, Product Service lo deriva del nombre. La regla
elimina términos de género, color y talla, por lo que es una heurística y admite corrección manual.
`(Store, ExternalId)` es único dentro de las ofertas: repetir el POST actualiza precio, tallas,
URL, imagen y disponibilidad en lugar de duplicar la tienda.

La respuesta tiene la forma:

```text
Product { id, canonicalKey, name, brand, category, description, image, offers[] }
Offer   { id, externalId, store, price, sizes[], url, image, active, updatedAt }
```

## Migraciones

```bash
dotnet tool install --global dotnet-ef --version 10.0.11
dotnet ef migrations add Nombre --project product-service/Product-Service.csproj
dotnet ef database update --project product-service/Product-Service.csproj
dotnet ef migrations list --project product-service/Product-Service.csproj
dotnet ef migrations has-pending-model-changes --project product-service/Product-Service.csproj
```

La migración `InitialPostgreSqlCatalog` reemplaza las migraciones SQLite anteriores antes del
primer despliegue de la versión C#. No convierte automáticamente un archivo `product.db`: si hay
datos locales valiosos deben exportarse por la API antes de cambiar de versión.

## Docker y verificación

```bash
docker compose up --build -d postgres product-service
curl http://localhost:8081/health
curl -H 'Version: 1.0' http://localhost:8081/api/products

dotnet format product-service/Product-Service.csproj --verify-no-changes --no-restore
dotnet build product-service/Product-Service.csproj --no-restore
dotnet ef migrations has-pending-model-changes --project product-service/Product-Service.csproj
```
