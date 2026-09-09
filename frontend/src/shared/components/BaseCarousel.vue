<script setup>
import { computed, onBeforeUnmount, onMounted, ref, useTemplateRef } from 'vue'

import {
  MARGEN,
  UMBRAL_ARRASTRE,
  estadoDesplazamiento,
  fueArrastre,
  indiceActivo,
  pasoDeFlecha,
} from '@/shared/utils/carrusel'

// Carrusel horizontal con scroll-snap, flechas, puntos y arrastre con el ratón.
//
// El número de elementos se cuenta sobre los hijos reales del carril y no sobre
// los vnodes del slot: un v-for dentro de un slot llega como UN solo fragmento,
// así que slots.default().length valdría 1 siempre. El MutationObserver hace
// falta porque el catálogo llega de forma asíncrona: los elementos aparecen
// después del montaje.

const carril = useTemplateRef('carril')
const activo = ref(0)
const total = ref(0)

// Estado de desplazamiento REAL, en píxeles. Es la corrección importante de
// este componente: antes las flechas se habilitaban comparando índices
// (`activo >= total - 1`), y eso está mal en cuanto se ve más de una tarjeta a
// la vez. El último elemento no puede colocarse pegado al borde izquierdo
// —no queda contenido detrás para empujarlo—, así que `activo` nunca llegaba a
// `total - 1`: la flecha derecha se quedaba habilitada para siempre y al
// pulsarla no pasaba nada. Con los píxeles no hay ambigüedad: o queda sitio
// para desplazarse o no queda.
const desplazado = ref(0)
const desplazable = ref(0)

const puedeIzquierda = computed(() => desplazado.value > MARGEN)
const puedeDerecha = computed(() => desplazado.value < desplazable.value - MARGEN)

// Si cabe todo, las flechas y los puntos sobran: son ruido encima de las
// tarjetas y un control desactivado que nadie va a poder usar.
const hayDesbordamiento = computed(() => desplazable.value > MARGEN)

let observador = null
let observadorTamano = null

function medir() {
  const el = carril.value

  if (!el) return

  total.value = el.children.length
  leerPosicion(el)
  situarActivo()
}

function leerPosicion(el) {
  desplazado.value = el.scrollLeft
  desplazable.value = estadoDesplazamiento(el).desplazable
}

// Qué punto se enciende. En los extremos se fuerza el primero y el último: si
// no, los últimos puntos no se encenderían jamás, por el mismo motivo que la
// flecha derecha no se apagaba.
function situarActivo() {
  const el = carril.value

  if (!el) return

  activo.value = indiceActivo({
    posiciones: Array.from(el.children).map((h) => h.offsetLeft - el.offsetLeft),
    scrollLeft: el.scrollLeft,
    puedeIzquierda: puedeIzquierda.value,
    puedeDerecha: puedeDerecha.value,
  })
}

function irA(indice) {
  const el = carril.value
  const hijo = el?.children[indice]

  if (!el || !hijo) return

  el.scrollTo({ left: hijo.offsetLeft - el.offsetLeft, behavior: 'smooth' })
}

// Las flechas mueven una tarjeta, pero desde la posición REAL, no desde el
// índice activo: al final del carril el índice se queda atascado y la flecha
// izquierda daba un salto raro hacia atrás.
function mover(sentido) {
  const el = carril.value

  if (!el) return

  const paso = pasoDeFlecha(
    el.children[0]?.getBoundingClientRect().width ?? 0,
    parseFloat(getComputedStyle(el).columnGap) || 0,
    el.clientWidth,
  )

  el.scrollBy({ left: sentido * paso, behavior: 'smooth' })
}

function alDesplazar() {
  const el = carril.value

  if (!el) return

  leerPosicion(el)
  situarActivo()
}

/* ——————————————— arrastre con el ratón ———————————————
   El táctil se queda con el desplazamiento nativo: va más suave que cualquier
   cosa que hagamos a mano y no hay que tocarlo. Esto es sólo para ratón y
   lápiz, donde `overflow-x: auto` no ofrece forma de arrastrar. */

const arrastrando = ref(false)

// Gesto empezado pero todavía sin decidir si es arrastre o selección.
let pendiente = false
let inicioX = 0
let inicioY = 0
let inicioScroll = 0
let recorrido = 0
let puntero = null

