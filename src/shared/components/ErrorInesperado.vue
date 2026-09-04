<script setup>
import BaseButton from '@/shared/components/BaseButton.vue'

// Lo que ve el usuario cuando algo se rompe de verdad. Sin esto la pantalla se
// queda en blanco, que es la peor forma de fallar: no informa y no ofrece
// salida. El detalle técnico va a la consola en desarrollo, nunca aquí.
import { CONTACTO } from '@/shared/config/sitio'

defineEmits(['reintentar'])
</script>

<template>
  <section class="fallo" role="alert">
    <!-- El icono va DENTRO de una forma con fondo, en vez de ampliarlo hasta
         llenar el hueco. Un icono dibujado a 24px y estirado a 56 se ve tosco
         y sin detalle; encerrarlo deja que ocupe el espacio sin deformarse. -->
    <span class="fallo__marca" aria-hidden="true">
      <svg viewBox="0 0 24 24" width="26" height="26" fill="none">
        <path
          d="M12 8.5v5"
          stroke="currentColor"
          stroke-width="2.2"
          stroke-linecap="round"
        />
        <circle cx="12" cy="17" r="1.3" fill="currentColor" />
        <path
          d="M10.3 3.9 2.5 17.4A2 2 0 0 0 4.2 20.4h15.6a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z"
          stroke="currentColor"
          stroke-width="2"
          stroke-linejoin="round"
        />
      </svg>
    </span>

    <h1 class="display fallo__titulo">Algo se nos rompió</h1>

    <p class="fallo__texto">
      No pudimos mostrar esta página. Vuelve a intentarlo; si sigue pasando,
      escríbenos a <a :href="`mailto:${CONTACTO}`">{{ CONTACTO }}</a> y lo
      revisamos.
    </p>

    <div class="fallo__acciones">
      <BaseButton @click="$emit('reintentar')">Reintentar</BaseButton>
      <BaseButton variante="secundario" to="/">Ir al inicio</BaseButton>
    </div>
  </section>
</template>

<style scoped>
.fallo {
  max-width: 46ch;
  margin: 0 auto;
  padding: var(--cep-sp-16) 0;
  text-align: center;
}
.fallo__marca {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 64px;
  height: 64px;
  border-radius: 50%;
  /* El acento se mezcla con el fondo en vez de usar un gris: así el círculo
     acompaña al aviso sin competir con el texto. */
  background: color-mix(in srgb, var(--cep-alerta) 12%, transparent);
  color: var(--cep-alerta);
}
.fallo__titulo {
  margin: var(--cep-sp-4) 0 var(--cep-sp-2);
  font-size: var(--cep-fs-2xl);
}
.fallo__texto {
  margin: 0 0 var(--cep-sp-6);
  color: var(--cep-muted);
}
.fallo__acciones {
  display: flex;
  gap: var(--cep-sp-3);
  justify-content: center;
  flex-wrap: wrap;
}
</style>
