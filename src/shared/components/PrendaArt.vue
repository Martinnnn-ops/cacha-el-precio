<script setup>
// Ilustración de relleno para las tarjetas sin fotografía.
//
// El proyecto original dibujaba una zapatilla porque comparaba calzado; aquí se
// comparan prendas, así que el trazo es una camiseta en una percha. Mismo
// lenguaje —línea fina, sin relleno—, motivo correcto.
defineProps({
  alto: { type: Number, default: 90 },
  trazo: { type: String, default: 'var(--cep-muted)' },
  fondo: { type: Boolean, default: true },
})
</script>

<template>
  <div
    class="arte"
    :style="{
      height: `${alto}px`,
      background: fondo ? 'var(--cep-sunken)' : 'transparent',
    }"
    aria-hidden="true"
  >
    <svg
      viewBox="0 0 120 76"
      width="74%"
      height="74%"
      fill="none"
      :stroke="trazo"
      stroke-width="2.2"
      stroke-linecap="round"
      stroke-linejoin="round"
    >
      <!-- gancho de la percha -->
      <path d="M60 6c0 5-6 5-6 9" />
      <!-- hombros -->
      <path d="M60 15 22 30" />
      <path d="M60 15 98 30" />
      <!-- camiseta -->
      <path
        d="M44 28 30 34l6 13 8-3v27a2 2 0 0 0 2 2h28a2 2 0 0 0 2-2V44l8 3 6-13-14-6-6 3a12 12 0 0 1-20 0Z"
      />
      <!-- costura del cuello -->
      <path d="M50 29a11 11 0 0 0 20 0" />
    </svg>
  </div>
</template>

<style scoped>
.arte {
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--cep-r-sm);
  overflow: hidden;
}
</style>
