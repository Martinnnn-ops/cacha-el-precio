// Las cuatro partes de un outfit y qué categorías del catálogo van en cada una.
//
// El mapa es por nombre de categoría porque es lo que devuelve la API
// (/catalogos). Una categoría que llegue y no esté aquí simplemente no aparece
// en ninguna ranura: se sigue pudiendo buscar desde el comparador, pero no
// entra en el armador hasta que se le asigne un sitio.

export const PARTES = [
  {
    id: 'cabeza',
    nombre: 'Cabeza',
    categorias: ['Gorros', 'Jockeys', 'Sombreros'],
  },
  {
    id: 'torso',
    nombre: 'Torso',
    categorias: ['Poleras', 'Camisas', 'Chaquetas', 'Abrigos', 'Chalecos'],
  },
  {
    id: 'piernas',
    nombre: 'Piernas',
    categorias: ['Pantalones', 'Faldas', 'Shorts'],
  },
  {
    id: 'pies',
    nombre: 'Pies',
    categorias: ['Zapatillas', 'Zapatos', 'Botas'],
  },
]

export const IDS_PARTES = PARTES.map((p) => p.id)

// Prendas que cubren dos partes a la vez. Sin esto, un vestido o se queda
// fuera del armador o permite outfits imposibles (vestido + pantalón).
export const CUERPO_COMPLETO = {
  categorias: ['Vestidos', 'Enterizos', 'Overoles'],
  // Al poner una de estas en el torso, la ranura de piernas queda cubierta.
  ocupa: ['torso', 'piernas'],
}

const PARTE_POR_CATEGORIA = new Map()

PARTES.forEach((parte) => {
  parte.categorias.forEach((categoria) => {
    PARTE_POR_CATEGORIA.set(categoria.toLowerCase(), parte.id)
  })
})

// Una prenda de cuerpo completo se ofrece en la ranura de torso, que es donde
// la busca la gente, y al elegirla cubre también las piernas.
CUERPO_COMPLETO.categorias.forEach((categoria) => {
  PARTE_POR_CATEGORIA.set(categoria.toLowerCase(), 'torso')
})

/** Parte a la que pertenece una categoría, o null si no está mapeada. */
export function parteDeCategoria(categoria) {
  if (typeof categoria !== 'string') return null

  return PARTE_POR_CATEGORIA.get(categoria.trim().toLowerCase()) ?? null
}

/** ¿Esta prenda cubre torso y piernas a la vez? */
export function esCuerpoCompleto(producto) {
  const categoria = producto?.categoria

  if (typeof categoria !== 'string') return false

  return CUERPO_COMPLETO.categorias.some(
    (c) => c.toLowerCase() === categoria.trim().toLowerCase(),
  )
}

/** Partes que ocupa una prenda: una, o dos si es de cuerpo completo. */
export function partesQueOcupa(producto) {
  if (esCuerpoCompleto(producto)) return [...CUERPO_COMPLETO.ocupa]

  const parte = parteDeCategoria(producto?.categoria)

  return parte ? [parte] : []
}

export function nombreParte(id) {
  return PARTES.find((p) => p.id === id)?.nombre ?? id
}
