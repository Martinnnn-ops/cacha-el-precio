<script setup>
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'

import { useCuentaStore } from '@/modules/cuenta/store/cuenta.store'
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'

// Pantalla de vuelta desde Google. Sólo procesa el código y sale: no es un
// sitio donde quedarse.
const route = useRoute()
const router = useRouter()
const cuenta = useCuentaStore()
const { error } = storeToRefs(cuenta)

const procesando = ref(true)

onMounted(async () => {
  const destino = await cuenta.procesarRetorno({
    code: typeof route.query.code === 'string' ? route.query.code : null,
    state: typeof route.query.state === 'string' ? route.query.state : null,
    error: typeof route.query.error === 'string' ? route.query.error : null,
  })

  procesando.value = false

  if (cuenta.autenticado) {
    // `replace` y no `push`: el código de autorización queda en la URL y no
    // debe poder recuperarse con el botón atrás. Ya está consumido y volver
    // aquí sólo daría un error confuso.
    router.replace(destino ?? { name: 'inicio' })
  }
})
</script>

<template>
  <BaseTicket class="retorno">
    <template v-if="procesando">
      <p class="retorno__punto" aria-hidden="true" />
      <p class="retorno__texto" role="status">Entrando…</p>
    </template>

    <template v-else>
      <h1 class="display retorno__titulo">No pudimos entrar</h1>

      <p class="retorno__texto">
        {{ error ?? 'Algo interrumpió el inicio de sesión.' }}
      </p>

      <div class="retorno__acciones">
        <BaseButton :to="{ name: 'entrar' }">Volver a intentarlo</BaseButton>
        <BaseButton variante="secundario" to="/">Ir al inicio</BaseButton>
      </div>
    </template>
  </BaseTicket>
</template>

<style scoped>
.retorno {
  width: 100%;
  max-width: 400px;
  text-align: center;
}
.retorno__titulo {
  margin: 0 0 var(--cep-sp-2);
  font-size: var(--cep-fs-xl);
}
.retorno__texto {
  margin: 0;
  color: var(--cep-muted);
}
.retorno__punto {
  width: 28px;
  height: 28px;
  margin: var(--cep-sp-2) auto var(--cep-sp-4);
  border: 3px solid var(--cep-line-media);
  border-top-color: var(--cep-accent);
  border-radius: 50%;
  animation: girar 0.8s linear infinite;
}
@keyframes girar {
  to {
    transform: rotate(360deg);
  }
}
.retorno__acciones {
  display: flex;
  gap: var(--cep-sp-3);
  justify-content: center;
  flex-wrap: wrap;
  margin-top: var(--cep-sp-5);
}
</style>
