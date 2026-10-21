// Detalles de una prenda leídos de su nombre. La API todavía no trae
// características (description llega vacío en todo el catálogo), pero las
// tiendas suelen ponerlas en el nombre: "Polera Manga Corta 100% Algodón
// Cuello Redondo". Sirve para que la comparación diga algo más que el precio.
//
// Solo se reconoce lo que está escrito: si el nombre no lo dice, no aparece.

const REGLAS = [
  ['Manga', [
    [/\bsin mangas?\b|\bmusculosa\b/, 'Sin manga'],
    [/\bmanga corta\b|\bm\/?c\b/, 'Corta'],
    [/\bmanga larga\b|\bm\/?l\b/, 'Larga'],
  ]],
  ['Material', [
    [/\b(\d{2,3}) ?% ?algod[oó]n\b/, (m) => `${m[1]}% algodón`],
    [/\balgod[oó]n\b/, 'Algodón'],
    [/\bpoli[eé]ster\b|\bdry ?fit\b/, 'Poliéster'],
    [/\blino\b/, 'Lino'],
    [/\bdenim\b|\bmezclilla\b/, 'Mezclilla'],
    [/\bcuero\b/, 'Cuero'],
    [/\bfranela\b/, 'Franela'],
  ]],
  ['Cuello', [
    [/\bcuello (?:redondo|r)\b/, 'Redondo'],
    [/\bcuello (?:en )?v\b|\bv-?neck\b/, 'En V'],
    [/\bpolo\b|\bpiqu[eé](?![a-z])/, 'Polo'],
    [/\bcapucha\b|\bhoodie\b/, 'Con capucha'],
  ]],
  ['Calce', [
    [/\boversize\b|\bholgad[ao]\b/, 'Holgado'],
    [/\brelax(?:ed)?(?: fit)?\b/, 'Relajado'],
    [/\bslim(?: fit)?\b|\bentallad[ao]\b/, 'Ajustado'],
    [/\bregular(?: fit)?\b/, 'Regular'],
    [/\bcompresi[oó]n\b/, 'Compresión'],
  ]],
]

/** [{ clave, valor }] con lo que el nombre dice de la prenda. */
export function detallesDeNombre(nombre) {
  const texto = String(nombre ?? '').toLocaleLowerCase('es')

  return REGLAS.flatMap(([clave, casos]) => {
    for (const [patron, valor] of casos) {
      const m = texto.match(patron)
      if (m) return [{ clave, valor: typeof valor === 'function' ? valor(m) : valor }]
    }

    return []
  })
}
