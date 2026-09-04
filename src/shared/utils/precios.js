// Reglas de precio compartidas. Viven en shared/ y no dentro del módulo porque
// un futuro módulo de alertas necesita exactamente el mismo cálculo de "cuál es
// el más barato": duplicarlo es garantizar que un día digan cosas distintas.

// Oferta más barata entre las tiendas con stock. Devuelve null si no hay ninguna.
export function precioMasBajo(producto) {
  const disponibles = (producto?.precios ?? []).filter(
    (p) => p.stock && typeof p.precio === 'number',
  )

  if (disponibles.length === 0) return null

  return disponibles.reduce((mejor, actual) =>
    actual.precio < mejor.precio ? actual : mejor,
  )
}

// Diferencia entre la tienda más cara y la más barata: lo que se ahorra quien
// compara antes de comprar.
export function ahorroMaximo(producto) {
  const disponibles = (producto?.precios ?? []).filter(
    (p) => p.stock && typeof p.precio === 'number',
  )

  if (disponibles.length < 2) return 0

  const valores = disponibles.map((p) => p.precio)

  return Math.max(...valores) - Math.min(...valores)
}

// Descuento declarado por la tienda respecto de su propio precio de lista.
export function descuento(oferta) {
  if (!oferta?.precioLista || oferta.precioLista <= oferta.precio) return 0

  return Math.round((1 - oferta.precio / oferta.precioLista) * 100)
}

/**
 * Precio más bajo que hemos visto nunca, con su fecha.
 *
 * Se calcula del historial en vez de guardarse como un campo: un "mínimo
 * observado" almacenado aparte puede quedarse desfasado respecto de los
 * precios reales, y entonces el sitio miente sin que nadie se entere.
 */
export function minimoHistorico(producto) {
  const puntos = (producto?.historial ?? []).flatMap((punto) =>
    Object.values(punto.precios ?? {}).map((precio) => ({
      precio,
      fecha: punto.fecha,
    })),
  )

  const actuales = (producto?.precios ?? [])
    .filter((o) => o.stock && typeof o.precio === 'number')
    .map((o) => ({ precio: o.precio, fecha: null }))

  const todos = [...puntos, ...actuales].filter(
    (p) => typeof p.precio === 'number',
  )

  if (todos.length === 0) return null

  return todos.reduce((menor, actual) =>
    actual.precio < menor.precio ? actual : menor,
  )
}
