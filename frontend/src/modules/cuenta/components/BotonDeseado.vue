<script setup>
import { computed, ref, watch } from 'vue'
import { useCuentaStore } from '@/modules/cuenta/store/cuenta.store'
import { useColeccionStore } from '@/modules/cuenta/store/coleccion.store'
const props = defineProps({ productoId: { type: String, required: true } })
const cuenta = useCuentaStore()
const coleccion = useColeccionStore()
const disponible = computed(() => cuenta.autenticado)
const guardado = computed(() => coleccion.deseados.includes(props.productoId))
const mensaje = ref('')
// Solo late al guardar, no al quitar ni al cargar la página con la prenda ya
// guardada: es la confirmación de que se hizo, no un adorno permanente.
const latido = ref(false)
watch(() => cuenta.usuario, (usuario) => {
  mensaje.value = ''
  if (usuario) coleccion.cargar()
}, { immediate: true })
async function alternar() {
  mensaje.value = ''
  latido.value = false
  const ibaAGuardar = !guardado.value
  if (!await coleccion.alternar(props.productoId)) mensaje.value = coleccion.error
  else if (ibaAGuardar) latido.value = true
}
</script>

<template>
  <button v-if="disponible" type="button" class="deseado"
    :class="{ 'deseado--guardado': guardado, 'deseado--latido': latido }"
    :aria-pressed="guardado"
    :aria-label="guardado ? 'Quitar de deseados' : 'Guardar en deseados'"
    :disabled="coleccion.ocupado" @click.stop.prevent="alternar"
    @animationend="latido = false">
    <span class="deseado__corazon" aria-hidden="true">{{ guardado ? '♥' : '♡' }}</span>
    {{ guardado ? 'Guardado' : 'Deseado' }}
  </button>
  <RouterLink v-else class="deseado" :to="{ name: 'login', query: { volver: $route.fullPath } }"
    @click.stop><span class="deseado__corazon" aria-hidden="true">♡</span> Guardar</RouterLink>
  <span v-if="mensaje" class="deseado__error" role="status">{{ mensaje }}</span>
</template>

<style scoped>
.deseado { display: inline-flex; align-items: center; gap: .35rem;
  border: 1px solid var(--cep-line-media); border-radius: var(--cep-r-pill);
  background: var(--cep-surface); color: var(--cep-ink); padding: .5rem .75rem;
  font: inherit; font-size: var(--cep-fs-xs); cursor: pointer; text-decoration: none;
  min-height: var(--cep-control-h-sm);
  transition: border-color var(--cep-dur-1) var(--cep-ease), color var(--cep-dur-1) var(--cep-ease); }
.deseado:hover { border-color: var(--cep-deseado); }
.deseado:hover .deseado__corazon { color: var(--cep-deseado); }
.deseado--guardado { color: var(--cep-deseado); border-color: currentColor; }
.deseado__corazon { position: relative; display: inline-block; line-height: 1;
  transition: color var(--cep-dur-1) var(--cep-ease); }
.deseado--guardado .deseado__corazon { color: var(--cep-deseado); }
/* Al guardar: el corazón late y suelta un anillo. Corto (450 ms) para que se
   note sin entorpecer; base.css lo apaga con prefers-reduced-motion. */
.deseado--latido .deseado__corazon { animation: latir 450ms var(--cep-ease); }
.deseado--latido .deseado__corazon::after { content: ''; position: absolute; inset: -6px;
  border: 1.5px solid var(--cep-deseado); border-radius: 50%; opacity: 0;
  animation: anillo 450ms ease-out; }
@keyframes latir {
  0% { transform: scale(1); }
  35% { transform: scale(1.45); }
  60% { transform: scale(.9); }
  100% { transform: scale(1); }
}
@keyframes anillo {
  0% { transform: scale(.4); opacity: .9; }
  100% { transform: scale(1.6); opacity: 0; }
}
.deseado__error { font-size: var(--cep-fs-xs); color: var(--cep-muted); }
</style>
