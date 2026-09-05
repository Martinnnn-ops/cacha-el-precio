<script setup>
import { computed } from 'vue'

import { useTiendas } from '@/modules/comparador/composables/useTiendas'
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'
import PrendaArt from '@/shared/components/PrendaArt.vue'
import { formatearPrecio } from '@/shared/utils/formato'
import { precioMasBajo } from '@/shared/utils/precios'

// Una parte del cuerpo en el armador: vacía, con prenda, o cubierta por una
// prenda de cuerpo completo puesta en otra ranura.
const props = defineProps({
  parte: { type: Object, required: true },
  producto: { type: Object, default: null },
  bloqueada: { type: Boolean, default: false },
  opciones: { type: Number, default: 0 },
})

defineEmits(['elegir', 'quitar'])

const { nombreTienda } = useTiendas()

const oferta = computed(() =>
  props.producto ? precioMasBajo(props.producto) : null,
)
</script>

<template>
  <BaseTicket class="ranura" :class="{ 'ranura--vacia': !producto }" columna>
    <p class="eyebrow ranura__parte">{{ parte.nombre }}</p>

    <!-- cubierta por una prenda de cuerpo completo -->
    <template v-if="bloqueada">
      <div class="ranura__cubierta">
        <p class="ranura__nombre">{{ producto?.nombre }}</p>
        <p class="mono muted ranura__nota">
          Esta parte ya la cubre la prenda del torso.
        </p>
      </div>
    </template>

    <!-- con prenda -->
    <template v-else-if="producto">
      <div class="ranura__prenda">
        <PrendaArt :alto="72" />

        <div class="ranura__datos">
          <p class="ranura__nombre">{{ producto.nombre }}</p>

          <p v-if="oferta" class="mono ranura__precio">
            {{ formatearPrecio(oferta.precio) }}
            <span class="muted">en {{ nombreTienda(oferta.tienda) }}</span>
          </p>

          <p v-else class="mono ranura__agotada">Sin stock ahora mismo</p>
        </div>
      </div>

      <div class="ranura__acciones">
        <BaseButton variante="secundario" tamano="chico" @click="$emit('elegir')">
          Cambiar
        </BaseButton>
        <BaseButton variante="texto" tamano="chico" @click="$emit('quitar')">
          Quitar
        </BaseButton>
      </div>
    </template>

    <!-- vacía -->
    <template v-else>
      <div class="ranura__hueco">
        <PrendaArt :alto="64" :fondo="false" />
      </div>

      <BaseButton
        v-if="opciones > 0"
        variante="secundario"
        tamano="chico"
        bloque
        @click="$emit('elegir')"
      >
        Elegir {{ parte.nombre.toLowerCase() }}
      </BaseButton>

      <!-- Sin prendas de esta parte en el catálogo se dice, en vez de dejar un
           botón que no lleva a ninguna parte. -->
      <p v-else class="mono muted ranura__nota">
        Todavía no tenemos prendas para esta parte.
      </p>
    </template>
  </BaseTicket>
</template>

<style scoped>
.ranura {
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-3);
  min-height: 210px;
}
.ranura--vacia {
  border-style: dashed;
}
.ranura__parte {
  margin: 0;
}

.ranura__prenda {
  display: flex;
  gap: var(--cep-sp-3);
  align-items: center;
  flex: 1;
}
.ranura__datos {
  min-width: 0;
}
.ranura__nombre {
  margin: 0 0 var(--cep-sp-1);
  font-size: var(--cep-fs-sm);
  font-weight: 600;
  line-height: var(--cep-lh-snug);
}
.ranura__precio {
  margin: 0;
  font-size: var(--cep-fs-sm);
  color: var(--cep-exito);
  font-weight: 600;
}
.ranura__precio .muted {
  font-weight: 400;
}
.ranura__agotada {
  margin: 0;
  font-size: var(--cep-fs-xs);
  color: var(--cep-alerta);
}

.ranura__hueco {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0.4;
}
.ranura__cubierta {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
}
.ranura__nota {
  margin: 0;
  font-size: var(--cep-fs-xs);
  line-height: var(--cep-lh-snug);
}
.ranura__acciones {
  display: flex;
  gap: var(--cep-sp-2);
  align-items: center;
}
</style>
