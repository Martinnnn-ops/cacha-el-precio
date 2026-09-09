import { computed } from 'vue'
import { storeToRefs } from 'pinia'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'

// Resuelve el id de una fuente de precio a su nombre y su color.
//
// Antes esto se leía de la constante TIENDAS del código, y por eso la ficha de
// producto mostraba "Más barato en catalogo": con backend real las fuentes
// vienen de la API y no están en esa constante. Aquí se resuelven contra la
// lista que tiene el store, sea la de ejemplo o la del servicio.
export function useTiendas() {
  const { tiendas } = storeToRefs(useComparadorStore())

  const porId = computed(
    () => new Map(tiendas.value.map((t) => [t.id, t])),
  )

  // Si el id no está en la lista se devuelve el id: es feo, pero es honesto y
  // no rompe el render. No debería pasar.
  const nombreTienda = (id) => porId.value.get(id)?.nombre ?? id
  const colorTienda = (id) => porId.value.get(id)?.color ?? 'var(--cep-muted)'

  return { tiendas, nombreTienda, colorTienda }
}
