// Rutas del armador de outfits. Se concatenan en core/router/routes.js.
export default [
  {
    path: '/outfits',
    name: 'outfits',
    component: () => import('@/modules/outfits/views/OutfitsView.vue'),
    meta: { titulo: 'Outfits listos' },
  },
  {
    path: '/armar',
    name: 'armar',
    component: () => import('@/modules/outfits/views/ArmarView.vue'),
    meta: { titulo: 'Arma tu outfit' },
  },
]
