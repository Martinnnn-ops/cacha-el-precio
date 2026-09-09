// Slug legible para las URL del sitio.
//
// La URL del detalle ES el slug:
//   /producto/poleron-ck-institutional-blanco
// Como la API identifica los productos por id, la vista del detalle resuelve
// el slug mirando el catálogo del store y con ese id pide la ficha. El slug se
// genera siempre del nombre del producto; si el nombre cambia, cambia la URL.

const MAX_SLUG = 60

export function slugProducto(producto) {
  if (!producto || typeof producto.nombre !== 'string') return 'producto'
  return slugificar(producto.nombre)
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