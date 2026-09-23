import { ref, watch } from 'vue'
import { defineStore } from 'pinia'
import http, { USAR_MOCK } from '@/core/api/http'
import { useCuentaStore } from './cuenta.store'

export const useColeccionStore = defineStore('coleccion', () => {
  const cuenta = useCuentaStore()
  const deseados = ref([])
  const outfits = ref([])
  const error = ref('')
  const ocupado = ref(false)
  const cargado = ref(false)
  let generacion = 0
  let pendiente = null

  watch(() => cuenta.usuario, () => {
    generacion++
    deseados.value = []
    outfits.value = []
    error.value = ''
    cargado.value = false
    ocupado.value = false
    pendiente = null
  }, { flush: 'sync' })

  async function cargar() {
    if (!cuenta.autenticado || cargado.value) return
    if (pendiente) return pendiente
    const version = generacion
    pendiente = (async () => {
      try {
        if (USAR_MOCK) throw new Error('Conecta el backend para guardar en tu cuenta.')
        const datos = await http.get('/mi-cuenta')
        if (version !== generacion) return
        deseados.value = datos.productos.map(String)
        outfits.value = datos.outfits
        cargado.value = true
        error.value = ''
      } catch (e) {
        if (version === generacion) error.value = e.message
      } finally {
        if (version === generacion) pendiente = null
      }
    })()
    return pendiente
  }

  async function cambiar(accion) {
    if (!cuenta.autenticado || ocupado.value) return false
    const version = generacion
    ocupado.value = true
    error.value = ''
    try {
      await cargar()
      if (version !== generacion || !cargado.value) return false
      if (USAR_MOCK) throw new Error('Conecta el backend para guardar en tu cuenta.')
      await accion()
      if (version !== generacion) return false
      cargado.value = false
      await cargar()
      return !error.value
    } catch (e) {
      if (version === generacion) error.value = e.message
      return false
    } finally {
      if (version === generacion) ocupado.value = false
    }
  }

  async function alternar(id) {
    const version = generacion
    await cargar()
    if (version !== generacion || !cargado.value) return false
    return cambiar(() => deseados.value.includes(String(id))
      ? http.delete(`/mi-cuenta/deseados/${id}`)
      : http.put(`/mi-cuenta/deseados/${id}`))
  }

  function guardarOutfit(nombre, outfit) {
    const seleccion = Object.fromEntries(Object.entries(outfit.seleccion)
      .filter(([, id]) => id !== null).map(([parte, id]) => [parte, Number(id)]))
    return cambiar(() => http.put(`/mi-cuenta/outfits/${crypto.randomUUID()}`, {
      nombre: nombre.trim(), seleccion,
      tallaRopa: outfit.tallaRopa, tallaCalzado: outfit.tallaCalzado,
    }))
  }

  return { deseados, outfits, error, ocupado, cargado, cargar, alternar, guardarOutfit,
    quitarOutfit: (id) => cambiar(() => http.delete(`/mi-cuenta/outfits/${id}`)) }
})
