<script setup>
import { RouterLink } from 'vue-router'

import { CONTACTO } from '@/shared/config/sitio'

// Contenido real: describe cómo funciona la aplicación de verdad. A diferencia
// de los términos y la privacidad, esto no necesita revisión legal.
const PREGUNTAS = [
  {
    p: '¿De dónde salen los precios?',
    r: 'Consultamos el catálogo publicado por cada tienda y guardamos el precio que aparece en su ficha. No compramos ni vendemos nada: sólo registramos lo que la tienda muestra al público.',
  },
  {
    p: '¿Cada cuánto se actualizan?',
    r: 'Una vez al día. Entre dos revisiones una tienda puede cambiar el precio, así que el importe final siempre es el que veas en la tienda al pagar. Si encuentras una diferencia, manda la tienda.',
  },
  {
    p: '¿Por qué el descuento que muestro no coincide con el de la tienda?',
    r: 'Porque medimos cosas distintas. La tienda compara su precio de hoy contra su propio precio de referencia. Nosotros comparamos el precio de hoy contra lo que cobran las demás tiendas por el mismo producto. Un −40% puede seguir siendo el precio más caro del mercado.',
  },
  {
    p: '¿Qué significa que una tienda salga como "la más barata"?',
    r: 'Que tiene el precio más bajo entre las tiendas que además tienen stock. Una oferta agotada no cuenta: no es una oferta si no la puedes comprar.',
  },
  {
    p: '¿Ganan dinero con esto?',
    r: 'Podemos recibir una comisión de afiliado cuando compras a través de nuestros enlaces, y hay espacios de publicidad marcados como tales. Ni una cosa ni la otra cambian el precio que pagas ni el orden en que aparecen las tiendas: el orden siempre va de más barata a más cara.',
  },
  {
    p: '¿Necesito una cuenta?',
    r: 'No para comparar. La cuenta sirve para guardar prendas y que te avisemos cuando bajen de precio.',
  },
  {
    p: '¿Qué hago si un producto está mal emparejado?',
    r: `Puede pasar que dos prendas parecidas se agrupen como una sola. Escríbenos a ${CONTACTO} y lo corregimos.`,
  },
]
</script>

<template>
  <article class="legal">
    <h1 class="display legal__titulo">Preguntas frecuentes</h1>
    <p class="legal__fecha">Sobre cómo funciona el comparador</p>

    <section v-for="item in PREGUNTAS" :key="item.p">
      <h2>{{ item.p }}</h2>
      <p>{{ item.r }}</p>
    </section>

    <h2>¿Sigues con dudas?</h2>
    <p>
      Escríbenos a <a :href="`mailto:${CONTACTO}`">{{ CONTACTO }}</a>, o revisa
      los
      <RouterLink :to="{ name: 'terminos' }">términos y condiciones</RouterLink>
      o la
      <RouterLink :to="{ name: 'privacidad' }">política de privacidad</RouterLink>.
    </p>
  </article>
</template>

<style scoped>
@import '@/assets/legal.css';
</style>