function alPulsar(e) {
  // Siempre se reinicia, aunque este gesto no vaya a arrastrar nada: si no, un
  // arrastre anterior dejaba `recorrido` alto y el siguiente clic legítimo se
  // anulaba sin motivo.
  recorrido = 0

  if (e.pointerType === 'touch' || e.button !== 0) return

  const el = carril.value

  if (!el || !hayDesbordamiento.value) return

  // OJO: aquí NO se captura el puntero ni se anula nada.
  //
  // Capturar en el `pointerdown` es lo que impedía seleccionar el texto de las
  // tarjetas: el navegador deja de recibir el gesto y no puede extender la
  // selección. Se apunta el punto de partida y se espera a ver qué hace la
  // mano. Así siguen funcionando el clic, el doble clic —que selecciona la
  // palabra— y el triple clic.
  pendiente = true
  inicioX = e.clientX
  inicioY = e.clientY
  inicioScroll = el.scrollLeft
  puntero = e.pointerId
}

function alArrastrar(e) {
  const el = carril.value

  if (!el) return

  const dx = e.clientX - inicioX
  const dy = e.clientY - inicioY

  // Se decide una sola vez por gesto: sólo un movimiento claramente horizontal
  // cuenta como arrastre del carrusel. Uno vertical o corto se le deja al
  // navegador, que es quien sabe seleccionar.
  if (pendiente && !arrastrando.value) {
    if (Math.abs(dx) <= UMBRAL_ARRASTRE || Math.abs(dx) <= Math.abs(dy)) return

    arrastrando.value = true
    pendiente = false

    // Lo que hubiera seleccionado el navegador en esos primeros píxeles ya no
    // vale: el gesto era para desplazar.
    window.getSelection?.()?.removeAllRanges()

    // Ahora sí: la captura mantiene los eventos en el carril aunque el ratón se
    // salga de él. Sin esto, al arrastrar rápido y salirse, el carrusel se
    // quedaba pegado al puntero para siempre.
    el.setPointerCapture?.(e.pointerId)
  }

  if (!arrastrando.value) return

  recorrido = Math.max(recorrido, Math.abs(dx))
  el.scrollLeft = inicioScroll - dx
}

function alSoltar() {
  pendiente = false

  if (!arrastrando.value) return

  arrastrando.value = false

  const el = carril.value

  if (el && puntero !== null && el.hasPointerCapture?.(puntero)) {
    el.releasePointerCapture(puntero)
  }

  puntero = null
}

// Un arrastre termina en `click` sobre la tarjeta que quedara debajo del dedo.
// Sin esto, soltar el ratón después de deslizar abría una ficha al azar. Va en
// fase de captura para llegar antes que el RouterLink de la tarjeta.
function alHacerClic(e) {
  if (!fueArrastre(recorrido)) return

  e.preventDefault()
  e.stopPropagation()
  recorrido = 0
}

onMounted(() => {
  medir()

  if (!carril.value) return

  observador = new MutationObserver(medir)
  observador.observe(carril.value, { childList: true })

  // Al cambiar el ancho cambia cuánto queda por desplazar: sin esto, las
  // flechas se quedaban con el estado del tamaño anterior.
  if (typeof ResizeObserver !== 'undefined') {
    observadorTamano = new ResizeObserver(medir)
    observadorTamano.observe(carril.value)
  }
})

onBeforeUnmount(() => {
  observador?.disconnect()
  observador = null
  observadorTamano?.disconnect()
  observadorTamano = null
})
</script>

<template>
  <div class="carrusel">
    <div
      ref="carril"
      class="carrusel__carril"
      :class="{ 'carrusel__carril--arrastrable': hayDesbordamiento,
                'carrusel__carril--arrastrando': arrastrando }"
      @scroll="alDesplazar"
      @pointerdown="alPulsar"
      @pointermove="alArrastrar"
      @pointerup="alSoltar"
      @pointercancel="alSoltar"
      @click.capture="alHacerClic"
      @dragstart.prevent
    >
      <slot />
    </div>

    <!-- Mando único debajo del carril: flecha, puntos, flecha.
         Antes las flechas iban superpuestas sobre las tarjetas (left/right: 2px)
         y la de la izquierda se sentaba justo encima del título de la primera.
         Con estas tarjetas el texto empieza pegado al borde izquierdo, así que
         cualquier cosa flotando ahí tapa contenido. Aquí no tapan nada y de
         paso aprovechan la fila de los puntos, que estaba medio vacía. -->
    <div v-if="hayDesbordamiento" class="carrusel__mando">
      <button
        type="button"
        class="carrusel__flecha"
        aria-label="Anterior"
        :disabled="!puedeIzquierda"
        @click="mover(-1)"
      >
        <svg viewBox="0 0 24 24" width="18" height="18" fill="none" aria-hidden="true">
          <path d="m15 5-7 7 7 7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </button>

      <div v-if="total > 1" class="carrusel__puntos">
        <button
          v-for="(_, i) in total"
          :key="i"
          type="button"
          class="carrusel__punto"
          :class="{ 'carrusel__punto--activo': i === activo }"
          :aria-label="`Ir al elemento ${i + 1}`"
          :aria-current="i === activo || undefined"
          @click="irA(i)"
        />
      </div>

      <button
        type="button"
        class="carrusel__flecha"
        aria-label="Siguiente"
        :disabled="!puedeDerecha"
        @click="mover(1)"
      >
        <svg viewBox="0 0 24 24" width="18" height="18" fill="none" aria-hidden="true">
          <path d="m9 5 7 7-7 7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </button>
    </div>
  </div>
