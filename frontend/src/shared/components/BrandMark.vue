<script setup>
// Símbolo de la marca: etiqueta de precio con su perforación y una lupa con
// visto bueno. Es el mismo dibujo que el favicon de public/, para que el icono
// de la pestaña y el de la cabecera sean la misma marca.
//
// Va todo en `currentColor`: así hereda el color del contexto y funciona igual
// en el tema claro, en el oscuro y sobre los bloques de acento.
defineProps({
  tamano: { type: Number, default: 26 },
  // Nombre accesible. Vacío = decorativo, cuando el texto de la marca va al
  // lado y anunciarlo dos veces sólo estorba al lector de pantalla.
  titulo: { type: String, default: '' },
})
</script>

<template>
  <svg
    :width="tamano"
    :height="tamano"
    viewBox="0 0 32 32"
    fill="none"
    :role="titulo ? 'img' : 'presentation'"
    :aria-hidden="titulo ? undefined : 'true'"
  >
    <title v-if="titulo">{{ titulo }}</title>

    <!-- etiqueta -->
    <path
      d="M5 16 15 5h10v10L14 27Z"
      stroke="currentColor"
      stroke-width="2.3"
      stroke-linejoin="round"
      stroke-linecap="round"
    />

    <!-- perforación -->
    <circle cx="19.5" cy="9.5" r="1.6" fill="currentColor" />

    <!-- lupa -->
    <circle
      cx="19.5"
      cy="19.5"
      r="5.2"
      stroke="currentColor"
      stroke-width="2.3"
    />
    <path
      d="m23.2 23.2 3.8 3.8"
      stroke="currentColor"
      stroke-width="2.6"
      stroke-linecap="round"
    />

    <!-- visto bueno -->
    <path
      d="m17 19.7 1.8 1.9 3.5-4.3"
      stroke="currentColor"
      stroke-width="2.1"
      stroke-linecap="round"
      stroke-linejoin="round"
    />
  </svg>
</template>
