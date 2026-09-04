<script setup>
import { computed } from 'vue'
import { RouterLink } from 'vue-router'

// Renderiza <button>, <a> o <RouterLink> según lo que reciba, pero se ve
// siempre igual. Así un "botón" que en realidad navega no tiene que imitar el
// estilo a mano ni perder la semántica del enlace.
const props = defineProps({
  variante: {
    type: String,
    default: 'primario',
    validator: (v) => ['primario', 'secundario', 'texto'].includes(v),
  },
  tamano: {
    type: String,
    default: 'medio',
    validator: (v) => ['chico', 'medio'].includes(v),
  },
  to: { type: [String, Object], default: null },
  href: { type: String, default: null },
  type: { type: String, default: 'button' },
  disabled: { type: Boolean, default: false },
  bloque: { type: Boolean, default: false },
})

const etiqueta = computed(() => {
  if (props.to) return RouterLink
  if (props.href) return 'a'

  return 'button'
})

const atributos = computed(() => {
  if (props.to) return { to: props.to }
  if (props.href) return { href: props.href, rel: 'noopener', target: '_blank' }

  return { type: props.type, disabled: props.disabled }
})
</script>

<template>
  <component
    :is="etiqueta"
    v-bind="atributos"
    class="btn"
    :class="[`btn--${variante}`, `btn--${tamano}`, { 'btn--bloque': bloque }]"
    :aria-disabled="disabled || undefined"
  >
    <slot />
  </component>
</template>

<style scoped>
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 40px;
  padding: 0 16px;
  border: 1.5px solid transparent;
  border-radius: 6px;
  font: inherit;
  font-weight: 600;
  line-height: 1;
  text-decoration: none;
  white-space: nowrap;
  cursor: pointer;
  transition:
    background 0.12s ease,
    border-color 0.12s ease,
    color 0.12s ease,
    transform 0.12s ease;
}
.btn:active:not(:disabled) {
  transform: translateY(1px);
}
.btn:disabled,
.btn[aria-disabled='true'] {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

.btn--primario {
  background: var(--cep-ink);
  border-color: var(--cep-ink);
  color: var(--cep-on-ink);
}
.btn--primario:hover:not(:disabled) {
  background: var(--cep-accent);
  border-color: var(--cep-accent);
}

.btn--secundario {
  background: transparent;
  border-color: var(--cep-line-fuerte);
  color: var(--cep-ink);
}
.btn--secundario:hover:not(:disabled) {
  border-color: var(--cep-ink);
  background: var(--cep-wash);
}

.btn--texto {
  background: none;
  border-color: transparent;
  color: var(--cep-accent);
  padding: 0 4px;
  min-height: 32px;
}
.btn--texto:hover:not(:disabled) {
  text-decoration: underline;
}

.btn--chico {
  min-height: 32px;
  padding: 0 12px;
  font-size: 13px;
}
.btn--bloque {
  width: 100%;
}
</style>
