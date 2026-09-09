// Rutas del módulo. Se concatenan en core/router/routes.js.
// Las vistas van con import() perezoso: cada una viaja en su propio trozo y no
// se descarga hasta que alguien entra en esa ruta.
export default [
  {
    path: '/',
    name: 'inicio',
    component: () => import('@/modules/comparador/views/InicioView.vue'),
    meta: { titulo: 'Inicio' },
  },
  {
    path: '/comparador',
    name: 'comparador',
    component: () => import('@/modules/comparador/views/ComparadorView.vue'),
    meta: { titulo: 'Comparador' },
  },
  {
    // La URL del detalle es el slug: /producto/poleron-ck-institutional-blanco.
    // El slug se genera del nombre, así que cualquier ruta por id o inventada
    // no hace match con ningún producto y la vista lo muestra como "ya no
    // está". La forma queda abierta (el slug puede ser casi cualquier cosa),
    // pero se recorta en la generación para no alargar las URLs.
    path: '/producto/:slug',
    name: 'producto-detalle',
    component: () => import('@/modules/comparador/views/ProductoDetailView.vue'),
    props: true,
    meta: { titulo: 'Detalle del producto', ancho: 'lectura' },
  },
]
