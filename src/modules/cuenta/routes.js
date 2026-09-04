export default [
  {
    path: '/entrar',
    name: 'entrar',
    component: () => import('@/modules/cuenta/views/EntrarView.vue'),
    meta: { titulo: 'Entrar', soloInvitados: true, layout: 'auth' },
  },
  {
    path: '/registro',
    name: 'registro',
    component: () => import('@/modules/cuenta/views/EntrarView.vue'),
    meta: { titulo: 'Crear cuenta', soloInvitados: true, layout: 'auth' },
  },
  {
    // Vuelta de Google. NO lleva `soloInvitados`: aquí se llega sin sesión y
    // se sale con ella, así que ese guard rebotaría el proceso a medias.
    path: '/auth/google',
    name: 'retorno-google',
    component: () => import('@/modules/cuenta/views/RetornoGoogleView.vue'),
    meta: { titulo: 'Entrando', layout: 'auth' },
  },
]
