import { computed, ref, watch } from 'vue'
import { defineStore } from 'pinia'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import {
  CUERPO_COMPLETO,
  IDS_PARTES,
  esCuerpoCompleto,
  parteDeCategoria,
} from '@/modules/outfits/data/partes'
import { precioMasBajo } from '@/shared/utils/precios'

// El outfit que se está armando.
//
// Guarda ids, no prendas: el catálogo lo tiene comparador.store y de ahí se
// resuelven. Así no hay dos copias del mismo producto que puedan discrepar
// cuando cambie un precio.

const CLAVE = 'cep:outfit'

function vacio() {
  return Object.fromEntries(IDS_PARTES.map((id) => [id, null]))
}

function leerGuardado() {
  try {
    const guardado = JSON.parse(localStorage.getItem(CLAVE) ?? 'null')

    if (!guardado || typeof guardado !== 'object') return vacio()

    // Sólo se aceptan las partes conocidas: si mañana cambia el modelo, un
    // outfit viejo no mete claves inventadas en el estado.
    return Object.fromEntries(
      IDS_PARTES.map((id) => [
        id,
        typeof guardado[id] === 'string' ? guardado[id] : null,
      ]),
    )
  } catch {
    return vacio()
  }
}

export const useOutfitStore = defineStore('outfit', () => {
  const catalogo = useComparadorStore()

  const seleccion = ref(leerGuardado())

  // Un outfit a medio armar no se pierde al recargar.
  watch(
    seleccion,
    (valor) => {
      try {
        localStorage.setItem(CLAVE, JSON.stringify(valor))
      } catch {
        // Sin almacenamiento el outfit dura la sesión; no es motivo para fallar.
      }
    },
    { deep: true },
  )

  /** Prendas del catálogo que pueden ir en una ranura. */
  function opcionesPara(parte) {
    return catalogo.productos.filter((p) => {
      const suya = parteDeCategoria(p.categoria)

      if (suya !== parte) return false

      // Una prenda de cuerpo completo se ofrece en torso, no en piernas.
      if (parte === 'piernas' && esCuerpoCompleto(p)) return false

      return true
    })
  }

  // Prenda resuelta de cada ranura. Una de cuerpo completo aparece también en
  // la ranura que cubre, marcada, para que se vea que esa parte ya está.
  const prendas = computed(() => {
    const puesto = Object.fromEntries(
      IDS_PARTES.map((id) => [
        id,
        seleccion.value[id] ? catalogo.productoById(seleccion.value[id]) : null,
      ]),
    )

    const torso = puesto.torso

    if (torso && esCuerpoCompleto(torso)) {
      CUERPO_COMPLETO.ocupa
        .filter((id) => id !== 'torso')
        .forEach((id) => {
          puesto[id] = torso
        })
    }

    return puesto
  })

  // Las partes que cubre una prenda de cuerpo completo no se pueden elegir
  // aparte: llevar vestido y pantalón a la vez no es un outfit.
  const partesBloqueadas = computed(() => {
    const torso = prendas.value.torso

    if (!torso || !esCuerpoCompleto(torso)) return []

    return CUERPO_COMPLETO.ocupa.filter((id) => id !== 'torso')
  })

  // Cada pieza distinta del outfit, sin repetir la de cuerpo completo.
  const piezas = computed(() => {
    const vistos = new Set()

    return IDS_PARTES.map((parte) => ({ parte, producto: prendas.value[parte] }))
      .filter(({ producto }) => {
        if (!producto || vistos.has(producto.id)) return false

        vistos.add(producto.id)

        return true
      })
  })

  const completo = computed(() =>
    IDS_PARTES.every((id) => prendas.value[id] !== null),
  )

  // ——— dinero ———

  // Cada pieza en la tienda donde está más barata. precioMasBajo ya descarta
  // las agotadas, así que una oferta que no se puede comprar no cuenta.
  const desglose = computed(() =>
    piezas.value.map(({ parte, producto }) => ({
      parte,
      producto,
      oferta: precioMasBajo(producto),
    })),
  )

  const total = computed(() =>
    desglose.value.reduce((suma, { oferta }) => suma + (oferta?.precio ?? 0), 0),
  )

  const tiendasImplicadas = computed(
    () => new Set(desglose.value.map(({ oferta }) => oferta?.tienda).filter(Boolean)),
  )

  // Lo que costaría comprarlo todo en una sola tienda. Sólo cuentan las que
  // tienen TODAS las piezas con stock: una tienda a la que le falta una prenda
  // no es una alternativa de compra única.
  const mejorTiendaUnica = computed(() => {
    if (piezas.value.length === 0) return null

    const candidatas = new Map()

    catalogo.tiendas.forEach((tienda) => {
      let suma = 0

      const tieneTodo = piezas.value.every(({ producto }) => {
        const oferta = producto.precios.find(
          (o) => o.tienda === tienda.id && o.stock,
        )

        if (!oferta) return false

        suma += oferta.precio

        return true
      })

      if (tieneTodo) candidatas.set(tienda.id, suma)
    })

    if (candidatas.size === 0) return null

    const [tienda, precio] = [...candidatas.entries()].sort(
      (a, b) => a[1] - b[1],
    )[0]

    return { tienda, total: precio }
  })

  // Lo que se ahorra comprando pieza por pieza en vez de todo en una tienda.
  // Puede ser 0 si la mejor tienda única ya tiene todo al mejor precio.
  const ahorroRepartiendo = computed(() => {
    const unica = mejorTiendaUnica.value

    if (!unica) return 0

    return Math.max(0, unica.total - total.value)
  })

  // ——— acciones ———

  function ponerPrenda(parte, productoId) {
    if (!IDS_PARTES.includes(parte)) return

    seleccion.value = { ...seleccion.value, [parte]: productoId }

    // Al poner una prenda de cuerpo completo se limpia lo que hubiera en las
    // partes que cubre, para no dejar una selección contradictoria guardada.
    const producto = catalogo.productoById(productoId)

    if (producto && esCuerpoCompleto(producto)) {
      CUERPO_COMPLETO.ocupa
        .filter((id) => id !== parte)
        .forEach((id) => {
          seleccion.value[id] = null
        })
    }
  }

  function quitarPrenda(parte) {
    seleccion.value = { ...seleccion.value, [parte]: null }
  }

  function limpiar() {
    seleccion.value = vacio()
  }

  function ponerOutfit(porParte) {
    const nuevo = vacio()

    IDS_PARTES.forEach((id) => {
      if (typeof porParte?.[id] === 'string') nuevo[id] = porParte[id]
    })

    seleccion.value = nuevo
  }

  return {
    seleccion,
    prendas,
    piezas,
    partesBloqueadas,
    completo,
    desglose,
    total,
    tiendasImplicadas,
    mejorTiendaUnica,
    ahorroRepartiendo,
    opcionesPara,
    ponerPrenda,
    quitarPrenda,
    limpiar,
    ponerOutfit,
  }
})
