# ADR-028 · Armario personal y política editorial del catálogo

Fecha: 23-09-2026 · Estado: implementado en rama, pendiente de despliegue.

## Contexto

El catálogo es para adolescentes y adultos. Variantes de «sin marca» llenaban el filtro;
el seguimiento del BFF era un diccionario en memoria. El armador ya separaba zonas,
pero polerón y chaqueta competían por una ranura y exigía accesorios para completarlo.

## Alternativas

- Guardar deseados/outfits solo en el navegador: fácil, pero no acompaña a la cuenta.
- Crear un servicio de perfiles: buen límite de dominio, con infraestructura adicional.
- Persistir las referencias en PostgreSQL existente, detrás del BFF autenticado.

## Decisión

Se adopta la tercera alternativa. `product.PersonalItems`, creada por migración EF Core,
guarda registros por `(Owner, Kind, Key)`. El gateway obtiene `Owner` del `sub` del JWT
validado de Cognito, también para usuarios federados desde Google. Nunca acepta un
propietario enviado por el navegador. Product Service expone esas operaciones únicamente
en `/internal/personal`, sin ruta pública equivalente en el gateway. Se mantiene la
restricción de publicar su puerto solo en loopback/red interna.

Los outfits guardan IDs por capa y preferencias de talla, no copias de precios. Hay límites
de 200 deseados y 50 outfits por cuenta, cuerpos de hasta 16 KB y escrituras idempotentes
serializadas por usuario en PostgreSQL. Las respuestas personales del BFF usan `no-store`.
El cliente limpia sus colecciones al cambiar de sesión e ignora respuestas atrasadas.

«Genéricas» es un grupo de marca, **no** una categoría de prenda. Se normalizan únicamente
alias explícitos; una marca desconocida conserva su nombre. Los genéricos sin identidad
canónica explícita no se fusionan entre tiendas por compartir un título. La ingesta busca
primero el SKU/tienda existente para no duplicarlo al cambiar su marca normalizada.

Se rechazan señales explícitas de bebé/niño en nombre o taxonomía. No se infiere edad por
XS, talla numérica, «junior» o «juvenil»; «baby tee» y «baby doll» son excepciones de corte.
El backend oculta datos infantiles anteriores incluso antes de la limpieza física.

## Consecuencias

- Los guardados sobreviven al reinicio del gateway y funcionan entre dispositivos.
- Product Service asume temporalmente datos personales de selección: si crecen perfiles
  o funciones sociales, habrá que mover esta tabla a un servicio propietario adecuado.
- La red interna sigue siendo una frontera de confianza: exponer Product Service sin el
  gateway permitiría acceder a rutas internas. No se debe abrir 8081 a internet.
- El borrador del armador sigue siendo local y anónimo; no es un outfit guardado en cuenta.
- La detección de edad es heurística, no una garantía. Los casos ambiguos requieren revisión.
  La limpieza debe auditarse y respaldarse; no se borra masivamente al arrancar una migración.
- El catálogo todavía se descarga completo. Se reduce reactividad y DOM, pero paginación
  y facetas en el servidor siguen siendo necesarias si crece significativamente.
