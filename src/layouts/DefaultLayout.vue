<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { storeToRefs } from 'pinia'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import { useCuentaStore } from '@/modules/cuenta/store/cuenta.store'
import { useUiStore } from '@/shared/stores/ui.store'
import BarraComparar from '@/modules/comparar/components/BarraComparar.vue'
import AdSlot from '@/shared/components/AdSlot.vue'
import AppFooter from '@/shared/components/AppFooter.vue'
import AppHeader from '@/shared/components/AppHeader.vue'

// Estructura de las pantallas de catálogo: cabecera completa, contenido y pie.
//
// Los layouts viven en src/layouts/ y no en shared/ a propósito: son parte del
// punto de composición, igual que App.vue y core/router. Son el ÚNICO sitio
// —junto a esos dos— donde el marco de la aplicación se junta con el estado de
// los módulos. shared/ sigue sin poder importar de modules/.
defineProps({
  // Las páginas de texto largo se leen mal a 1180px: se estrechan a una
  // columna de lectura sin necesitar otro layout entero.
  ancho: {
    type: String,
    default: 'completo',
    validator: (v) => ['completo', 'lectura'].includes(v),
  },
})

const route = useRoute()

// El anuncio de cierre vive AQUÍ y no repetido en cada vista: así aparece en
// todas por defecto, incluidas las que se añadan mañana, y se gestiona desde
// un solo sitio.
//
// Se excluyen las rutas marcadas con `sinAnuncios`. Hoy es sólo el 404: las
// políticas de AdSense prohíben anuncios en páginas de error y en páginas sin
// contenido propio, y saltárselo es motivo de suspensión de la cuenta.
const BLOQUE_CIERRE = import.meta.env.VITE_ADSENSE_SLOT_CIERRE ?? ''

const conAnuncios = computed(() => route.meta.sinAnuncios !== true)

const ui = useUiStore()
const cuenta = useCuentaStore()
const comparador = useComparadorStore()

const { tiendas, tiendasActivas, productos, actualizadoEn, categoriasPopulares } =
  storeToRefs(comparador)

// El pie enseña las categorías reales del catálogo, no una lista escrita a
// mano: si la API añade una, aparece sola.
const categoriasDelPie = computed(() =>
  categoriasPopulares.value.map((c) => c.categoria).slice(0, 6),
)
</script>

<template>
  <div class="marco">
    <AppHeader
      v-model:tiendas-activas="tiendasActivas"
      :tiendas="tiendas"
      :tema="ui.tema"
      :temas="ui.TEMAS"
      :usuario="cuenta.usuario"
      @cambiar-tema="ui.elegirTema"
      @cerrar-sesion="cuenta.cerrarSesion"
    />

    <main
      id="contenido"
      class="marco__contenido"
      :class="{ 'marco__contenido--lectura': ancho === 'lectura' }"
    >
      <slot />

      <AdSlot v-if="conAnuncios" :bloque="BLOQUE_CIERRE" :alto="90" class="marco__anuncio" />
    </main>

    <BarraComparar />

    <AppFooter
      :tiendas="tiendas"
      :categorias="categoriasDelPie"
      :total-productos="productos.length"
      :actualizado-en="actualizadoEn"
    />
  </div>
</template>

<style scoped>
.marco {
  display: flex;
  flex-direction: column;
  min-height: 100%;
}
.marco__contenido {
  flex: 1;
  width: 100%;
  max-width: var(--cep-container);
  margin: 0 auto;
  padding: var(--cep-sp-8) var(--cep-gutter) var(--cep-sp-16);
}
.marco__anuncio {
  margin-top: var(--cep-sp-10);
}
.marco__contenido--lectura {
  max-width: 820px;
}
</style>
