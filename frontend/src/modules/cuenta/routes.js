export default [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/modules/cuenta/views/EntrarView.vue'),
    // Sin `layout`: usa el layout por defecto, con cabecera y pie completos.
    // `ancho: 'lectura'` estrecha la columna para que la tarjeta no quede
    // perdida en 1180px.
    meta: { titulo: 'Entrar', soloInvitados: true, ancho: 'lectura', centrado: true },
  },
  {
    // La URL anterior estuvo publicada: se redirige en vez de romperla, que es
    // lo que se lleva los enlaces guardados y lo que Google ya tenía indexado.
    path: '/entrar',
    redirect: { name: 'login' },
  },
  {
    // Misma vista que /login —con Google, entrar y registrarse son el mismo
    // acto— así que también el mismo marco. Si una llevara cabecera y la otra
    // no, la misma pantalla se vería distinta según por dónde se llegue.
    path: '/registro',
    name: 'registro',
    component: () => import('@/modules/cuenta/views/EntrarView.vue'),
    meta: { titulo: 'Crear cuenta', soloInvitados: true, ancho: 'lectura', centrado: true },
  },
  {
    // Vuelta de Google. NO lleva `soloInvitados`: aquí se llega sin sesión y
    // se sale con ella, así que ese guard rebotaría el proceso a medias.
    //
    // Ésta sí conserva el layout desnudo: es una pantalla de paso de dos
    // segundos, sin contenido propio. Ponerle cabecera y pie invita a hacer
    // clic en algo justo mientras se está cerrando la sesión de Google.
    path: '/auth/google',
    name: 'retorno-google',
    component: () => import('@/modules/cuenta/views/RetornoGoogleView.vue'),
    meta: { titulo: 'Entrando', layout: 'auth' },
  },
]
