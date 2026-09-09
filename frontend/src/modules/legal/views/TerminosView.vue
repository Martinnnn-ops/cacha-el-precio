<script setup>
import { FECHA_VIGENCIA } from '@/modules/legal/vigencia'
import { CONTACTO } from '@/shared/config/sitio'

// AVISO PARA EL EQUIPO — este texto describe con precisión cómo funciona el
// servicio, pero NO lo ha revisado nadie con competencia legal. Antes de
// considerarlo definitivo debe pasar por revisión y ajustarse a la normativa
// aplicable (en Chile, Ley 19.496 de protección al consumidor y Ley 19.628
// sobre datos personales). Ver la sección "Legal" del README.
</script>

<template>
  <article class="legal">
    <h1 class="display legal__titulo">Términos y condiciones</h1>
    <p class="legal__fecha">Última actualización: {{ FECHA_VIGENCIA }}</p>

    <h2>1. Qué es este servicio</h2>
    <p>
      Cacha el Precio es un servicio <strong>informativo</strong> de comparación
      de precios. No vendemos productos, no intermediamos en la compra y no
      somos parte del contrato entre el comprador y la tienda. Toda transacción
      ocurre en el sitio de la tienda, bajo sus propias condiciones.
    </p>

    <h2>2. Sobre la información de precios</h2>
    <p>
      Los precios que mostramos se recogen del catálogo público de cada tienda y
      se actualizan una vez al día. Entre dos actualizaciones el precio puede
      cambiar sin aviso.
    </p>
    <p>
      <strong>El precio válido es siempre el que la tienda muestra al pagar.</strong>
      Hacemos lo razonable por que la información sea correcta, pero no
      garantizamos que esté libre de errores, ni que un producto siga
      disponible.
    </p>

    <h2>3. Enlaces a tiendas y comisiones</h2>
    <p>
      Algunos enlaces a tiendas son de afiliado: si compras a través de ellos
      podemos recibir una comisión. Esto no supone un costo adicional para ti y
      no altera el orden de los resultados, que siempre se ordenan de precio
      más bajo a más alto entre las tiendas con stock.
    </p>

    <h2>4. Cuentas de usuario</h2>
    <p>
      Para seguir productos hace falta una cuenta. Eres responsable de mantener
      tu contraseña en secreto y de la actividad que ocurra con tu cuenta.
      Puedes cerrarla cuando quieras.
    </p>

    <h2>5. Uso aceptable</h2>
    <ul>
      <li>No extraer los datos de forma automatizada ni a gran escala.</li>
      <li>No intentar interrumpir ni sobrecargar el servicio.</li>
      <li>No suplantar a otra persona ni a una tienda.</li>
    </ul>

    <h2>6. Marcas de terceros</h2>
    <p>
      Los nombres y marcas de las tiendas pertenecen a sus titulares. Se usan
      únicamente para identificar de dónde procede cada precio. No implican
      asociación, patrocinio ni respaldo.
    </p>

    <h2>7. Limitación de responsabilidad</h2>
    <p>
      El servicio se ofrece «tal cual». No respondemos por decisiones de compra
      tomadas a partir de la información mostrada, ni por lo que ocurra en el
      sitio de una tienda.
    </p>
    <p>
      Nada de lo anterior limita los derechos que la ley reconoce a las personas
      consumidoras y que no se pueden renunciar por contrato.
    </p>

    <h2>8. Cambios</h2>
    <p>
      Podemos actualizar estos términos. Si el cambio es relevante lo avisaremos
      en la página con antelación razonable.
    </p>

    <h2>9. Contacto</h2>
    <p>
      Para cualquier duda sobre estos términos, escríbenos a
      <a :href="`mailto:${CONTACTO}`">{{ CONTACTO }}</a>.
    </p>
  </article>
</template>

<style scoped>
@import '@/assets/legal.css';
</style>
