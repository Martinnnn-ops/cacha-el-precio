// Decisiones del carrusel, sin DOM.
//
// Viven aquí y no dentro del componente porque son justo la parte que se rompió
// y la única que se puede probar con números. El componente se queda con lo que
// sí necesita un navegador: leer medidas y mover el carril.

// El navegador da valores fraccionarios con zoom o en pantallas HiDPI, así que
// `scrollLeft` no llega nunca exactamente al máximo. Sin este margen la flecha
// derecha se quedaba encendida al final por medio píxel.
export const MARGEN = 2

/**
 * Qué se puede hacer desde la posición actual del carril.
 *
 * Se decide con PÍXELES, no con índices. Comparar índices
 * (`activo >= total - 1`) es lo que estaba mal: cuando se ven varias tarjetas a
 * la vez, la última no puede colocarse pegada al borde izquierdo —no queda
 * contenido detrás para empujarla—, así que el índice activo nunca llegaba al
 * último y la flecha derecha no se apagaba jamás.
 */
export function estadoDesplazamiento({ scrollLeft, scrollWidth, clientWidth }) {
  const desplazable = Math.max(0, scrollWidth - clientWidth)

  return {
    desplazable,
    // Si cabe todo, no hay nada que desplazar: sobran flechas y puntos.
    hayDesbordamiento: desplazable > MARGEN,
    puedeIzquierda: scrollLeft > MARGEN,
    puedeDerecha: scrollLeft < desplazable - MARGEN,
  }
}

/**
 * Qué punto se enciende.
 *
 * `posiciones` son los offsets de cada hijo dentro del carril.
 * En los extremos se fuerza el primero y el último: por el mismo motivo que
 * arriba, los últimos puntos no se encenderían nunca si sólo se mirara cuál
 * está más cerca del borde izquierdo.
 */
export function indiceActivo({ posiciones, scrollLeft, puedeIzquierda, puedeDerecha }) {
  if (posiciones.length === 0) return 0
  if (!puedeIzquierda) return 0
  if (!puedeDerecha) return posiciones.length - 1

  let cercano = 0
  let distancia = Infinity

  posiciones.forEach((x, i) => {
    const d = Math.abs(x - scrollLeft)

    if (d < distancia) {
      distancia = d
      cercano = i
    }
  })

  return cercano
}

/**
 * Cuánto mueve una flecha.
 *
 * Una tarjeta más su hueco. Se calcula desde el ancho real y no desde el índice
 * activo porque al final del carril ese índice se queda atascado, y la flecha
 * izquierda daba entonces un salto hacia atrás que no correspondía.
 */
export function pasoDeFlecha(anchoTarjeta, hueco, anchoVisible) {
  const paso = anchoTarjeta > 0 ? anchoTarjeta : anchoVisible

  return paso + (hueco || 0)
}

// Por debajo de esto fue un clic con pulso tembloroso, no un arrastre.
export const UMBRAL_ARRASTRE = 6

/** Si el gesto fue un arrastre, el clic final no debe abrir la tarjeta. */
export function fueArrastre(recorrido) {
  return recorrido > UMBRAL_ARRASTRE
}
