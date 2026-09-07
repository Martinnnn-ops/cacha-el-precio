// Slug legible para las URL del sitio.
//
// La URL del detalle lleva el id (que es lo que necesita la API) y un slug
// humano al lado, para que el enlace diga qué producto es antes de entrar:
//   /producto/12/poleron-ck-institutional-blanco
// El slug solo le da sentido a la URL; nadie debe depender de él para lookup.
// Se recalcula al entrar y, si no coincide, la vista redirige al correcto.

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