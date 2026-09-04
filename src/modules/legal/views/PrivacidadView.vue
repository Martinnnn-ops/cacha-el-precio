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
    <h1 class="display legal__titulo">Política de privacidad</h1>
    <p class="legal__fecha">Última actualización: {{ FECHA_VIGENCIA }}</p>

    <h2>Qué datos guardamos</h2>

    <h3>Si no tienes cuenta</h3>
    <p>
      Nada que te identifique. Tus preferencias —el tema claro u oscuro y qué
      tiendas quieres ver— se guardan
      <strong>en tu propio navegador</strong>, no en nuestros servidores, y
      desaparecen si borras los datos del sitio.
    </p>

    <h3>Si creas una cuenta</h3>
    <ul>
      <li>Tu nombre y tu correo, para identificarte y avisarte.</li>
      <li>Tu contraseña, guardada cifrada. Nadie del equipo puede leerla.</li>
      <li>Los productos que sigues, para poder mandarte los avisos.</li>
    </ul>

    <h2>Para qué los usamos</h2>
    <p>
      Sólo para hacer funcionar el servicio: mantener tu sesión y avisarte
      cuando baje el precio de algo que sigues. No vendemos tus datos ni los
      cedemos a las tiendas.
    </p>

    <h2>Publicidad</h2>
    <p>
      Los anuncios que ves los sirve <strong>Google AdSense</strong>, no
      nosotros. Para hacerlo, Google y sus socios pueden usar cookies e
      identificadores de tu navegador y mostrarte anuncios basados en tus
      visitas a este y a otros sitios.
    </p>
    <p>
      Puedes revisar y cambiar qué usa Google en
      <a
        href="https://myadcenter.google.com/"
        target="_blank"
        rel="noopener noreferrer"
      >Mi centro de anuncios</a>, y leer cómo trata los datos en la
      <a
        href="https://policies.google.com/technologies/partner-sites"
        target="_blank"
        rel="noopener noreferrer"
      >política de privacidad y condiciones de Google</a>.
    </p>
    <p>
      Nosotros no recibimos de Google ningún dato que te identifique: sólo
      cifras agregadas de cuántos anuncios se vieron.
    </p>

    <h2>Enlaces de afiliado</h2>
    <p>
      Algunos enlaces a tiendas son de afiliado. Cuando pulsas uno, la tienda
      de destino puede registrar que llegaste desde aquí. Lo que ocurra dentro
      del sitio de la tienda se rige por la política de privacidad de esa
      tienda, no por la nuestra.
    </p>

    <h2>Tus derechos</h2>
    <p>
      Puedes pedirnos acceso a tus datos, su corrección o su eliminación, y
      cerrar tu cuenta cuando quieras. Escríbenos a
      <a :href="`mailto:${CONTACTO}`">{{ CONTACTO }}</a> y respondemos.
    </p>

    <h2>Conservación</h2>
    <p>
      Mantenemos tus datos mientras la cuenta esté activa. Al cerrarla se
      eliminan, salvo lo que haya que conservar por obligación legal.
    </p>

    <h2>Cambios</h2>
    <p>
      Si cambiamos algo relevante lo avisaremos en la página antes de que entre
      en vigor.
    </p>
  </article>
</template>

<style scoped>
@import '@/assets/legal.css';
</style>
