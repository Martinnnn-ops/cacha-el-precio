// Las tiendas escriben las tallas cada una a su manera: "M", "3XL", "38",
// "38 CL", "8.5 US". Mezcladas en una sola lista no se encuentra nada, así que
// cada talla se clasifica en un sistema y se ordena dentro de él.

export const GRUPOS_TALLA = [
  { id: 0, nombre: 'Ropa' },
  { id: 1, nombre: 'Calzado' },
  { id: 2, nombre: 'Calzado CL' },
  { id: 3, nombre: 'Calzado US' },
  { id: 4, nombre: 'Otras' },
]

// XXL y 2XL son la misma talla escrita distinto: comparten posición.
const LETRAS = {
  XXS: 0, XS: 1, S: 2, M: 3, L: 4, XL: 5,
  XXL: 6, '2XL': 6, XXXL: 7, '3XL': 7, XXXXL: 8, '4XL': 8, '5XL': 9,
}

/** { grupo, orden } de una talla, según GRUPOS_TALLA. */
export function clasificarTalla(nombre) {
  const t = String(nombre).trim().toUpperCase()

  if (t in LETRAS) return { grupo: 0, orden: LETRAS[t] }

  const sufijo = t.match(/^(\d+(?:[.,]\d+)?)\s*(CL|US)$/)
  if (sufijo) return { grupo: sufijo[2] === 'CL' ? 2 : 3, orden: Number(sufijo[1].replace(',', '.')) }

  const numero = Number(t.replace(',', '.'))
  if (Number.isFinite(numero)) {
    // Bajo 30 son tallas de ropa infantil o de pantalón (4, 6, 8…); de ahí
    // para arriba, calzado europeo.
    return numero < 30 ? { grupo: 0, orden: 100 + numero } : { grupo: 1, orden: numero }
  }

  return { grupo: 4, orden: 0 }
}
