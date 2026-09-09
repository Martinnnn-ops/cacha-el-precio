<script setup>
import { computed, onMounted, useTemplateRef } from 'vue'

import { ADSENSE_CLIENT, cargarAdsense } from '@/shared/services/adsense'

// Bloque de anuncio de AdSense.
//
// La prop se llama `bloque` y no `slot`: en Vue, "slot" significa otra cosa y
// mezclar los dos conceptos en el mismo componente se lee fatal.
const props = defineProps({
  bloque: { type: String, default: '' },
  // Alto reservado desde el primer pintado. No es decoración: el anuncio llega
  // tarde y, sin reserva, empuja el contenido hacia abajo cuando aparece. Ese
  // salto lo mide Core Web Vitals (CLS) y penaliza el posicionamiento.
  alto: { type: Number, default: 110 },
  formato: { type: String, default: 'auto' },
})

// Sin editor o sin bloque no hay nada que pedir: el componente no renderiza
// nada. Es lo que evita que en producción se vea una caja vacía.
const visible = computed(() => ADSENSE_CLIENT !== '' && props.bloque !== '')

const anuncio = useTemplateRef('anuncio')

// Una sola inserción por instancia montada. AdSense lanza
// "All ins elements in the DOM with class=adsbygoogle already have ads in them"
// si se empuja dos veces sobre el mismo <ins>.
let empujado = false

onMounted(async () => {
  if (!visible.value || empujado || !anuncio.value) return

  try {
    await cargarAdsense()

    empujado = true
    window.adsbygoogle = window.adsbygoogle || []
    window.adsbygoogle.push({})
  } catch {
    // Bloqueador de anuncios, red caída o script rechazado. El hueco se queda
    // vacío y ya está: que falle un anuncio no puede tumbar la portada.
  }
})
</script>

<template>
  <aside v-if="visible" class="anuncio" :style="{ minHeight: `${alto}px` }">
    <!-- Etiquetar el anuncio está permitido por las políticas de AdSense
         (siempre que la etiqueta sea neutra) y en un comparador de precios es
         lo honesto: un anuncio que se confunde con un producto engaña. -->
    <span class="anuncio__marca mono">Publicidad</span>

    <ins
      ref="anuncio"
      class="adsbygoogle anuncio__bloque"
      :data-ad-client="ADSENSE_CLIENT"
      :data-ad-slot="bloque"
      :data-ad-format="formato"
      data-full-width-responsive="true"
    />
  </aside>
</template>

<style scoped>
.anuncio {
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-15);
}
.anuncio__marca {
  align-self: flex-start;
  font-size: var(--cep-fs-2xs);
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--cep-muted);
}
.anuncio__bloque {
  display: block;
  width: 100%;
}
</style>
