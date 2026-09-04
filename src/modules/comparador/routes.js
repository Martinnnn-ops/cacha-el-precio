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
    // La forma del id se declara en la propia ruta: /producto/abc o
    // /producto/../algo ni siquiera llegan a la vista, caen en el 404. Vale
    // más rechazarlo aquí que confiar en que cada vista se acuerde de validar.
    path: '/producto/:id(\\d{1,12})',
    name: 'producto-detalle',
    component: () => import('@/modules/comparador/views/ProductoDetailView.vue'),
    props: true,
    meta: { titulo: 'Detalle del producto', ancho: 'lectura' },
  },
]
