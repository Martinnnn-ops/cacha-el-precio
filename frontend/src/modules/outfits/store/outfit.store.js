import { computed, ref, watch } from 'vue'
import { defineStore } from 'pinia'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import {
  CUERPO_COMPLETO,
  IDS_PARTES,
  esCuerpoCompleto,
  parteDeProducto,
} from '@/modules/outfits/data/partes'
import { precioMasBajo } from '@/shared/utils/precios'

// El outfit que se está armando.
//
// Guarda ids, no prendas: el catálogo lo tiene comparador.store y de ahí se
// resuelven. Así no hay dos copias del mismo producto que puedan discrepar
// cuando cambie un precio.

const CLAVE = 'cep:outfit'
const CLAVE_TALLA_ROPA = 'cep:outfit:talla-ropa'
const CLAVE_TALLA_CALZADO = 'cep:outfit:talla-calzado'

function vacio() {
  return Object.fromEntries(IDS_PARTES.map((id) => [id, null]))
}

function leerGuardado() {
  try {
    const guardado = JSON.parse(localStorage.getItem(CLAVE) ?? 'null')

    if (!guardado || typeof guardado !== 'object') return vacio()

    // Sólo se aceptan las partes conocidas: si mañana cambia el modelo, un
    // outfit viejo no mete claves inventadas en el estado.
    const migrado = {
      ...guardado,
      'torso-base': guardado['torso-base'] ?? guardado.torso ?? null,
      calzado: guardado.calzado ?? guardado.pies ?? null,
    }

    return Object.fromEntries(
      IDS_PARTES.map((id) => [id, typeof migrado[id] === 'string' ? migrado[id] : null]),
    )
  } catch {
    return vacio()
  }
}

function leerPreferencia(clave) {
  try {
    return localStorage.getItem(clave) ?? ''
  } catch {
    return ''
  }
}

