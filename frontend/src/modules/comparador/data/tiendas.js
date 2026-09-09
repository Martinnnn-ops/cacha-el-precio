// Catálogo de tiendas que raspa el comparador. El id es el que viaja en la API
// y en el query string; el nombre es sólo para mostrar.
export const TIENDAS = [
  { id: 'ripley', nombre: 'Ripley', color: '#6b2d8c' },
  { id: 'paris', nombre: 'Paris', color: '#0b5cad' },
  { id: 'zara', nombre: 'Zara', color: '#2b2b2b' },
  { id: 'hym', nombre: 'H&M', color: '#c0392b' },
  { id: 'mango', nombre: 'Mango', color: '#8a6a2f' },
]

export const TIENDAS_POR_ID = Object.fromEntries(
  TIENDAS.map((tienda) => [tienda.id, tienda]),
)

export function nombreTienda(id) {
  return TIENDAS_POR_ID[id]?.nombre ?? id
}

export function colorTienda(id) {
  return TIENDAS_POR_ID[id]?.color ?? 'var(--cep-muted)'
}
