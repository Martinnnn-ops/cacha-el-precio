// Las capas de un outfit y qué categorías del catálogo van en cada una.
//
// El mapa es por nombre de categoría porque es lo que devuelve la API
// (campo `category`). Una categoría que llegue y no esté aquí simplemente no aparece
// en ninguna ranura: se sigue pudiendo buscar desde el comparador, pero no
// entra en el armador hasta que se le asigne un sitio.

export const PARTES = [
  {
    id: 'cabeza',
    nombre: 'Cabeza',
    categorias: ['Gorros', 'Jockeys', 'Sombreros'],
  },
  {
    id: 'torso-base',
    nombre: 'Torso · capa base',
    categorias: ['Poleras', 'Camisas', 'Blusas'],
  },
  {
    id: 'torso-abrigo',
    nombre: 'Torso · abrigo',
    categorias: ['Polerones', 'Chaquetas', 'Abrigos', 'Chalecos'],
  },
  {
    id: 'interior',
    nombre: 'Ropa interior',
    categorias: ['Ropa interior masculina', 'Ropa interior femenina', 'Ropa interior unisex'],
  },
  {
    id: 'piernas',
    nombre: 'Piernas',
    categorias: ['Pantalones', 'Jeans', 'Calzas', 'Faldas', 'Shorts'],
  },
  {
    id: 'calcetines',
    nombre: 'Calcetines',
    categorias: ['Calcetines', 'Medias'],
  },
  {
    id: 'calzado',
    nombre: 'Calzado',
    categorias: ['Zapatillas', 'Zapatos', 'Botas', 'Sandalias', 'Pantuflas'],
  },
]

export const IDS_PARTES = PARTES.map((p) => p.id)

// Prendas que cubren dos partes a la vez. Sin esto, un vestido o se queda
// fuera del armador o permite outfits imposibles (vestido + pantalón).
export const CUERPO_COMPLETO = {
  categorias: ['Vestidos', 'Enterizos', 'Overoles', 'Conjuntos', 'Pijamas', 'Trajes de baño'],
  // Va en la capa base; un abrigo todavía se puede llevar por encima.
  ocupa: ['torso-base', 'piernas'],
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
  PARTE_POR_CATEGORIA.set(categoria.toLowerCase(), 'torso-base')
})

/** Parte a la que pertenece una categoría, o null si no está mapeada. */
export function parteDeCategoria(categoria) {
  if (typeof categoria !== 'string') return null

  return PARTE_POR_CATEGORIA.get(categoria.trim().toLowerCase()) ?? null
}

const PARTE_POR_ZONA = new Map([
  ['cabeza', 'cabeza'],
  ['torso', 'torso-base'],
  ['piernas', 'piernas'],
  ['pies', 'calzado'],
  ['cuerpo completo', 'torso-base'],
])

const PARTE_POR_CAPA = new Map([
  ['base', 'torso-base'],
  ['abrigo', 'torso-abrigo'],
  ['ropa interior', 'interior'],
  ['inferior', 'piernas'],
  ['calceteria', 'calcetines'],
  ['calzado', 'calzado'],
  ['entero', 'torso-base'],
])

/** Usa la capa explícita del backend y conserva categoría/zona como respaldo. */
export function parteDeProducto(producto) {
  const capa =
    typeof producto?.capa === 'string'
      ? producto.capa
          .normalize('NFD')
          .replace(/[\u0300-\u036f]/g, '')
          .trim()
          .toLowerCase()
      : ''
  const zona = typeof producto?.zona === 'string' ? producto.zona.trim().toLowerCase() : ''

  return (
    PARTE_POR_CAPA.get(capa) ??
    parteDeCategoria(producto?.categoria) ??
    PARTE_POR_ZONA.get(zona) ??
    null
  )
}

/** ¿Esta prenda cubre torso y piernas a la vez? */
export function esCuerpoCompleto(producto) {
  if (producto?.zona?.trim?.().toLowerCase() === 'cuerpo completo') return true

  const categoria = producto?.categoria

  if (typeof categoria !== 'string') return false

  return CUERPO_COMPLETO.categorias.some((c) => c.toLowerCase() === categoria.trim().toLowerCase())
}

/** Partes que ocupa una prenda: una, o dos si es de cuerpo completo. */
export function partesQueOcupa(producto) {
  if (esCuerpoCompleto(producto)) return [...CUERPO_COMPLETO.ocupa]

  const parte = parteDeProducto(producto)

  return parte ? [parte] : []
}

export function nombreParte(id) {
  return PARTES.find((p) => p.id === id)?.nombre ?? id
}
