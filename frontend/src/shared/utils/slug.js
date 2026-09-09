// Slug legible para las URL del sitio.
//
// La URL del detalle ES el slug:
//   /producto/poleron-ck-institutional-blanco-42
// Como la API identifica los productos por id, la vista del detalle resuelve
// el slug mirando el catálogo del store y con ese id pide la ficha. El id se
// incluye porque dos productos canónicos todavía pueden compartir nombre; sin
// él una ficha y una entrada del sitemap quedarían ocultas tras la otra.

const MAX_SLUG = 60

export function slugProducto(producto) {
  if (!producto || typeof producto.nombre !== 'string') return 'producto'

  const identidad = String(producto.id ?? '').trim()

  if (!identidad) return slugificar(producto.nombre)

  const sufijo = slugificar(identidad)

  return `${slugificar(producto.nombre, MAX_SLUG - sufijo.length - 1)}-${sufijo}`
}

// `maximo` deja componer un slug de varias partes sin que la \u00faltima se pierda
// por el recorte: quien va a a\u00f1adir un sufijo reserva su espacio al recortar
// la primera parte.
export function slugificar(texto, maximo = MAX_SLUG) {
  const normalizado = texto
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '') // quita tildes
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')

  return normalizado.slice(0, Math.max(1, maximo)).replace(/-+$/g, '') || 'producto'
}
