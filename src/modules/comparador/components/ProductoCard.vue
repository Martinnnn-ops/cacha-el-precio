<script setup>
import { computed } from 'vue'

import { useTiendas } from '@/modules/comparador/composables/useTiendas'
import { formatearPrecio } from '@/shared/utils/formato'
import { ahorroMaximo, descuento, precioMasBajo } from '@/shared/utils/precios'
import BaseTicket from '@/shared/components/BaseTicket.vue'
import BotonComparar from '@/modules/comparar/components/BotonComparar.vue'
import EnlaceTienda from '@/shared/components/EnlaceTienda.vue'
import PrendaArt from '@/shared/components/PrendaArt.vue'

const props = defineProps({
  producto: { type: Object, required: true },
  // Texto de la esquina: la portada lo usa para "12.450 visitas" o
  // "Ahorra $4.000" según la sección que la muestre.
  insignia: { type: String, default: '' },
  // En el carrusel la tarjeta va estrecha y sin el desglose completo.
  compacta: { type: Boolean, default: false },
})

const { nombreTienda, colorTienda } = useTiendas()

const masBarato = computed(() => precioMasBajo(props.producto))
const ahorro = computed(() => ahorroMaximo(props.producto))

// Ofertas de menor a mayor, con las agotadas al final.
const ofertas = computed(() =>
  [...(props.producto.precios ?? [])].sort((a, b) => {
    if (a.stock !== b.stock) return a.stock ? -1 : 1

    return a.precio - b.precio
  }),
)

const visibles = computed(() =>
  props.compacta ? ofertas.value.slice(0, 3) : ofertas.value,
)

const ocultas = computed(() => ofertas.value.length - visibles.value.length)

// Con una sola oferta no hay comparación: sobra decir "en tal tienda" y sobra
// el desglose, que sería esa misma línea repetida debajo.
const comparando = computed(() => ofertas.value.length > 1)

// La API no guarda marca: sin este guard salía " · Poleras" con el separador
// colgando al principio de la tarjeta.
const subtitulo = computed(() =>
  [props.producto.marca, props.producto.categoria].filter(Boolean).join(' · '),
)
</script>

<template>
  <BaseTicket
    class="tarjeta"
    :class="{ 'tarjeta--compacta': compacta }"
    :to="{ name: 'producto-detalle', params: { id: producto.id } }"
    :relleno="false"
  >
    <div class="tarjeta__cabecera">
      <div class="tarjeta__arte">
        <img v-if="producto.imagen" :src="producto.imagen" :alt="producto.nombre" />
        <PrendaArt v-else :alto="compacta ? 108 : 140" />

        <span v-if="insignia" class="tarjeta__insignia mono">{{ insignia }}</span>
      </div>

      <p v-if="subtitulo" class="eyebrow tarjeta__marca">{{ subtitulo }}</p>

      <h3 class="display tarjeta__nombre">{{ producto.nombre }}</h3>

      <p v-if="masBarato" class="tarjeta__precio">
        <span class="precio">{{ formatearPrecio(masBarato.precio) }}</span>
        <span v-if="comparando" class="mono tarjeta__en">
          en {{ nombreTienda(masBarato.tienda) }}
        </span>
      </p>

      <p v-else class="mono muted tarjeta__sinstock">
        {{ comparando ? 'Sin stock en las tiendas seleccionadas' : 'No disponible' }}
      </p>
    </div>

    <div v-if="comparando" class="divisor tarjeta__divisor" />

    <ul v-if="comparando" class="tarjeta__ofertas">
      <li
        v-for="oferta in visibles"
        :key="oferta.tienda"
        class="tarjeta__oferta"
        :class="{
          'tarjeta__oferta--mejor': oferta.tienda === masBarato?.tienda,
          'tarjeta__oferta--agotada': !oferta.stock,
        }"
      >
        <span class="tarjeta__punto" :style="{ background: colorTienda(oferta.tienda) }" />

        <span class="tarjeta__tienda">{{ nombreTienda(oferta.tienda) }}</span>

        <span v-if="!oferta.stock" class="mono tarjeta__agotada">Agotado</span>

        <template v-else>
          <span v-if="descuento(oferta)" class="mono tarjeta__descuento">
            −{{ descuento(oferta) }}%
          </span>

          <strong class="mono tarjeta__monto">
            {{ formatearPrecio(oferta.precio) }}
          </strong>
        </template>
      </li>

      <li v-if="ocultas > 0" class="mono muted tarjeta__resto">
        +{{ ocultas }} {{ ocultas === 1 ? 'tienda más' : 'tiendas más' }}
      </li>
    </ul>

    <div class="tarjeta__pie">
      <p v-if="ahorro > 0" class="mono tarjeta__ahorro">
        Ahorras {{ formatearPrecio(ahorro) }} eligiendo bien
      </p>

      <!-- @click.stop dentro de los dos: la tarjeta entera es un enlace a la
           ficha, y pulsar aquí no debe abrir además el detalle. -->
      <BotonComparar :producto-id="producto.id" />

      <EnlaceTienda
        v-if="masBarato"
        :url="masBarato.url"
        :tienda="nombreTienda(masBarato.tienda)"
        tamano="chico"
        @click.stop
      />
    </div>
  </BaseTicket>
