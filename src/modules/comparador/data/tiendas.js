// Nombres y colores de las tiendas, para mostrar.
//
// ⚠️ ESTO NO ES LA LISTA DE TIENDAS DEL CATÁLOGO. La lista real se deriva del
// campo `store` de GET /productos, en `adaptarTiendas()`. Aquí solo vive cómo
// se escribe cada una y de qué color se pinta.
//
// La distinción importa porque antes no existía: este archivo tenía una lista
// fija de cinco tiendas —Ripley, Paris, Zara, H&M y Mango— que eran las de los
// datos de ejemplo, y el resto de la aplicación la trataba como si fuera el
// catálogo. Las consecuencias, las tres reales:
//
//   1. La portada mostraba «falabella» en minúscula, porque Falabella no
//      estaba en el mapa y `nombreTienda` devuelve el id cuando no encuentra.
//   2. Zara y Mango aparecían como tiendas del sistema sin tener scraper.
//   3. El filtro arrancaba marcando tiendas que no existen en el catálogo.
//
// Regla para no repetirlo: si necesitas saber QUÉ tiendas hay, pregúntaselo al
// catálogo. Si necesitas saber CÓMO se llama una, pregúntale a este archivo.

// Las ocho que el scraper sabe leer (`_TIENDAS` en scraper-service), más las
// dos que solo existen en los datos de ejemplo.
export const TIENDAS_CONOCIDAS = {
  converse: { nombre: 'Converse', color: '#111111' },
  falabella: { nombre: 'Falabella', color: '#1676b8' },
  hites: { nombre: 'Hites', color: '#d71920' },
  hym: { nombre: 'H&M', color: '#c0392b' },
  lapolar: { nombre: 'La Polar', color: '#e31b23' },
  paris: { nombre: 'Paris', color: '#0b5cad' },
  ripley: { nombre: 'Ripley', color: '#6b2d8c' },
  sparta: { nombre: 'Sparta', color: '#e4572e' },
  // Solo en los datos de ejemplo: no hay scraper para ninguna de las dos.
  mango: { nombre: 'Mango', color: '#8a6a2f' },
  zara: { nombre: 'Zara', color: '#2b2b2b' },
}

// Las tiendas de `productos.mock.js`, que es lo que se ve cuando no hay
// backend configurado (`USAR_MOCK`). No se usan contra la API real.
export const TIENDAS_MOCK = ['ripley', 'paris', 'zara', 'hym', 'mango'].map(
  (id) => ({ id, ...TIENDAS_CONOCIDAS[id] }),
)

export function nombreTienda(id) {
  return TIENDAS_CONOCIDAS[id]?.nombre ?? id
}

export function colorTienda(id) {
  return TIENDAS_CONOCIDAS[id]?.color ?? 'var(--cep-muted)'
}
