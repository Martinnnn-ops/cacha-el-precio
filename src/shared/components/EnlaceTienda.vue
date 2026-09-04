<script setup>
import { computed } from 'vue'

// Enlace a la ficha del producto EN LA TIENDA. Es el único sitio del que sale
// alguien de aquí para comprar, y por eso concentra tres detalles:
//
//  · rel="sponsored"  — Google lo exige para enlaces que pueden dar comisión.
//    Sin él, un enlace de afiliado se interpreta como recomendación editorial
//    y penaliza el posicionamiento de todo el sitio.
//  · rel="noopener"   — sin esto, la página de destino puede manipular la
//    nuestra a través de window.opener.
//  · target="_blank"  — quien compara no quiere perder la comparación al ir a
//    mirar una tienda.
const props = defineProps({
  url: { type: String, default: null },
  tienda: { type: String, required: true },
  // Se marca cuando la oferta puede dar comisión, para etiquetarla como toca.
  patrocinado: { type: Boolean, default: true },
  tamano: { type: String, default: 'medio' },
})

// Sin URL no hay botón: es preferible que falte a mandar a alguien a una
// dirección adivinada.
const visible = computed(() => Boolean(props.url))
</script>

<template>
  <a
    v-if="visible"
    class="ir"
    :class="`ir--${tamano}`"
    :href="url"
    target="_blank"
    :rel="patrocinado ? 'sponsored noopener noreferrer' : 'noopener noreferrer'"
  >
    Ver en {{ tienda }}

    <svg viewBox="0 0 16 16" width="13" height="13" fill="none" aria-hidden="true">
      <path
        d="M6 3h7v7M13 3 4 12"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
      />
    </svg>

    <span class="sr-only">(se abre en la tienda, en una pestaña nueva)</span>
  </a>
</template>

<style scoped>
.ir {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--cep-sp-15);
  min-height: var(--cep-control-h);
  padding: 0 var(--cep-sp-4);
  background: var(--cep-ink);
  color: var(--cep-on-ink);
  border-radius: var(--cep-r-sm);
  font-family: var(--cep-font-display);
  font-weight: 600;
  font-size: var(--cep-fs-md);
  text-decoration: none;
  transition:
    background var(--cep-dur-1) var(--cep-ease),
    transform var(--cep-dur-1) var(--cep-ease);
}
.ir:hover {
  background: var(--cep-accent);
  color: var(--cep-on-accent);
}
.ir:active {
  transform: translateY(1px);
}
.ir--chico {
  min-height: var(--cep-control-h-sm);
  padding: 0 var(--cep-sp-3);
  font-size: var(--cep-fs-sm);
}
</style>
