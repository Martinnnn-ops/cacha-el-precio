<script setup>
import { RouterLink } from 'vue-router'

import BrandMark from '@/shared/components/BrandMark.vue'

// Estructura de las pantallas de sesión.
//
// Sin buscador, sin filtro de tiendas y sin el pie completo: en una pantalla
// donde se escribe una contraseña, cada elemento de más es una distracción y
// una salida por la que abandonar el formulario a medias.
</script>

<template>
  <div class="acceso">
    <header class="acceso__barra">
      <RouterLink
        class="acceso__marca"
        :to="{ name: 'inicio' }"
        aria-label="Cacha el Precio — ir al inicio"
      >
        <BrandMark :tamano="30" />
        <span class="display">cacha<em>°</em> el precio</span>
      </RouterLink>
    </header>

    <main id="contenido" class="acceso__contenido">
      <slot />
    </main>

    <footer class="acceso__pie">
      <nav aria-label="Legal">
        <RouterLink class="acceso__enlace" :to="{ name: 'terminos' }">
          Términos
        </RouterLink>
        <RouterLink class="acceso__enlace" :to="{ name: 'privacidad' }">
          Privacidad
        </RouterLink>
        <RouterLink class="acceso__enlace" :to="{ name: 'preguntas' }">
          Ayuda
        </RouterLink>
      </nav>
    </footer>
  </div>
</template>

<style scoped>
.acceso {
  display: flex;
  flex-direction: column;
  min-height: 100%;
}
.acceso__barra {
  width: 100%;
  max-width: var(--cep-container);
  margin: 0 auto;
  padding: var(--cep-sp-5) var(--cep-gutter);
}
.acceso__marca {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-2);
  color: var(--cep-ink);
  font-weight: 700;
  font-size: var(--cep-fs-lg);
  text-decoration: none;
}
.acceso__marca em {
  font-style: normal;
  color: var(--cep-accent);
}

.acceso__contenido {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: var(--cep-sp-6) var(--cep-gutter);
}

.acceso__pie {
  padding: var(--cep-sp-6) var(--cep-gutter) var(--cep-sp-8);
  text-align: center;
}
.acceso__pie nav {
  display: flex;
  justify-content: center;
  gap: var(--cep-sp-5);
  flex-wrap: wrap;
}
.acceso__enlace {
  color: var(--cep-muted);
  font-size: var(--cep-fs-sm);
  text-decoration: none;
}
.acceso__enlace:hover {
  color: var(--cep-ink);
  text-decoration: underline;
}
</style>
