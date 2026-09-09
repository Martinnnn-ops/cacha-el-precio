<script setup>
// Botón de Google. El logotipo va con sus colores de marca y el texto dice
// "Continuar con Google": son requisitos de las condiciones de Google Sign-In,
// no una preferencia estética.
defineProps({
  texto: { type: String, default: 'Continuar con Google' },
  cargando: { type: Boolean, default: false },
  deshabilitado: { type: Boolean, default: false },
})

defineEmits(['click'])
</script>

<template>
  <button
    type="button"
    class="google"
    :disabled="cargando || deshabilitado"
    @click="$emit('click')"
  >
    <svg class="google__logo" viewBox="0 0 18 18" aria-hidden="true">
      <path fill="#4285F4" d="M17.64 9.2c0-.64-.06-1.25-.16-1.84H9v3.48h4.84a4.14 4.14 0 0 1-1.8 2.72v2.26h2.92c1.7-1.57 2.68-3.88 2.68-6.62Z" />
      <path fill="#34A853" d="M9 18c2.43 0 4.47-.8 5.96-2.18l-2.92-2.26c-.8.54-1.84.86-3.04.86-2.34 0-4.32-1.58-5.03-3.7H.96v2.33A9 9 0 0 0 9 18Z" />
      <path fill="#FBBC05" d="M3.97 10.72a5.4 5.4 0 0 1 0-3.44V4.95H.96a9 9 0 0 0 0 8.1l3-2.33Z" />
      <path fill="#EA4335" d="M9 3.58c1.32 0 2.5.45 3.44 1.35l2.58-2.58C13.46.9 11.43 0 9 0A9 9 0 0 0 .96 4.95l3.01 2.33C4.68 5.16 6.66 3.58 9 3.58Z" />
    </svg>

    <span>{{ cargando ? 'Conectando…' : texto }}</span>
  </button>
</template>

<style scoped>
.google {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--cep-sp-3);
  width: 100%;
  min-height: var(--cep-control-h-lg);
  padding: 0 var(--cep-sp-5);
  background: var(--cep-surface);
  color: var(--cep-ink);
  border: 1.5px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-lg);
  font-family: var(--cep-font-display);
  font-weight: 600;
  font-size: var(--cep-fs-base);
  cursor: pointer;
  transition:
    background var(--cep-dur-1) var(--cep-ease),
    border-color var(--cep-dur-1) var(--cep-ease),
    transform var(--cep-dur-1) var(--cep-ease);
}
.google:hover:not(:disabled) {
  border-color: var(--cep-ink);
  background: var(--cep-wash);
}
.google:active:not(:disabled) {
  transform: translateY(1px);
}
.google:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}
.google__logo {
  width: 18px;
  height: 18px;
  flex: none;
}
</style>