export const useOutfitStore = defineStore('outfit', () => {
  const catalogo = useComparadorStore()

  const seleccion = ref(leerGuardado())
  const tallaRopa = ref(leerPreferencia(CLAVE_TALLA_ROPA))
  const tallaCalzado = ref(leerPreferencia(CLAVE_TALLA_CALZADO))

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

  function guardarPreferencia(clave, valor) {
    try {
      if (valor) localStorage.setItem(clave, valor)
      else localStorage.removeItem(clave)
    } catch {
      // La preferencia sigue funcionando durante la sesión.
    }
  }

  watch(tallaRopa, (valor) => guardarPreferencia(CLAVE_TALLA_ROPA, valor))
  watch(tallaCalzado, (valor) => guardarPreferencia(CLAVE_TALLA_CALZADO, valor))

  function tallasDePartes(partes) {
    // Las tallas salen del stock real. `numeric: true` mantiene 36, 37.5, 38
    // en orden sin desordenar XS, S, M, L.
    return [
      ...new Set(
        catalogo.productos
          .filter((producto) => partes.includes(parteDeProducto(producto)))
          .flatMap((producto) =>
            producto.precios
              .filter((oferta) => oferta.stock)
              .flatMap((oferta) => oferta.tallas ?? []),
          ),
      ),
    ].sort((a, b) => a.localeCompare(b, 'es', { numeric: true }))
  }

  const tallasRopa = computed(() =>
    tallasDePartes(['cabeza', 'torso-base', 'torso-abrigo', 'interior', 'piernas']),
  )
  const tallasCalzado = computed(() => tallasDePartes(['calcetines', 'calzado']))

  function tallaParaParte(parte) {
    return ['calcetines', 'calzado'].includes(parte) ? tallaCalzado.value : tallaRopa.value
  }

  function ofertaPara(producto, parte = parteDeProducto(producto)) {
    return precioMasBajo(producto, tallaParaParte(parte))
  }

  /** Prendas del catálogo que pueden ir en una ranura. */
  function opcionesPara(parte) {
    return catalogo.productos.filter((p) => {
      const suya = parteDeProducto(p)

      if (suya !== parte) return false

      // Una prenda de cuerpo completo se ofrece en torso, no en piernas.
      if (parte === 'piernas' && esCuerpoCompleto(p)) return false

      return ofertaPara(p, parte) !== null
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

    const torso = puesto['torso-base']

    if (torso && esCuerpoCompleto(torso)) {
      CUERPO_COMPLETO.ocupa
        .filter((id) => id !== 'torso-base')
        .forEach((id) => {
          puesto[id] = torso
        })
    }

    return puesto
  })

  // Las partes que cubre una prenda de cuerpo completo no se pueden elegir
  // aparte: llevar vestido y pantalón a la vez no es un outfit.
  const partesBloqueadas = computed(() => {
    const torso = prendas.value['torso-base']

    if (!torso || !esCuerpoCompleto(torso)) return []

    return CUERPO_COMPLETO.ocupa.filter((id) => id !== 'torso-base')
  })

  // Cada pieza distinta del outfit, sin repetir la de cuerpo completo.
  const piezas = computed(() => {
    const vistos = new Set()

    return IDS_PARTES.map((parte) => ({
      parte,
      producto: prendas.value[parte],
    })).filter(({ producto }) => {
      if (!producto || vistos.has(producto.id)) return false

      vistos.add(producto.id)

      return true
    })
  })

  // Una capa sin productos todavía no impide completar el outfit. La decisión
  // depende del catálogo con stock, NO de la talla elegida: si una talla deja
  // una ranura sin alternativas, esa ranura debe invalidar el progreso en vez
  // de desaparecer del denominador y mantener el outfit como “completo”.
  const partesActivas = computed(() =>
    IDS_PARTES.filter((parte) =>
      catalogo.productos.some((producto) => {
        if (parteDeProducto(producto) !== parte) return false
        if (parte === 'piernas' && esCuerpoCompleto(producto)) return false

        return precioMasBajo(producto) !== null
      }),
    ),
  )

  const partesListas = computed(
    () =>
      partesActivas.value.filter((id) => {
        const producto = prendas.value[id]

        return producto !== null && ofertaPara(producto, id) !== null
      }).length,
  )

  // Una ranura deja de estar lista si la persona cambia a una talla que esa
  // prenda no tiene. Así el progreso y el autocompletado no dan por válido un
  // outfit cuyo total omite piezas sin stock.
  const completo = computed(
    () => partesActivas.value.length > 0 && partesListas.value === partesActivas.value.length,
  )

  // ——— dinero ———

  // Cada pieza en la tienda donde está más barata. precioMasBajo ya descarta
  // las agotadas, así que una oferta que no se puede comprar no cuenta.
  const desglose = computed(() =>
    piezas.value.map(({ parte, producto }) => ({
      parte,
      producto,
      oferta: ofertaPara(producto, parte),
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

      const tieneTodo = piezas.value.every(({ parte, producto }) => {
        const talla = tallaParaParte(parte)
        const oferta = producto.precios
          .filter(
            (o) =>
              o.tienda === tienda.id &&
              o.stock &&
              (talla === '' || (o.tallas ?? []).includes(talla)),
          )
          .sort((a, b) => a.precio - b.precio)[0]

        if (!oferta) return false

        suma += oferta.precio

        return true
      })

      if (tieneTodo) candidatas.set(tienda.id, suma)
    })

    if (candidatas.size === 0) return null

    const [tienda, precio] = [...candidatas.entries()].sort((a, b) => a[1] - b[1])[0]

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

    const producto = catalogo.productoById(productoId)

    if (!producto || !opcionesPara(parte).some((opcion) => opcion.id === productoId)) {
      return
    }

    seleccion.value = { ...seleccion.value, [parte]: productoId }

    // Al poner una prenda de cuerpo completo se limpia lo que hubiera en las
    // partes que cubre, para no dejar una selección contradictoria guardada.
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

  // Completa sólo las ranuras vacías o incompatibles con la talla elegida.
  // Respeta lo que la persona ya eligió y usa, para cada parte, la prenda con
  // la oferta comprable más barata.
  function completarConMasBarato() {
    IDS_PARTES.forEach((parte) => {
      if (partesBloqueadas.value.includes(parte)) return

      const actual = prendas.value[parte]

      if (actual && ofertaPara(actual, parte)) return

      const candidata = opcionesPara(parte)
        .map((producto) => ({ producto, oferta: ofertaPara(producto, parte) }))
        .filter(({ oferta }) => oferta !== null)
        .sort((a, b) => a.oferta.precio - b.oferta.precio)[0]

      if (candidata) ponerPrenda(parte, candidata.producto.id)
    })
  }

  function ponerOutfit(porParte) {
    const nuevo = vacio()

    IDS_PARTES.forEach((id) => {
      const producto = catalogo.productoById(porParte?.[id])

      if (producto && ofertaPara(producto, id)) nuevo[id] = producto.id
    })

    seleccion.value = nuevo
  }

  return {
    seleccion,
    tallaRopa,
    tallaCalzado,
    tallasRopa,
    tallasCalzado,
    prendas,
    piezas,
    partesBloqueadas,
    partesActivas,
    completo,
    partesListas,
    desglose,
    total,
    tiendasImplicadas,
    mejorTiendaUnica,
    ahorroRepartiendo,
    opcionesPara,
    tallaParaParte,
    ofertaPara,
    ponerPrenda,
    quitarPrenda,
    limpiar,
    completarConMasBarato,
    ponerOutfit,
  }
})
