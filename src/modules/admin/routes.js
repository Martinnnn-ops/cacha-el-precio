export default [
  {
    // Panel de la mensajeria (EP3, T12). Solo para el grupo admin de Cognito:
    // la guarda evita mostrar la pantalla y el backend responde 403 igual.
    path: '/admin/mensajeria',
    name: 'admin-mensajeria',
    component: () => import('@/modules/admin/views/AdminMensajeriaView.vue'),
    meta: { titulo: 'Mensajería', requiereSesion: true, soloAdmin: true, sinAnuncios: true },
  },
]
