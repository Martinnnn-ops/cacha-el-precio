import { createRouter, createWebHistory } from 'vue-router'

import { routes } from '@/core/router/routes'
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

  // Rutas sólo para invitados: quien ya tiene sesión no pinta en /entrar.
  if (to.meta.soloInvitados && cuenta.autenticado) {
    return { name: 'inicio' }
  }

  // Rutas que exigen sesión: se manda a /entrar guardando a dónde iba, pero
  // sólo si ese destino es una ruta interna (ver rutaInternaSegura).
  if (to.meta.requiereSesion && !cuenta.autenticado) {
    const volver = rutaInternaSegura(to.fullPath)

    return {
      name: 'entrar',
      query: volver ? { volver } : undefined,
    }
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
