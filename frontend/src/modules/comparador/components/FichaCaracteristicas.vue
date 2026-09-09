<script setup>
import { computed } from 'vue'

import BaseTicket from '@/shared/components/BaseTicket.vue'

// Características agrupadas por bloque, como en las fichas de SoloTodo.
//
// Acepta las DOS formas: el formato agrupado —[{ grupo, notas, filas }]— y el
// plano —{ clave: valor }—. El plano es lo que daría la API si algún día
// expone specs sin agrupar, y no puede quedarse sin ficha por eso.
const props = defineProps({
  specs: { type: [Array, Object], default: () => [] },
})

const grupos = computed(() => {
  const s = props.specs

  if (Array.isArray(s)) {
    return s.filter((g) => Object.keys(g?.filas ?? {}).length > 0)
  }

  const filas = s ?? {}

  if (Object.keys(filas).length === 0) return []

  return [{ grupo: 'Características', filas }]
})

const hay = computed(() => grupos.value.length > 0)
</script>

<template>
  <div v-if="hay" class="fichas">
    <BaseTicket v-for="grupo in grupos" :key="grupo.grupo">
      <p class="eyebrow">{{ grupo.grupo }}</p>

      <table class="tabla-ficha">
        <tbody>
          <tr v-for="(valor, clave) in grupo.filas" :key="clave">
            <th scope="row" class="mono tabla-ficha__clave">{{ clave }}</th>
            <td class="tabla-ficha__valor">{{ valor }}</td>
          </tr>
        </tbody>
      </table>

      <!-- La advertencia va DENTRO del grupo al que se refiere, no al final de
           la ficha: una nota sobre el lavado suelta al pie no se relaciona con
           nada. -->
      <p v-if="grupo.notas" class="nota">
        <strong>Ojo:</strong> {{ grupo.notas }}
      </p>
    </BaseTicket>
  </div>
</template>

<style scoped>
.fichas {
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-4);
}

.tabla-ficha {
  width: 100%;
  border-collapse: collapse;
  margin-top: var(--cep-sp-2);
}
.tabla-ficha tr {
  border-bottom: 1px solid var(--cep-line);
}
.tabla-ficha tr:last-child {
  border-bottom: none;
}
.tabla-ficha th,
.tabla-ficha td {
  padding: var(--cep-sp-2) var(--cep-sp-1);
  text-align: left;
  vertical-align: top;
  font-size: var(--cep-fs-sm);
}
.tabla-ficha__clave {
  width: 38%;
  font-weight: 400;
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}

.nota {
  margin: var(--cep-sp-3) 0 0;
  padding: var(--cep-sp-25) var(--cep-sp-3);
  background: var(--cep-bg);
  border-left: 3px solid var(--cep-rate);
  border-radius: var(--cep-r-sm);
  font-size: var(--cep-fs-sm);
  line-height: var(--cep-lh-snug);
}
</style>
