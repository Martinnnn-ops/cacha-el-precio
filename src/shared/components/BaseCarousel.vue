<script setup>
import { onBeforeUnmount, onMounted, ref, useTemplateRef } from 'vue'

// Carrusel horizontal con scroll-snap.
//
// El número de elementos se cuenta sobre los hijos reales del carril y no sobre
// los vnodes del slot: un v-for dentro de un slot llega como UN solo fragmento,
// así que slots.default().length valdría 1 siempre y la flecha derecha quedaría
// deshabilitada para siempre. El MutationObserver hace falta porque el catálogo
// llega de forma asíncrona: los elementos aparecen después del montaje.

const carril = useTemplateRef('carril')
const activo = ref(0)
const total = ref(0)

let observador = null

function sincronizar() {
  total.value = carril.value?.children.length ?? 0

  if (activo.value > total.value - 1) {
    activo.value = Math.max(0, total.value - 1)
  }
}

function irA(indice) {
  const el = carril.value
  const hijo = el?.children[indice]

  if (!el || !hijo) return

  el.scrollTo({ left: hijo.offsetLeft - el.offsetLeft, behavior: 'smooth' })
}

function alDesplazar() {
  const el = carril.value

  if (!el) return

  let cercano = 0
  let distancia = Infinity

  Array.from(el.children).forEach((hijo, i) => {
    const d = Math.abs(hijo.offsetLeft - el.scrollLeft - el.offsetLeft)

    if (d < distancia) {
      distancia = d
      cercano = i
    }
  })

  activo.value = cercano
}

onMounted(() => {
  sincronizar()

  if (carril.value) {
    observador = new MutationObserver(sincronizar)
    observador.observe(carril.value, { childList: true })
  }
})

onBeforeUnmount(() => {
  observador?.disconnect()
  observador = null
})
</script>

<template>
  <div class="carrusel">
    <button
      type="button"
      class="carrusel__flecha carrusel__flecha--izq"
      aria-label="Anterior"
      :disabled="activo === 0 || total === 0"
      @click="irA(Math.max(0, activo - 1))"
    >
      <svg viewBox="0 0 24 24" width="18" height="18" fill="none" aria-hidden="true">
        <path d="m15 5-7 7 7 7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
      </svg>
    </button>

    <div ref="carril" class="carrusel__carril" @scroll="alDesplazar">
      <slot />
    </div>

    <button
      type="button"
      class="carrusel__flecha carrusel__flecha--der"
      aria-label="Siguiente"
      :disabled="total === 0 || activo >= total - 1"
      @click="irA(Math.min(total - 1, activo + 1))"
    >
      <svg viewBox="0 0 24 24" width="18" height="18" fill="none" aria-hidden="true">
        <path d="m9 5 7 7-7 7" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
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

.carrusel__flecha {
  position: absolute;
  top: 40%;
  transform: translateY(-50%);
  z-index: 2;
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
.carrusel__flecha--izq {
  left: 2px;
}
.carrusel__flecha--der {
  right: 2px;
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
  margin-top: var(--cep-sp-25);
}
.carrusel__punto {
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
</style>
