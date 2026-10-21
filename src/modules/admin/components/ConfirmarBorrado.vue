<script setup>
import { computed, ref } from 'vue'
import BaseButton from '@/shared/components/BaseButton.vue'

// Borrar un destino o una regla no se deshace: se confirma escribiendo el
// nombre, sin dialogos del navegador. Lo declarado en topologia.json pide
// ademas marcar «forzar», porque lo usa un flujo del sistema.
const props = defineProps({
  nombre: { type: String, required: true },
  declarado: { type: Boolean, default: false },
  ocupado: { type: Boolean, default: false },
})
const emit = defineEmits(['confirmar', 'cancelar'])

const escrito = ref('')
const forzar = ref(false)
const listo = computed(() => escrito.value === props.nombre && (!props.declarado || forzar.value))
</script>

<template>
  <form class="confirmar" @submit.prevent="listo && emit('confirmar', forzar)">
    <label>
      Escribe <strong>{{ nombre }}</strong> para confirmar
      <input v-model.trim="escrito" type="text" autocomplete="off" spellcheck="false" :aria-label="`Confirmar borrado de ${nombre}`" />
    </label>
    <label v-if="declarado" class="forzar">
      <input v-model="forzar" type="checkbox" />
      Está en topologia.json y lo usa un flujo del sistema: borrarlo igual
    </label>
    <div class="acciones">
      <BaseButton type="submit" tamano="chico" :disabled="!listo || ocupado">Borrar</BaseButton>
      <BaseButton variante="texto" tamano="chico" @click="emit('cancelar')">Cancelar</BaseButton>
    </div>
  </form>
</template>

<style scoped>
.confirmar {
  display: grid;
  gap: var(--cep-sp-2);
  padding: var(--cep-sp-3);
  margin-top: var(--cep-sp-2);
  border: 1px solid var(--cep-alerta);
  border-radius: var(--cep-r-md);
}
.confirmar input[type='text'] {
  display: block;
  width: 100%;
  margin-top: var(--cep-sp-1);
  font-family: var(--cep-font-mono);
}
.forzar { display: flex; gap: var(--cep-sp-2); align-items: baseline; font-size: var(--cep-fs-sm); }
.acciones { display: flex; gap: var(--cep-sp-2); flex-wrap: wrap; }
</style>
