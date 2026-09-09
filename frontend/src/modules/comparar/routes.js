export default [
  {
    path: '/comparar',
    name: 'comparar',
    component: () => import('@/modules/comparar/views/CompararView.vue'),
    meta: { titulo: 'Comparación' },
  },
]
