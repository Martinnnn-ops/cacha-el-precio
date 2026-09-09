import AuthLayout from '@/layouts/AuthLayout.vue'
import DefaultLayout from '@/layouts/DefaultLayout.vue'

// Qué layout usa cada ruta se declara en su `meta.layout`. Se resuelve por
// nombre y no importando el componente en cada routes.js para que un módulo no
// tenga que conocer el marco de la aplicación.
export const LAYOUTS = {
  default: DefaultLayout,
  auth: AuthLayout,
}

export function resolverLayout(nombre) {
  return LAYOUTS[nombre] ?? LAYOUTS.default
}
