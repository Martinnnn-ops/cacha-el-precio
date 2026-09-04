// Mapa de rutas de toda la aplicación. Cada módulo publica las suyas en su
// routes.js y aquí sólo se concatenan: añadir un módulo es un import más.
//
// Vive separado de index.js a propósito. index.js llama a createWebHistory(),
// que toca `window` al importarse: si el mapa estuviera ahí, no se podría
// importar desde un test ni desde un render en servidor.
import comparadorRoutes from '@/modules/comparador/routes'
import compararRoutes from '@/modules/comparar/routes'
import cuentaRoutes from '@/modules/cuenta/routes'
import legalRoutes from '@/modules/legal/routes'
import outfitsRoutes from '@/modules/outfits/routes'

export const routes = [
  ...comparadorRoutes,
  ...compararRoutes,
  ...cuentaRoutes,
  ...outfitsRoutes,
  ...legalRoutes,

  {
    path: '/:pathMatch(.*)*',
    name: 'no-encontrado',
    component: () => import('@/shared/views/NoEncontradoView.vue'),
    // Sin anuncios: las políticas de AdSense prohíben ponerlos en páginas de
    // error, y hacerlo puede costar la cuenta entera.
    meta: { titulo: 'Página no encontrada', sinAnuncios: true },
  },
]

export default routes
