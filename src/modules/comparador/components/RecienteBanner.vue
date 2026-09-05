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
      <!-- Sólo la antigüedad. Antes decía «Recién agregado · hace 1 día», que
           repite el título de la sección («Lo más reciente») y no cabía en una
           línea: rompía en dos y empujaba todo lo de abajo, así que cada
           tarjeta tenía el título a una altura distinta. -->
      <p class="mono banner__eyebrow truncar">{{ antiguedad }}</p>

      <p class="display banner__titulo recorte-2" :title="producto.nombre">
        {{ producto.nombre }}
      </p>

      <p class="mono banner__sub truncar">
        {{ producto.marca }} ·
        <template v-if="desde">desde {{ formatearPrecio(desde.precio) }}</template>
        <template v-else>sin stock</template>
      </p>

      <span class="banner__cta">Ver ficha</span>
    </div>

    <div class="banner__arte">
      <PrendaArt :alto="120" trazo="rgba(255,255,255,0.55)" :fondo="false" />
    </div>
  </RouterLink>
</template>

<style scoped>
.banner {
  position: relative;
  display: flex;
  flex-direction: column;
  width: 288px;
  /* `min-height`, no `height`.
     Con alto fijo y `overflow: hidden`, cualquier texto que creciera se
     recortaba en silencio. Como el carril del carrusel es un flex con
     `align-items: stretch`, todas las tarjetas se igualan solas a la más alta:
     el alto fijo no hacía falta ni para eso. */
  min-height: 168px;
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
/* El texto ocupa la tarjeta entera y se reparte en vertical: epígrafe y título
   arriba, y el botón empujado al fondo con `margin-top: auto`. Así el botón de
   todas las tarjetas queda a la misma altura aunque el título ocupe una línea
   o dos. */
.banner__texto {
  position: relative;
  z-index: 1;
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
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
/* El banner tiene alto fijo (168px) y `overflow: hidden`: sin recortar el
   título, un nombre de cuatro líneas empujaba el botón «Ver ficha» fuera de la
   tarjeta y desaparecía sin dejar rastro. */
.banner__sub {
  margin: 0;
  /* El único hueco que hace falta para la percha. El título va arriba, donde la
     percha no llega, así que reservarle sitio a él sólo servía para estrujarlo:
     medía 148px de ancho y dejaba el título en una columna de 152. */
  padding-right: 84px;
  font-size: var(--cep-fs-xs);
  color: rgb(255 255 255 / 85%);
}
.banner__cta {
  display: inline-block;
  /* Al fondo de la tarjeta, no pegado al texto de arriba. */
  margin-top: auto;
  padding: var(--cep-sp-2) var(--cep-sp-3);
  background: #fff;
  color: var(--cep-ink);
  border-radius: var(--cep-r-pill);
  font-family: var(--cep-font-display);
  font-weight: 700;
  font-size: var(--cep-fs-xs);
}
/* Textura de fondo, no un objeto más de la tarjeta.
   Se sale por la esquina a propósito y va muy tenue: al 0.9 de opacidad y con
   el trazo fuerte competía con el título y parecía que se lo comía. */
.banner__arte {
  position: absolute;
  right: -22px;
  bottom: -26px;
  opacity: 0.3;
  pointer-events: none;
}
</style>
