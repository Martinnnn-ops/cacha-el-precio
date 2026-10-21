import { createRouter, createWebHistory } from 'vue-router'

import { routes } from '@/core/router/routes'
import { hayRetornoPendiente } from '@/modules/cuenta/services/google.oauth'
import { useCuentaStore } from '@/modules/cuenta/store/cuenta.store'
import { rutaInternaSegura } from '@/shared/utils/rutas'

const router = createRouter({
  history: createWebHistory(),
  routes,

  // Cada navegación empieza arriba, salvo al volver atrás.
  scrollBehavior(to, from, savedPosition) {
    return savedPosition ?? { top: 0 }
  },
})

// ═══════════════════════ guards ═══════════════════════
//
// IMPORTANTE: esto es protección de navegación y de experiencia de uso, NO
// seguridad. Todo lo que hay aquí corre en el navegador del usuario y se puede
// saltar con las herramientas de desarrollo. La autorización de verdad la tiene
// que hacer el backend en cada petición, sin excepción.
router.beforeEach((to) => {
  const cuenta = useCuentaStore()

  // Respaldo del retorno de Google. Cognito devuelve a la persona a
  // /auth/google?code=…&state=…, pero si algo delante de la aplicación
  // (una regla de redirección del CDN, por ejemplo) manda esa ruta a `/`
  // conservando sólo la query, la vista de retorno no llegaba a ejecutarse y
  // el inicio de sesión, que había salido bien, se perdía sin avisar.
  //
  // Se reconoce por dos cosas a la vez: que la portada traiga code (o error) y
  // state, y que ESTA pestaña tenga un inicio de sesión a medias. Con la
  // segunda, un enlace cualquiera con `?code=` no secuestra la navegación.
  if (
    to.path === '/' &&
    typeof to.query.state === 'string' &&
    (typeof to.query.code === 'string' || typeof to.query.error === 'string') &&
    hayRetornoPendiente()
  ) {
    return {
      name: 'retorno-google',
      query: {
        code: to.query.code,
        error: to.query.error,
        state: to.query.state,
      },
      replace: true,
    }
  }

  // Rutas sólo para invitados: quien ya tiene sesión no pinta en /login.
  if (to.meta.soloInvitados && cuenta.autenticado) {
    return { name: 'inicio' }
  }

  // Rutas que exigen sesión: se manda a /login guardando a dónde iba, pero
  // sólo si ese destino es una ruta interna (ver rutaInternaSegura).
  if (to.meta.requiereSesion && !cuenta.autenticado) {
    const volver = rutaInternaSegura(to.fullPath)

    return {
      name: 'login',
      query: volver ? { volver } : undefined,
    }
  }

  // Pantallas de administracion: sin el grupo admin no se muestran. El backend
  // responde 403 de todas formas; esto solo evita ensenar una pantalla vacia.
  if (to.meta.soloAdmin && !cuenta.esAdmin) {
    return { name: 'inicio' }
  }

  return true
})

const BASE_TITULO = 'Cacha el Precio'

router.afterEach((to) => {
  // El título se compone SÓLO con textos del propio mapa de rutas, nunca con
  // algo que venga de la URL: si no, un enlace preparado podría escribir lo
  // que quisiera en la pestaña y en el historial.
  document.title = to.meta.titulo
    ? `${to.meta.titulo} · ${BASE_TITULO}`
    : BASE_TITULO
})

export default router
