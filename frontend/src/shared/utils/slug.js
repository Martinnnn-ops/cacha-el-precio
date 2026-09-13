// Slug canónico que entrega Product Service. La forma antigua `nombre-id` se
// conserva sólo para poder redirigir enlaces guardados antes de API V2.

const MAX_SLUG = 60
const SLUG_VALIDO = /^(?![0-9]+$)[a-z0-9]+(?:-[a-z0-9]+)*$/

export function slugProducto(producto) {
  const canonico = String(producto?.slug ?? '').trim()

  if (SLUG_VALIDO.test(canonico)) return canonico

  return slugProductoAnterior(producto)
}

export function slugProductoAnterior(producto) {
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
