<script setup>
import { computed } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import { storeToRefs } from 'pinia'

import { useCuentaStore } from '@/modules/cuenta/store/cuenta.store'
import BotonGoogle from '@/modules/cuenta/components/BotonGoogle.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'
import { rutaInternaSegura } from '@/shared/utils/rutas'

const route = useRoute()
const cuenta = useCuentaStore()
const { entrando, error, disponible } = storeToRefs(cuenta)

// El destino viene del query string, o sea de quien haya mandado el enlace:
// sólo se acepta si es una ruta interna de la propia aplicación.
const volver = computed(() => rutaInternaSegura(route.query.volver))

// Con Google no hay diferencia entre entrar y registrarse: la primera vez se
// crea la cuenta. La copia de la pantalla lo dice para que nadie busque un
// formulario de registro que no existe.
const esRegistro = computed(() => route.name === 'registro')
</script>

<template>
  <BaseTicket class="acceso__tarjeta">
    <h1 class="display acceso__titulo">
      {{ esRegistro ? 'Crea tu cuenta' : 'Entra a tu cuenta' }}
    </h1>

    <p class="acceso__bajada">
      Para guardar las prendas que te interesan y avisarte cuando bajen de
      precio.
    </p>

    <BotonGoogle
      :texto="esRegistro ? 'Registrarme con Google' : 'Continuar con Google'"
      :cargando="entrando"
      :deshabilitado="!disponible"
      @click="cuenta.entrarConGoogle(volver)"
    />

    <p v-if="error" class="acceso__error" role="alert">{{ error }}</p>

    <p class="acceso__nota">
      {{
        esRegistro
          ? 'Si ya entraste antes con este correo, te llevamos a tu cuenta de siempre.'
          : 'Si es tu primera vez, te creamos la cuenta en el momento.'
      }}
    </p>

    <div class="divisor" />

    <p class="acceso__legal">
      Al continuar aceptas los
      <RouterLink :to="{ name: 'terminos' }">términos y condiciones</RouterLink>
      y la
      <RouterLink :to="{ name: 'privacidad' }">política de privacidad</RouterLink>.
    </p>
  </BaseTicket>
</template>

<style scoped>
@import '@/assets/acceso.css';
</style>