</template>

<style scoped>
.carrusel {
  position: relative;
}
.carrusel__carril {
  display: flex;
  gap: var(--cep-sp-4);
  overflow-x: auto;
  scroll-snap-type: x mandatory;
  padding: 2px 2px var(--cep-sp-15);
  scrollbar-width: none;
}
.carrusel__carril::-webkit-scrollbar {
  display: none;
}
.carrusel__carril > * {
  scroll-snap-align: start;
  flex-shrink: 0;
}

/* La manita sólo cuando de verdad hay algo que arrastrar. */
.carrusel__carril--arrastrable {
  cursor: grab;
}
.carrusel__carril--arrastrando {
  cursor: grabbing;
  /* El anclaje pelea con el arrastre: cada píxel movido tira de vuelta al punto
     de anclaje y el carril va a tirones. Se apaga mientras se arrastra y al
     soltar vuelve, que es lo que da el enganche final. */
  scroll-snap-type: none;
  /* Sin esto, arrastrar sobre las tarjetas selecciona su texto. */
  user-select: none;
}

.carrusel__mando {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--cep-sp-3);
  margin-top: var(--cep-sp-3);
}

.carrusel__flecha {
  flex: none;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  padding: 0;
  background: var(--cep-surface);
  color: var(--cep-ink);
  border: 1px solid var(--cep-line-media);
  border-radius: 50%;
  box-shadow: var(--cep-shadow-2);
  cursor: pointer;
  transition:
    border-color var(--cep-dur-1) var(--cep-ease),
    color var(--cep-dur-1) var(--cep-ease),
    opacity var(--cep-dur-1) var(--cep-ease);
}
.carrusel__flecha:hover:not(:disabled) {
  border-color: var(--cep-accent);
  color: var(--cep-accent);
}
.carrusel__flecha:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.carrusel__puntos {
  display: flex;
  justify-content: center;
  gap: var(--cep-sp-15);
}
.carrusel__punto {
  position: relative;
  width: 7px;
  height: 7px;
  padding: 0;
  border: none;
  border-radius: 50%;
  background: var(--cep-line-fuerte);
  cursor: pointer;
  transition:
    background var(--cep-dur-2) var(--cep-ease),
    width var(--cep-dur-2) var(--cep-ease);
}
.carrusel__punto:hover {
  background: var(--cep-muted);
}
.carrusel__punto--activo {
  width: 18px;
  border-radius: var(--cep-r-md);
  background: var(--cep-accent);
}

/* El punto se ve de 7px pero se toca en 44x44.
   La zona la pone un pseudo-elemento invisible centrado encima: agrandar el
   punto de verdad convertiría una fila discreta de puntitos en una hilera de
   botones enormes. Los puntos siguen separados por su `gap`, así que las zonas
   se tocan pero no se solapan de forma que una robe el toque de la vecina. */
.carrusel__punto::after {
  content: '';
  position: absolute;
  top: 50%;
  left: 50%;
  width: 100%;
  height: 100%;
  transform: translate(-50%, -50%);
}
@media (pointer: coarse) {
  /* Excepción a la regla global que da 44px de ancho a todo botón: aquí la zona
     tocable ya la pone el ::after de abajo. Sin esta línea el punto de 7px se
     convertía en un óvalo de 44 y la fila de puntitos pasaba a ser una hilera
     de pastillas que además empujaba las flechas fuera de la pantalla. */
  .carrusel__punto {
    min-width: 0;
  }
  .carrusel__punto::after {
    width: 32px;
    height: 44px;
  }
  .carrusel__puntos {
    gap: var(--cep-sp-1);
  }
  .carrusel__flecha {
    width: 44px;
    height: 44px;
  }
}
</style>
