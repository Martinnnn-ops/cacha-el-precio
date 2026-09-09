<script setup>
import { RouterLink } from 'vue-router'

// Migas de pan.
//
// El último elemento NO es un enlace y lleva aria-current="page": es la página
// donde ya estás. Un lector de pantalla lo anuncia como tal, y de paso evita
// el enlace que no lleva a ninguna parte.
defineProps({
  // [{ texto, a? }] — sin `a` se pinta como texto plano.
  items: { type: Array, required: true },
})
</script>

<template>
  <nav class="migas mono" aria-label="Dónde estás">
    <ol class="migas__lista">
      <li v-for="(item, i) in items" :key="i" class="migas__item">
        <span v-if="i > 0" class="migas__sep" aria-hidden="true">/</span>

        <RouterLink v-if="item.a" class="migas__enlace" :to="item.a">
          {{ item.texto }}
        </RouterLink>

        <span v-else aria-current="page">{{ item.texto }}</span>
      </li>
    </ol>
  </nav>
</template>

<style scoped>
.migas {
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}
.migas__lista {
  list-style: none;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--cep-sp-15);
  margin: 0;
  padding: 0;
}
.migas__item {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-15);
  min-width: 0;
}
.migas__sep {
  opacity: 0.5;
}
.migas__enlace {
  color: var(--cep-accent);
  text-decoration: none;
}
.migas__enlace:hover {
  text-decoration: underline;
}
/* El último puede ser largo: se recorta en vez de romper la línea. */
.migas__item:last-child span[aria-current] {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 46ch;
}
</style>
