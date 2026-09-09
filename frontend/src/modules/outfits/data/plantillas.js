// Outfits prehechos, en las dos formas.
//
// AUTOMÁTICAS: dicen qué categoría va en cada parte y la app mete la prenda
// más barata disponible. Se mantienen al día solas — si mañana entra una polera
// más barata, el outfit la usa sin que nadie toque nada.
//
// FIJAS: prendas concretas por id, para destacar una combinación elegida a
// mano. Si una prenda deja de existir, esa ranura se marca como no disponible
// en vez de romper el outfit entero.

export const AUTOMATICAS = [
  {
    id: 'economico',
    nombre: 'Lo más barato',
    descripcion: 'Vestir completo pagando lo mínimo, sea de la tienda que sea.',
    // Sin categorías: coge lo más barato de cada parte, venga de donde venga.
    porParte: {},
  },
  {
    id: 'oficina',
    nombre: 'Para la oficina',
    descripcion: 'Camisa, pantalón y zapatos. Sobrio y sin gastar de más.',
    porParte: {
      torso: ['Camisas', 'Chalecos'],
      piernas: ['Pantalones'],
      pies: ['Zapatos'],
    },
  },
  {
    id: 'finde',
    nombre: 'Fin de semana',
    descripcion: 'Polera, jeans y zapatillas. El uniforme de no hacer nada.',
    porParte: {
      cabeza: ['Jockeys', 'Gorros'],
      torso: ['Poleras'],
      piernas: ['Pantalones'],
      pies: ['Zapatillas'],
    },
  },
  {
    id: 'frio',
    nombre: 'Para el frío',
    descripcion: 'Abrigo, gorro y calzado cerrado para los días malos.',
    porParte: {
      cabeza: ['Gorros'],
      torso: ['Abrigos', 'Chaquetas'],
      piernas: ['Pantalones'],
      pies: ['Zapatos', 'Zapatillas'],
    },
  },
]

// Combinaciones elegidas a mano. Los ids son los del catálogo.
export const FIJAS = [
  {
    id: 'urbano-clasico',
    nombre: 'Urbano clásico',
    descripcion: 'La combinación que más se arma en el comparador.',
    prendas: {
      cabeza: '8',
      torso: '1',
      piernas: '2',
      pies: '9',
    },
  },
]
