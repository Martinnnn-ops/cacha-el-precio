import { computed, ref, watch } from 'vue'
import { defineStore } from 'pinia'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'

// Selección de productos para comparar lado a lado.
//
// Guarda ids, no productos: el catálogo lo tiene comparador.store y de ahí se
// resuelven. Así no hay dos copias del mismo producto que puedan discrepar
// cuando cambie un precio.

// Tres es el máximo que cabe en pantalla sin que las columnas queden
// ilegibles. Con cuatro, en un móvil cada columna mide 80px.
export const MAXIMO = 3

const CLAVE = 'cep:comparar'

function leerGuardado() {
  try {
    const guardado = JSON.parse(localStorage.getItem(CLAVE) ?? '[]')

    return Array.isArray(guardado)
      ? guardado.filter((id) => typeof id === 'string').slice(0, MAXIMO)
      : []
  } catch {
    return []
  }
}

export const useCompararStore = defineStore('comparar', () => {
  const catalogo = useComparadorStore()

  const ids = ref(leerGuardado())

  // Último intento rechazado por el tope. Lo enseña la barra: así el mensaje
  // vive en un sitio y no hay que pasar un evento por cada rejilla que pinte
  // tarjetas.
  const avisoTope = ref(false)

  watch(
    ids,
    (valor) => {
      try {
        localStorage.setItem(CLAVE, JSON.stringify(valor))
      } catch {
        // Sin almacenamiento la selección dura la sesión.
      }
    },
    { deep: true },
  )

  // Productos resueltos. Se descartan los ids que ya no están en el catálogo:
  // si una prenda desaparece, la comparación no se rompe.
  const productos = computed(() =>
    ids.value.map((id) => catalogo.productoById(id)).filter((p) => p !== null),
  )

  const cuantos = computed(() => productos.value.length)
  const lleno = computed(() => ids.value.length >= MAXIMO)

  const tiene = (id) => ids.value.includes(id)

  // Se puede quitar siempre; añadir sólo si queda hueco.
  const bloqueado = (id) => !tiene(id) && lleno.value

  function alternar(id) {
    if (tiene(id)) {
      ids.value = ids.value.filter((x) => x !== id)

      return 'quitado'
    }

    if (lleno.value) {
      avisoTope.value = true

      return 'lleno'
    }

    avisoTope.value = false

    ids.value = [...ids.value, id]

    return 'agregado'
  }

  function quitar(id) {
    ids.value = ids.value.filter((x) => x !== id)
    // Al hacer hueco el aviso ya no aplica.
    avisoTope.value = false
  }

  function vaciar() {
    ids.value = []
    avisoTope.value = false
  }

  return {
    ids,
    avisoTope,
    productos,
    cuantos,
    lleno,
    tiene,
    bloqueado,
    alternar,
    quitar,
    vaciar,
    MAXIMO,
  }
})