</template>

<style scoped>
.tarjeta {
  display: flex;
  flex-direction: column;
}
.tarjeta--compacta {
  width: 260px;
}

.tarjeta__cabecera {
  padding: var(--cep-sp-4) var(--cep-sp-4) 0;
}
.tarjeta__arte {
  position: relative;
  border-radius: var(--cep-r-sm);
  overflow: hidden;
}
.tarjeta__arte img {
  width: 100%;
  height: 140px;
  object-fit: cover;
}
.tarjeta__insignia {
  position: absolute;
  top: var(--cep-sp-15);
  left: var(--cep-sp-15);
  padding: 3px 8px;
  background: var(--cep-ink);
  color: var(--cep-on-ink);
  border-radius: var(--cep-r-sm);
  font-size: var(--cep-fs-2xs);
}

.tarjeta__marca {
  margin-top: var(--cep-sp-3);
}
.tarjeta__nombre {
  margin: 0 0 var(--cep-sp-2);
  font-size: var(--cep-fs-lg);
}
.tarjeta__precio {
  display: flex;
  align-items: baseline;
  gap: var(--cep-sp-2);
  margin: 0;
}
.tarjeta__en {
  font-size: var(--cep-fs-xs);
  color: var(--cep-exito);
}
.tarjeta__sinstock {
  margin: 0;
  font-size: var(--cep-fs-xs);
  font-style: italic;
}

.tarjeta__divisor {
  margin: var(--cep-sp-3) var(--cep-sp-4);
}

.tarjeta__ofertas {
  list-style: none;
  margin: 0;
  padding: 0 var(--cep-sp-4);
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-05);
}
.tarjeta__oferta {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-2);
  padding: var(--cep-sp-15) var(--cep-sp-2);
  border-radius: var(--cep-r-md);
  font-size: var(--cep-fs-sm);
}
.tarjeta__oferta--mejor {
  background: color-mix(in srgb, var(--cep-exito) 12%, transparent);
}
.tarjeta__oferta--mejor .tarjeta__monto {
  color: var(--cep-exito);
}
.tarjeta__oferta--agotada {
  opacity: 0.5;
}
.tarjeta__punto {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex: none;
}
.tarjeta__tienda {
  flex: 1;
}
.tarjeta__descuento {
  font-size: var(--cep-fs-xs);
  font-weight: 600;
  color: var(--cep-alerta);
}
.tarjeta__monto {
  font-variant-numeric: tabular-nums;
}
.tarjeta__agotada {
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}
.tarjeta__resto {
  padding: var(--cep-sp-1) var(--cep-sp-2);
  font-size: var(--cep-fs-xs);
}

.tarjeta__pie {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--cep-sp-2);
  flex-wrap: wrap;
  margin-top: var(--cep-sp-3);
  padding: var(--cep-sp-25) var(--cep-sp-4) var(--cep-sp-4);
}
.tarjeta__ahorro {
  margin: 0;
  font-size: var(--cep-fs-xs);
  color: var(--cep-exito);
}
</style>
