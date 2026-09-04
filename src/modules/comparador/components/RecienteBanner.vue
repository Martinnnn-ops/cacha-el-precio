<script setup>
import { computed } from 'vue'
import { RouterLink } from 'vue-router'

import { formatearPrecio } from '@/shared/utils/formato'
import { precioMasBajo } from '@/shared/utils/precios'
import PrendaArt from '@/shared/components/PrendaArt.vue'

const props = defineProps({
  producto: { type: Object, required: true },
  indice: { type: Number, default: 0 },
})

const desde = computed(() => precioMasBajo(props.producto))

const antiguedad = computed(() => {
  const d = props.producto.agregadoHace

  if (d === 0) return 'hoy'
  if (d === 1) return 'hace 1 día'

  return `hace ${d} días`
})
</script>

<template>
  <RouterLink
    class="banner"
    :class="`acento-${indice % 3}`"
    :to="{ name: 'producto-detalle', params: { id: producto.id } }"
  >
    <div class="banner__texto">
      <p class="mono banner__eyebrow">Recién agregado · {{ antiguedad }}</p>

      <p class="display banner__titulo">{{ producto.nombre }}</p>

      <p class="mono banner__sub">
        {{ producto.marca }} ·
        <template v-if="desde">desde {{ formatearPrecio(desde.precio) }}</template>
        <template v-else>sin stock</template>
      </p>

      <span class="banner__cta">Ver ficha</span>
    </div>

    <div class="banner__arte">
      <PrendaArt :alto="104" trazo="rgba(255,255,255,0.8)" :fondo="false" />
    </div>
  </RouterLink>
</template>

<style scoped>
.banner {
  position: relative;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  width: 288px;
  height: 168px;
  padding: var(--cep-sp-5);
  border-radius: var(--cep-r-lg);
  overflow: hidden;
  color: #fff;
  text-decoration: none;
  transition:
    box-shadow var(--cep-dur-2) var(--cep-ease),
    transform var(--cep-dur-2) var(--cep-ease);
}
.banner:hover {
  box-shadow: var(--cep-shadow-2);
  transform: translateY(-2px);
}
.banner__texto {
  position: relative;
  z-index: 1;
  max-width: 76%;
}
.banner__eyebrow {
  margin: 0;
  font-size: var(--cep-fs-2xs);
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: rgb(255 255 255 / 80%);
}
.banner__titulo {
  margin: var(--cep-sp-1) 0 var(--cep-sp-15);
  font-size: var(--cep-fs-lg);
  line-height: var(--cep-lh-tight);
}
.banner__sub {
  margin: 0;
  font-size: var(--cep-fs-xs);
  color: rgb(255 255 255 / 85%);
}
.banner__cta {
  display: inline-block;
  align-self: flex-start;
  margin-top: var(--cep-sp-25);
  padding: var(--cep-sp-2) var(--cep-sp-3);
  background: #fff;
  color: var(--cep-ink);
  border-radius: var(--cep-r-pill);
  font-family: var(--cep-font-display);
  font-weight: 700;
  font-size: var(--cep-fs-xs);
}
.banner__arte {
  position: absolute;
  right: -10px;
  bottom: -12px;
  opacity: 0.9;
}
</style>
