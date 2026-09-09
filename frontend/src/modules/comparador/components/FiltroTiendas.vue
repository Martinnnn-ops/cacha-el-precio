<script setup>
import { computed } from 'vue'

import BaseButton from '@/shared/components/BaseButton.vue'

// Las tiendas llegan por props: con backend salen del campo `store` y ya no son
// una constante del código.
const props = defineProps({
  tiendas: { type: Array, required: true },
})

// Lista de tiendas marcadas, en v-model. El componente no toca el store: recibe
// la selección y emite la nueva, para que también sirva en otra vista o módulo.
const seleccion = defineModel({ type: Array, required: true })

const todasMarcadas = computed(
  () => seleccion.value.length === props.tiendas.length,
)

function alternar(id) {
  const i = seleccion.value.indexOf(id)

  seleccion.value =
    i === -1
      ? [...seleccion.value, id]
      : seleccion.value.filter((t) => t !== id)
}
</script>

<template>
  <fieldset class="filtro">
    <legend class="filtro__titulo">Tiendas</legend>

    <label v-for="tienda in tiendas" :key="tienda.id" class="filtro__opcion">
      <input
        type="checkbox"
        :value="tienda.id"
        :checked="seleccion.includes(tienda.id)"
        @change="alternar(tienda.id)"
      />

      <span class="filtro__punto" :style="{ background: tienda.color }" />
      <span>{{ tienda.nombre }}</span>
    </label>

    <p v-if="seleccion.length === 0" class="filtro__aviso">
      No hay ninguna tienda marcada, así que no se puede comparar nada.
    </p>

    <BaseButton
      v-if="!todasMarcadas"
      variante="texto"
      tamano="chico"
      @click="seleccion = tiendas.map((t) => t.id)"
    >
      Marcar todas
    </BaseButton>
  </fieldset>
</template>

<style scoped>
.filtro {
  border: 1px solid var(--cep-line);
  border-radius: 10px;
  padding: 14px 16px 16px;
  background: var(--cep-surface);
}
.filtro__titulo {
  padding: 0 6px;
  font-size: 11px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--cep-muted);
}
.filtro__opcion {
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 7px 6px;
  border-radius: 6px;
  font-size: 14px;
  cursor: pointer;
}
.filtro__opcion:hover {
  background: var(--cep-wash);
}
.filtro__opcion input {
  accent-color: var(--cep-accent);
  width: 16px;
  height: 16px;
  cursor: pointer;
}
/* Con el dedo, 16px es un blanco imposible. El objetivo real es la etiqueta
   entera —envuelve al input, así que tocar el nombre de la tienda la marca—
   pero el cuadradito también se agranda para que se vea que es tocable. */
@media (pointer: coarse) {
  .filtro__opcion {
    min-height: 44px;
  }
  .filtro__opcion input {
    width: 22px;
    height: 22px;
  }
}
.filtro__punto {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  flex: none;
}
.filtro__aviso {
  margin: 8px 6px 0;
  font-size: 12px;
  color: var(--cep-alerta);
  line-height: 1.4;
}
</style>
