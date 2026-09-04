<script setup>
// Bloque de carga: la silueta de algo que todavía no ha llegado.
//
// Va con aria-hidden porque es decoración mientras se espera — un lector de
// pantalla no debe ir anunciando cajas vacías. Lo que sí se anuncia es el
// estado, y de eso se encarga AvisoDatos con su role="status".
defineProps({
  alto: { type: [Number, String], default: 12 },
  ancho: { type: String, default: '100%' },
  radio: { type: String, default: 'var(--cep-r-sm)' },
  circulo: { type: Boolean, default: false },
})

const medida = (v) => (typeof v === 'number' ? `${v}px` : v)
</script>

<template>
  <span
    class="hueso"
    aria-hidden="true"
    :style="{
      height: medida(alto),
      width: circulo ? medida(alto) : ancho,
      borderRadius: circulo ? '50%' : radio,
    }"
  />
</template>

<style scoped>
.hueso {
  display: block;
  flex: none;
  background: var(--cep-sunken);
  animation: cep-latido 1.4s ease-in-out infinite;
}
</style>
