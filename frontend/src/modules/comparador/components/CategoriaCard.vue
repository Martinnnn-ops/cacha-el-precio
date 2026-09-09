<script setup>
import { computed } from 'vue'
import { RouterLink } from 'vue-router'

import { lemaCategoria } from '@/modules/comparador/data/categorias'
import PrendaArt from '@/shared/components/PrendaArt.vue'

const props = defineProps({
  categoria: { type: String, required: true },
  cantidad: { type: Number, required: true },
  indice: { type: Number, default: 0 },
})

const lema = computed(() => lemaCategoria(props.categoria, props.cantidad))
</script>

<template>
  <RouterLink
    class="baldosa"
    :to="{ name: 'comparador', query: { categoria } }"
  >
    <div class="baldosa__texto">
      <div>
        <p class="display baldosa__titulo">{{ categoria }}</p>
        <p class="mono baldosa__lema">{{ lema }}</p>
      </div>

      <span class="baldosa__cta">Ver ofertas →</span>
    </div>

    <div class="baldosa__arte" :class="`acento-${indice % 3}`">
      <PrendaArt :alto="86" trazo="rgba(255,255,255,0.85)" :fondo="false" />
    </div>
  </RouterLink>
</template>

<style scoped>
.baldosa {
  display: flex;
  height: 148px;
  border-radius: var(--cep-r-lg);
  overflow: hidden;
  text-decoration: none;
  transition:
    box-shadow var(--cep-dur-2) var(--cep-ease),
    transform var(--cep-dur-2) var(--cep-ease);
}
.baldosa:hover {
  box-shadow: var(--cep-shadow-2);
  transform: translateY(-2px);
}

.baldosa__texto {
  flex: 1.1;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  padding: var(--cep-sp-4) var(--cep-sp-5);
  background: var(--cep-ink);
  color: var(--cep-on-ink);
}
.baldosa__titulo {
  margin: 0;
  font-size: var(--cep-fs-xl);
}
.baldosa__lema {
  margin: var(--cep-sp-1) 0 0;
  font-size: var(--cep-fs-xs);
  line-height: var(--cep-lh-snug);
  opacity: 0.75;
}
.baldosa__cta {
  align-self: flex-start;
  padding: var(--cep-sp-15) var(--cep-sp-3);
  /* currentColor y no un blanco fijo: el día que el bloque cambie de fondo,
     el borde sigue siendo visible. */
  border: 1.5px solid color-mix(in srgb, currentColor 55%, transparent);
  border-radius: var(--cep-r-sm);
  font-family: var(--cep-font-display);
  font-weight: 600;
  font-size: var(--cep-fs-xs);
}

.baldosa__arte {
  flex: 0.9;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
