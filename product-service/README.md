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
| POST | `/api/products/{id}/visits` | suma una visita; `204`, o `404` si no existe |

## Lectura canónica por slug en v2

V2 mantiene temporalmente la entrada por ID para no romper enlaces existentes, pero la convierte
en una redirección hacia la URL legible del producto:

| Método | Cabecera | Ruta | Resultado |
|---|---|---|---|
| GET | `Version: 2.0` | `/api/products/{id}` | `302` hacia `/api/products/{slug}`; `404` si no existe |
| GET | `Version: 2.0` | `/api/products/{slug}` | producto canónico como `ProductResponse`; `404` si no existe |

Por ejemplo, `/api/products/2` redirige a una ruta como
`/api/products/poleron-ck-institutional-blanco-yaf-calvin-klein`. El cliente debe conservar la
cabecera `Version: 2.0` al seguir o solicitar directamente la URL canónica.

El servicio genera `slug` al crear el producto, lo guarda en PostgreSQL y no lo cambia al editar
el nombre, evitando romper enlaces publicados. Si dos nombres producen el mismo valor, añade un
sufijo numérico sólo al segundo. La migración `AddProductSlug` rellena los productos existentes a
partir de su `canonicalKey`, que ya era único.

Ejemplo de una tercera oferta para el mismo modelo:

```json
{
  "canonicalKey": "converse:chuck-taylor-all-star",
  "externalId": "SKU-123",
  "store": "paris",
  "name": "Zapatilla Converse Chuck Taylor All Star negra",
  "brand": "Converse",
  "category": "Zapatillas",
  "bodyArea": "Pies",
  "gender": "Unisex",
  "layer": "Calzado",
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
URL, imagen y disponibilidad. Una tienda puede conservar varios SKU del mismo modelo en la base;
la respuesta pública los consolida en una oferta por tienda, elige el menor precio disponible y
reúne sus tallas. Así una variante no se presenta como si fuera otra tienda ni alarga las tarjetas.

La respuesta tiene la forma:

```text
Product { id, slug, canonicalKey, name, brand, category, bodyArea, gender, layer,
          description, image, offers[] }
Offer   { id, externalId, store, price, sizes[], url, image, active, updatedAt }
```

`category` dice qué prenda es (`Polerones`, `Calcetines`, `Ropa interior femenina`);
`bodyArea` dice dónde se usa (`Cabeza`, `Torso`, `Piernas`, `Pies` o `Cuerpo completo`) y
`gender` conserva `Mujer`, `Hombre`, `Niña`, `Niño`, `Bebé` o `Unisex`; `layer` distingue, por
ejemplo, `Base`, `Abrigo`, `Calcetería` y `Calzado`. Los campos son abiertos
para no volver a bloquear la ingesta cada vez que aparezca una categoría legítima nueva.

### Tres campos que la respuesta trae y la petición no acepta

`ProductResponse` incluye `slug`, `visits` y `createdAt`, y **`ProductRequest` no los tiene**. No
es un olvido:

- **`slug`** es identidad pública administrada por el servicio. Aceptarlo desde el scraper
  permitiría cambiar o duplicar URLs canónicas por accidente.
- **`visits`** solo cambia por `POST /api/products/{id}/visits`, que suma uno con una única
  sentencia `UPDATE ... SET Visits = Visits + 1`. Si se pudiera mandar en el cuerpo, cualquiera con
  el scope de escritura podría poner el número que quisiera; y si se hiciera leyendo, sumando y
  guardando, dos visitas simultáneas leerían el mismo valor y una se perdería.
- **`createdAt`** la pone el servicio en UTC al crear. Si viniera del cliente, el scraper podría
  fechar un producto en el futuro y quedarse para siempre el primer puesto de «Lo más reciente».

`createdAt` usa `DateTimeOffset` y PostgreSQL `timestamp with time zone`, por lo que la respuesta
conserva UTC sin depender de la zona horaria de la EC2. Los productos existentes reciben la hora
de aplicación de la migración; los nuevos se fechan explícitamente en el servicio.

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
