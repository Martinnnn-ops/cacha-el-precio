<script setup>
import { computed } from 'vue'

import { useOutfitArmado } from '@/modules/outfits/composables/useOutfitArmado'
import { nombreParte } from '@/modules/outfits/data/partes'
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'
import { formatearPrecio } from '@/shared/utils/formato'

const props = defineProps({
  plantilla: { type: Object, required: true },
})

defineEmits(['usar'])

const { armar, resumir } = useOutfitArmado()

const prendas = computed(() => armar(props.plantilla))
const resumen = computed(() => resumir(prendas.value))
</script>

<template>
  <BaseTicket class="outfit">
    <p class="eyebrow">{{ plantilla.prendas ? 'Selección propia' : 'Se arma solo' }}</p>

    <h3 class="display outfit__nombre">{{ plantilla.nombre }}</h3>
    <p class="outfit__descripcion">{{ plantilla.descripcion }}</p>

    <ul v-if="resumen.piezas.length > 0" class="outfit__piezas">
      <li v-for="{ parte, producto } in resumen.piezas" :key="parte">
        <span class="mono outfit__parte">{{ nombreParte(parte) }}</span>
        <span class="outfit__prenda">{{ producto.nombre }}</span>
      </li>
    </ul>

    <p v-else class="mono muted outfit__vacio">
      Todavía no tenemos prendas para armar este outfit.
    </p>

    <!-- Si una prenda elegida a mano ya no existe, se dice en vez de enseñar
         un outfit incompleto como si estuviera entero. -->
    <p v-if="resumen.faltantes > 0" class="mono outfit__faltan">
      {{ resumen.faltantes }}
      {{ resumen.faltantes === 1 ? 'prenda ya no está' : 'prendas ya no están' }}
      disponibles.
    </p>

    <div v-if="resumen.piezas.length > 0" class="outfit__pie">
      <div>
        <p class="mono muted outfit__etiqueta">Desde</p>
        <p class="outfit__total">{{ formatearPrecio(resumen.total) }}</p>
        <p class="mono muted outfit__etiqueta">
          en {{ resumen.tiendas }}
          {{ resumen.tiendas === 1 ? 'tienda' : 'tiendas' }}
        </p>
      </div>

      <BaseButton tamano="chico" @click="$emit('usar', prendas)">
        Usar este
      </BaseButton>
    </div>
  </BaseTicket>
</template>

<style scoped>
.outfit {
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-2);
  height: 100%;
}
.outfit__nombre {
  margin: 0;
  font-size: var(--cep-fs-lg);
}
.outfit__descripcion {
  margin: 0;
  font-size: var(--cep-fs-sm);
  color: var(--cep-muted);
}

.outfit__piezas {
  list-style: none;
  margin: var(--cep-sp-2) 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-15);
  flex: 1;
}
.outfit__piezas li {
  display: flex;
  gap: var(--cep-sp-2);
  align-items: baseline;
  font-size: var(--cep-fs-sm);
}
.outfit__parte {
  flex: none;
  width: 62px;
  font-size: var(--cep-fs-2xs);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--cep-muted);
}
.outfit__prenda {
  min-width: 0;
}
.outfit__vacio,
.outfit__faltan {
  margin: var(--cep-sp-2) 0 0;
  font-size: var(--cep-fs-xs);
}
.outfit__faltan {
  color: var(--cep-rate);
}

.outfit__pie {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--cep-sp-3);
  margin-top: var(--cep-sp-4);
  padding-top: var(--cep-sp-3);
  border-top: 1px solid var(--cep-line);
}
.outfit__etiqueta {
  margin: 0;
  font-size: var(--cep-fs-2xs);
}
.outfit__total {
  margin: 0;
  font-family: var(--cep-font-display);
  font-size: var(--cep-fs-xl);
  font-variant-numeric: tabular-nums;
}
</style>
