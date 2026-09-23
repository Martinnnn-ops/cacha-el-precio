# Catálogo, capas y armario personal

Última revisión: 23-09-2026.

## Cambios

- «Genéricas» agrupa alias de marca vacía/sin marca/genérico, sin alterar la categoría.
- Scraper rechaza prendas infantiles antes de imágenes, persistencia y sincronización.
  Product Service valida ingesta y oculta productos históricos infantiles en sus lecturas.
- El listado conserva las tandas de 48 incorporadas en development. El catálogo usa
  reactividad superficial; las marcas se buscan y muestran de a 16. El selector de prendas
  del armador empieza con 24. Los filtros laterales tienen altura acotada y las imágenes
  no se recortan. Los enlaces de ficha ya no envuelven otros botones/enlaces.
- Capas independientes: cabeza, torso base, torso intermedio, abrigo, interior, piernas,
  calcetines y calzado. Solo base, piernas y calzado son necesarios; las demás son opcionales.
  El filtro Mujer/Hombre/Unisex acota nuevas elecciones sin borrar lo que ya elegiste.
- Después de entrar con Google: corazón para deseados, guardar outfit con nombre y
  «Mi armario» en el menú de cuenta. Stock y precios se vuelven a resolver al abrir un outfit;
  prendas retiradas o incompatibles no se restauran en una ranura equivocada.

## Contrato privado

El BFF requiere JWT de Cognito válido y `sub` en todas estas rutas:

| Método | Ruta | Uso |
|---|---|---|
| GET | `/mi-cuenta` | Deseados y outfits propios |
| PUT / DELETE | `/mi-cuenta/deseados/{id}` | Guardar/quitar producto |
| PUT / DELETE | `/mi-cuenta/outfits/{uuid}` | Guardar/quitar combinación |

Cuerpo de un outfit: `{nombre, seleccion: {"torso-base": 123}, tallaRopa: "M", tallaCalzado: "39"}`.
`GET /seguimiento` conserva `productos` y `total`; POST/DELETE son alias de la persistencia
nueva y responden 204 al guardar/quitar. El diccionario antiguo no era durable ni recuperable.

## Despliegue

1. Respaldar PostgreSQL con el procedimiento habitual.
2. Publicar Product Service y gateway. Al arrancar se aplica `AddPersonalItems` mediante EF.
3. Reconciliar las rutas de AWS con `bash tools/crear-api-gateway.sh` usando las credenciales
   vigentes de la cuenta correcta. Incluye las cinco rutas nuevas protegidas por JWT.
4. Compilar y publicar el frontend del monorepo. `tools/desplegar.sh` incluye `/mi-armario`
   en las rutas SPA de S3; se excluye de sitemap y robots.
5. Verificar login real desde HTTPS con el Google IdP ya configurado en Cognito. Estas pruebas
   locales no sustituyen la validación de federación/redirect URIs en la cuenta AWS.

La decisión y sus límites están en [ADR-028](adr/028-armario-personal-y-politica-catalogo.md).

## Limpiar datos infantiles existentes

No se ha ejecutado sobre la base desplegada. La herramienta usa una conexión administrativa
en `DATABASE_URL`, ambos esquemas y `pg_dump` instalado de versión compatible con el servidor.
No pongas la contraseña como argumento ni subas respaldos al repositorio.

```bash
# Con DATABASE_URL ya cargada desde tu entorno seguro:
scraper-service/.venv/bin/python tools/limpiar-catalogo.py

# Después de revisar los candidatos. La ruta debe estar fuera del repositorio:
scraper-service/.venv/bin/python tools/limpiar-catalogo.py --apply \
  --backup /ruta/privada/catalogo-antes-de-limpieza.sql
```

El primer comando es solo auditoría. El segundo vuelve a identificar los candidatos bajo
locks, crea un respaldo completo con permisos 600 y ejecuta la eliminación en una transacción.
Si el respaldo falla, no borra. Elimina productos infantiles, sus ofertas y capturas/historial
del scraper relacionados; una referencia personal retirada queda como no disponible.
Para recuperar, restaura el SQL en una **base nueva** con `psql --set ON_ERROR_STOP=on --file` y
verifica el contenido antes de sustituir una base activa. El dump contiene datos personales.
Puede bloquear ingesta durante la operación: ejecuta en una ventana de mantenimiento.

## Verificación local

```bash
dotnet build product-service
dotnet build gateway
cd scraper-service
.venv/bin/pytest -m 'not red' -q
cd ../frontend
npm run build
npm run humo
node scripts/probar-coleccion.mjs
```

`tools/probar-catalogo-personal.py` comprueba ingesta, aislamiento de propietarios,
idempotencia concurrente y 401 del BFF, contra servicios **de prueba** en 5581 y 5580.
Usa una PostgreSQL desechable, nunca la base del equipo. La prueba de colección simula HTTP
para cubrir cambios de cuenta, respuestas tardías, fallo de red y outfits por capas.
