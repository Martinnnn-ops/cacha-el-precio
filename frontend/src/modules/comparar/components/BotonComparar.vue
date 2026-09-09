<script setup>
import { computed } from 'vue'

import { useCompararStore } from '@/modules/comparar/store/comparar.store'

// Botón de comparar en cada tarjeta.
//
// Cuando el tope está lleno el botón se atenúa pero SIGUE siendo pulsable y
// enfocable: un `disabled` real lo saca del recorrido de teclado y del lector
// de pantalla, y entonces quien más necesita saber por qué no pasa nada es
// justamente quien no se entera. Al pulsarlo se explica el tope.
const props = defineProps({
  productoId: { type: String, required: true },
})

const comparar = useCompararStore()

const activo = computed(() => comparar.tiene(props.productoId))
const bloqueado = computed(() => comparar.bloqueado(props.productoId))

function pulsar() {
  // El store anota el intento rechazado; la barra lo explica.
  comparar.alternar(props.productoId)
}
</script>

<template>
  <button
    type="button"
    class="comparar"
    :class="{ 'comparar--activo': activo, 'comparar--tope': bloqueado }"
    :aria-pressed="activo"
    :aria-disabled="bloqueado"
    @click.stop.prevent="pulsar"
  >
    <svg viewBox="0 0 20 20" width="13" height="13" fill="none" aria-hidden="true">
      <path
        v-if="activo"
        d="m4 10.5 4 4 8-9"
        stroke="currentColor"
        stroke-width="2.2"
        stroke-linecap="round"
        stroke-linejoin="round"
      />
      <g v-else stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
        <path d="M10 3v14M4 7h12M6 7l-2.5 5h5zM14 7l-2.5 5h5z" />
      </g>
    </svg>

    {{ activo ? 'Comparando' : 'Comparar' }}
  </button>
</template>

<style scoped>
.comparar {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-15);
  min-height: var(--cep-control-h-sm);
  padding: 0 var(--cep-sp-25);
  background: transparent;
  color: var(--cep-ink);
  border: 1px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-pill);
  font-family: var(--cep-font-mono);
  font-size: var(--cep-fs-xs);
  line-height: 1;
  cursor: pointer;
  transition:
    background var(--cep-dur-1) var(--cep-ease),
    border-color var(--cep-dur-1) var(--cep-ease),
    color var(--cep-dur-1) var(--cep-ease);
}
.comparar:hover:not(.comparar--activo) {
  border-color: var(--cep-ink);
  background: var(--cep-wash);
}
.comparar--activo {
  background: var(--cep-accent);
  border-color: var(--cep-accent);
  color: var(--cep-on-accent);
}
.comparar--tope {
  opacity: 0.45;
}
</style>
