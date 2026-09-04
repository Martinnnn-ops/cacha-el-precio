<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

import BaseButton from '@/shared/components/BaseButton.vue'
import { tiempoRelativo } from '@/shared/utils/formato'

// Franja de estado. Va arriba del contenido y NO lo sustituye: la página se
// sigue viendo entera debajo.
//
// La diferencia importa. Antes un error tapaba la vista completa —incluido el
// titular y los títulos de sección, que son texto fijo y no necesitan datos—
// y quedaba una pantalla que parecía rota.
const props = defineProps({
  error: { type: String, default: null },
  // Hay datos en pantalla aunque la última petición fallara.
  desactualizado: { type: Boolean, default: false },
  // Marca de tiempo de la última carga con éxito.
  actualizadoEn: { type: Number, default: null },
  reintentando: { type: Boolean, default: false },
})

defineEmits(['reintentar'])

// Sin conexión es distinto de un fallo del servidor, y la salida también:
// aquí no sirve de nada reintentar hasta que vuelva la red.
const conectado = ref(true)

function sincronizarRed() {
  conectado.value = navigator.onLine !== false
}

onMounted(() => {
  sincronizarRed()
  window.addEventListener('online', sincronizarRed)
  window.addEventListener('offline', sincronizarRed)
})

onBeforeUnmount(() => {
  window.removeEventListener('online', sincronizarRed)
  window.removeEventListener('offline', sincronizarRed)
})

const visible = computed(() => !conectado.value || props.error !== null)

const mensaje = computed(() => {
  if (!conectado.value) {
    return 'Parece que estás sin conexión. Lo que ves puede no estar al día.'
  }

  if (props.desactualizado) {
    const cuando = tiempoRelativo(props.actualizadoEn)

    return cuando
      ? `No pudimos actualizar los precios. Los que ves son de ${cuando}.`
      : 'No pudimos actualizar los precios. Los que ves pueden no estar al día.'
  }

  return props.error ?? ''
})
</script>

<template>
  <!-- role="status" y no "alert": se anuncia al terminar lo que se esté
       leyendo, sin interrumpir a quien navega con teclado o lector. -->
  <div
    v-if="visible"
    class="aviso"
    :class="{ 'aviso--suave': desactualizado || !conectado }"
    role="status"
    aria-live="polite"
  >
    <span class="aviso__texto">{{ mensaje }}</span>

    <BaseButton
      v-if="conectado"
      variante="secundario"
      tamano="chico"
      :disabled="reintentando"
      @click="$emit('reintentar')"
    >
      {{ reintentando ? 'Reintentando…' : 'Reintentar' }}
    </BaseButton>
  </div>
</template>

<style scoped>
.aviso {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--cep-sp-4);
  flex-wrap: wrap;
  padding: var(--cep-sp-3) var(--cep-sp-4);
  margin-bottom: var(--cep-sp-6);
  background: var(--cep-surface);
  /* Sólo el borde de acento: el fondo distinto ya lo separa del contenido y
     rodearlo entero de línea añade ruido sin añadir información. */
  border-left: 3px solid var(--cep-alerta);
  box-shadow: var(--cep-shadow-1);
  border-radius: var(--cep-r-sm);
}
/* Datos viejos o sin red no es un error rojo: la página funciona, sólo que con
   lo último que se pudo traer. */
.aviso--suave {
  border-left-color: var(--cep-rate);
}
.aviso__texto {
  font-size: var(--cep-fs-sm);
  line-height: var(--cep-lh-snug);
}
</style>
