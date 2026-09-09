<script setup>
import { computed, onErrorCaptured, ref, watch } from 'vue'
import { RouterView, useRoute } from 'vue-router'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import { resolverLayout } from '@/layouts'
import ErrorInesperado from '@/shared/components/ErrorInesperado.vue'

// Raíz de la aplicación. Sólo hace tres cosas: arrancar las preferencias,
// elegir el layout de la ruta y contener los errores que se escapen.
const route = useRoute()

// Las tiendas se piden una vez, aquí: las necesitan la cabecera y el pie, que
// están en todas las pantallas.
useComparadorStore().cargarTiendas()

const layout = computed(() => resolverLayout(route.meta.layout))
const ancho = computed(() => route.meta.ancho ?? 'completo')

// Red de seguridad: si un componente lanza durante el render, sin esto el
// usuario se queda con una pantalla en blanco y sin ninguna salida.
const fallo = ref(null)

onErrorCaptured((e) => {
  fallo.value = e

  if (import.meta.env.DEV) console.error(e)

  return false
})

// Al cambiar de ruta se limpia: el error era de la pantalla anterior.
watch(() => route.fullPath, () => {
  fallo.value = null
})
</script>

<template>
  <component :is="layout" :ancho="ancho">
    <ErrorInesperado v-if="fallo" @reintentar="fallo = null" />

    <RouterView v-else v-slot="{ Component }">
      <component :is="Component" />
    </RouterView>
  </component>
</template>
