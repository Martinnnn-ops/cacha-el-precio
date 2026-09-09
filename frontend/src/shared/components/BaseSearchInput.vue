<script setup>
import { ref, useTemplateRef } from 'vue'

// v-model + evento `buscar` al pulsar Enter. Se separan a propósito: escribir
// filtra en vivo, Enter es la acción explícita (navegar, pedir al servidor).
const modelo = defineModel({ type: String, default: '' })

defineProps({
  placeholder: { type: String, default: 'Buscar…' },
  etiqueta: { type: String, default: 'Buscar' },
  id: { type: String, default: 'base-search' },
})

const emit = defineEmits(['buscar'])

const campo = useTemplateRef('campo')
const enfocado = ref(false)

function limpiar() {
  modelo.value = ''
  campo.value?.focus()
}
</script>

<template>
  <div class="buscador" :class="{ 'buscador--activo': enfocado }">
    <label class="sr-only" :for="id">{{ etiqueta }}</label>

    <svg class="buscador__lupa" viewBox="0 0 20 20" aria-hidden="true">
      <circle cx="9" cy="9" r="6" fill="none" stroke="currentColor" stroke-width="2" />
      <path d="M13.5 13.5 18 18" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
    </svg>

    <input
      :id="id"
      ref="campo"
      v-model="modelo"
      type="search"
      class="buscador__campo"
      :placeholder="placeholder"
      @focus="enfocado = true"
      @blur="enfocado = false"
      @keydown.enter="emit('buscar', modelo.trim())"
      @keydown.esc="limpiar"
    />

    <button
      v-if="modelo"
      type="button"
      class="buscador__limpiar"
      aria-label="Limpiar búsqueda"
      @click="limpiar"
    >
      ✕
    </button>
  </div>
</template>

<style scoped>
.buscador {
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 44px;
  padding: 0 14px;
  background: var(--cep-surface);
  border: 1.5px solid var(--cep-line-fuerte);
  border-radius: 8px;
  transition:
    border-color 0.12s ease,
    box-shadow 0.12s ease;
}
.buscador--activo {
  border-color: var(--cep-accent);
  box-shadow: 0 0 0 3px var(--cep-wash);
}
.buscador__lupa {
  width: 18px;
  height: 18px;
  flex: none;
  color: var(--cep-muted);
}
.buscador__campo {
  flex: 1;
  min-width: 0;
  /* Estira el campo a los 44px de la caja. Sin esto el <input> medía 24px de
     alto y quedaba centrado: los 10px de arriba y de abajo parecían parte del
     buscador pero al tocarlos no pasaba nada. */
  align-self: stretch;
  border: none;
  outline: none;
  background: none;
  font: inherit;
  color: var(--cep-ink);
}
/* Con el dedo la caja sube a 47: el borde de 1.5px se come casi 3, y sin esto
   el campo de dentro se quedaba en 42 y no llegaba a los 44 de mínimo. */
@media (pointer: coarse) {
  .buscador {
    min-height: 47px;
  }
}
.buscador__campo::-webkit-search-cancel-button {
  display: none;
}
.buscador__limpiar {
  flex: none;
  width: 24px;
  height: 24px;
  border: none;
  border-radius: 50%;
  background: none;
  color: var(--cep-muted);
  cursor: pointer;
  line-height: 1;
}
.buscador__limpiar:hover {
  background: var(--cep-wash);
  color: var(--cep-ink);
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}
</style>
