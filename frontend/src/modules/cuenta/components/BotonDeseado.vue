<script setup>
import { computed, ref, watch } from 'vue'
import { useCuentaStore } from '@/modules/cuenta/store/cuenta.store'
import { useColeccionStore } from '@/modules/cuenta/store/coleccion.store'
const props = defineProps({ productoId: { type: String, required: true } })
const cuenta = useCuentaStore()
const coleccion = useColeccionStore()
const disponible = computed(() => cuenta.autenticado)
const mensaje = ref('')
watch(() => cuenta.usuario, (usuario) => {
  mensaje.value = ''
  if (usuario) coleccion.cargar()
}, { immediate: true })
async function alternar() {
  mensaje.value = ''
  if (!await coleccion.alternar(props.productoId)) mensaje.value = coleccion.error
}
</script>

<template>
  <button v-if="disponible" type="button" class="deseado"
    :aria-pressed="coleccion.deseados.includes(productoId)"
    :aria-label="coleccion.deseados.includes(productoId) ? 'Quitar de deseados' : 'Guardar en deseados'"
    :disabled="coleccion.ocupado" @click.stop.prevent="alternar">
    {{ coleccion.deseados.includes(productoId) ? '♥ Guardado' : '♡ Deseado' }}
  </button>
  <RouterLink v-else class="deseado" :to="{ name: 'login', query: { volver: $route.fullPath } }"
    @click.stop>♡ Guardar</RouterLink>
  <span v-if="mensaje" class="deseado__error" role="status">{{ mensaje }}</span>
</template>

<style scoped>
.deseado { border: 1px solid var(--cep-line-media); border-radius: var(--cep-r-pill);
  background: var(--cep-surface); color: var(--cep-ink); padding: .5rem .75rem;
  font: inherit; font-size: var(--cep-fs-xs); cursor: pointer; text-decoration: none;
  min-height: var(--cep-control-h-sm); }
.deseado[aria-pressed="true"] { color: var(--cep-accent); border-color: currentColor; }
.deseado__error { font-size: var(--cep-fs-xs); color: var(--cep-muted); }
</style>
