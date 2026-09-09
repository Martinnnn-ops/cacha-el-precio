// Slug legible para las URL del sitio.
//
// La URL del detalle ES el slug:
//   /producto/poleron-ck-institutional-blanco-zara
// Como la API identifica los productos por id, la vista del detalle resuelve
// el slug mirando el catálogo del store y con ese id pide la ficha. Se genera
// del nombre del producto; si el nombre cambia, cambia la URL.
//
// ┌─ POR QUÉ EL NOMBRE NO BASTA ──────────────────────────────────────────┐
// │ Hoy cada fila de la API es la publicación de UNA tienda, y dos        │
// │ tiendas venden la misma prenda con el mismo nombre. Con el nombre     │
// │ solo, las dos generaban el mismo slug: la ficha resuelve con          │
// │ `productos.find(...)`, que devuelve la primera, y la segunda oferta   │
// │ quedaba sin ninguna URL que llevara a ella. Existía en el listado y   │
// │ al hacer clic te llevaba a la otra. El `sitemap.xml` emitía además    │
// │ dos entradas idénticas.                                              │
// │                                                                       │
// │ Medido con dos ofertas reales de la misma polera:                    │
// │   id=1  hym   $ 8.990  →  polera-basica-de-algodon                    │
// │   id=2  zara  $12.990  →  polera-basica-de-algodon   ← el mismo       │
// └───────────────────────────────────────────────────────────────────────┘
//
// ⚠️ Esto NO es la solución definitiva, es el arreglo del defecto. Lo que
// falta es una identidad de producto compartida entre tiendas, que hoy no
// existe: el `externalId` es el código interno de cada tienda (HM-001 en H&M,
// ZA-114 en Zara), así que agrupar por él no juntaría nada. Con esa identidad,
// un producto tendría varias ofertas dentro y el slug volvería a ser solo el
// nombre — es el caso que ya contempla la función. Queda como tema del equipo.

const MAX_SLUG = 60

export function slugProducto(producto) {
  if (!producto || typeof producto.nombre !== 'string') return 'producto'

  const base = slugificar(producto.nombre)
  const ofertas = producto.precios ?? []

  // Con varias ofertas el producto YA es una prenda con sus tiendas dentro, y
  // el nombre lo identifica. Con una sola, lo que se está publicando es la
  // oferta de una tienda concreta, y la tienda es parte de su identidad.
  if (ofertas.length !== 1) return base

  // Se mira el valor CRUDO y no el ya slugificado: `slugificar('')` devuelve
  // 'producto' por su valor de respaldo, así que preguntar después de pasar
  // por ella nunca daría vacío y se colaría un `-producto` al final.
  const tienda = ofertas[0]?.tienda

  return tienda ? `${base}-${slugificar(String(tienda))}` : base
}

export function slugificar(texto) {
  const normalizado = texto
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '') // quita tildes
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')

  return normalizado.slice(0, MAX_SLUG).replace(/-+$/g, '') || 'producto'
}