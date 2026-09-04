import { computed } from 'vue'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import { IDS_PARTES, parteDeCategoria } from '@/modules/outfits/data/partes'
import { precioMasBajo } from '@/shared/utils/precios'

// Rellena una plantilla con prendas reales del catálogo.
//
// Vive aquí y no en el store porque la galería muestra varios outfits a la vez
// sin que ninguno sea "el que estoy armando": son cálculos sobre el catálogo,
// no estado de la aplicación.
export function useOutfitArmado() {
  const catalogo = useComparadorStore()

  // La prenda más barata de una parte, opcionalmente acotada a unas categorías.
  function masBarataDe(parte, categorias) {
    const candidatas = catalogo.productos.filter((p) => {
      if (parteDeCategoria(p.categoria) !== parte) return false

      if (!categorias?.length) return true

      return categorias.some(
        (c) => c.toLowerCase() === p.categoria.trim().toLowerCase(),
      )
    })

    // Sin oferta con stock no sirve: no se puede comprar.
    const conPrecio = candidatas
      .map((producto) => ({ producto, oferta: precioMasBajo(producto) }))
      .filter(({ oferta }) => oferta !== null)

    if (conPrecio.length === 0) return null

    return conPrecio.sort((a, b) => a.oferta.precio - b.oferta.precio)[0]
      .producto
  }

  /** Plantilla automática → ids por parte. */
  function armarAutomatica(plantilla) {
    const prendas = {}
    const especifica = Object.keys(plantilla.porParte ?? {}).length > 0

    IDS_PARTES.forEach((parte) => {
      const categorias = plantilla.porParte?.[parte]

      // Una plantilla que nombra categorías sólo usa esas partes; la que no
      // nombra ninguna ("Lo más barato") coge de todas.
      if (especifica && !categorias) return

      const producto = masBarataDe(parte, categorias)

      if (producto) prendas[parte] = producto.id
    })

    return prendas
  }

  /** Plantilla fija → ids por parte, descartando lo que ya no exista. */
  function armarFija(plantilla) {
    const prendas = {}

    IDS_PARTES.forEach((parte) => {
      const id = plantilla.prendas?.[parte]

      if (id && catalogo.productoById(id)) prendas[parte] = id
    })

    return prendas
  }

  function armar(plantilla) {
    return plantilla.prendas ? armarFija(plantilla) : armarAutomatica(plantilla)
  }

  // Resumen de un outfit ya armado, para pintarlo en la galería.
  function resumir(prendasPorParte) {
    const piezas = Object.entries(prendasPorParte)
      .map(([parte, id]) => ({ parte, producto: catalogo.productoById(id) }))
      .filter(({ producto }) => producto !== null)
      .map((pieza) => ({ ...pieza, oferta: precioMasBajo(pieza.producto) }))
      .filter(({ oferta }) => oferta !== null)

    return {
      piezas,
      total: piezas.reduce((suma, { oferta }) => suma + oferta.precio, 0),
      tiendas: new Set(piezas.map(({ oferta }) => oferta.tienda)).size,
      // Cuántas ranuras de la plantilla no se pudieron llenar.
      faltantes: Object.keys(prendasPorParte).length - piezas.length,
    }
  }

  const listo = computed(() => catalogo.productos.length > 0)

  return { armar, resumir, listo, masBarataDe }
}
