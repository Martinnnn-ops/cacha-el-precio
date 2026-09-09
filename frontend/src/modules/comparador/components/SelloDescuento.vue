<script setup>
import { computed } from 'vue'

// Sello circular con el descuento real, el elemento más reconocible de la
// interfaz. Va inclinado y con doble borde para que parezca estampado sobre el
// ticket, no una etiqueta pegada encima.
//
// El porcentaje NO es el que declara la tienda: es cuánto se aparta el precio
// de hoy del mínimo que hemos visto nunca. Un "−40 %" de vitrina puede ser el
// precio de siempre; esto dice si de verdad está barato.
const props = defineProps({
  // Diferencia contra el mínimo histórico, en porcentaje. 0 = está al mínimo.
  porcentaje: { type: Number, default: null },
  grande: { type: Boolean, default: false },
})

// Tres niveles, con su color y su lectura.
const nivel = computed(() => {
  const p = props.porcentaje

  if (p === null) return { texto: 'SIN DATOS', color: 'var(--cep-muted)' }
  if (p <= 2) return { texto: 'MÍNIMO HISTÓRICO', color: 'var(--cep-exito)' }
  if (p <= 15) return { texto: 'BUEN PRECIO', color: 'var(--cep-rate)' }

  return { texto: `${Math.round(p)}% SOBRE EL MÍNIMO`, color: 'var(--cep-alerta)' }
})
</script>

<template>
  <div
    class="sello"
    :class="{ 'sello--grande': grande }"
    :style="{ color: nivel.color, borderColor: nivel.color }"
    role="img"
    :aria-label="`Precio de hoy: ${nivel.texto.toLowerCase()}`"
  >
    <span class="sello__texto">{{ nivel.texto }}</span>
  </div>
</template>

<style scoped>
.sello {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 96px;
  height: 96px;
  padding: var(--cep-sp-2);
  border: 2.5px solid;
  border-radius: 50%;
  /* La inclinación es lo que lo hace parecer estampado a mano. */
  transform: rotate(-9deg);
  font-family: var(--cep-font-display);
  font-weight: 700;
  font-size: var(--cep-fs-2xs);
  line-height: 1.15;
  letter-spacing: 0.02em;
  text-align: center;
  text-transform: uppercase;
  opacity: 0.92;
}
.sello--grande {
  width: 132px;
  height: 132px;
  font-size: var(--cep-fs-xs);
}
</style>
