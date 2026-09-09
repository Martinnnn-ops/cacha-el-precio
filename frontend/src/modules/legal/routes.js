export default [
  {
    path: '/terminos',
    name: 'terminos',
    component: () => import('@/modules/legal/views/TerminosView.vue'),
    meta: { titulo: 'Términos y condiciones', ancho: 'lectura' },
  },
  {
    path: '/privacidad',
    name: 'privacidad',
    component: () => import('@/modules/legal/views/PrivacidadView.vue'),
    meta: { titulo: 'Política de privacidad', ancho: 'lectura' },
  },
  {
    path: '/preguntas',
    name: 'preguntas',
    component: () => import('@/modules/legal/views/PreguntasView.vue'),
    meta: { titulo: 'Preguntas frecuentes', ancho: 'lectura' },
  },
]
