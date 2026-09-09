<script setup>
import BaseButton from '@/shared/components/BaseButton.vue'
</script>

<template>
  <section class="vacio">
    <h1>404</h1>
    <p>Esta página no existe.</p>

    <BaseButton to="/">Volver al comparador</BaseButton>
  </section>
</template>

<style scoped>
.vacio {
  text-align: center;
  padding: 80px 20px;
}
h1 {
  font-size: 56px;
  margin: 0;
}
p {
  color: var(--cep-muted);
  margin: 4px 0 24px;
}
</style>
