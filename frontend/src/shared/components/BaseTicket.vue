<script setup>
import { computed } from 'vue'
import { RouterLink } from 'vue-router'

// Tarjeta-ticket con sus muescas troqueladas. Las muescas van aquí y no en el
// marcado de cada tarjeta porque acordarse de poner las dos —arriba y abajo—
// en cada sitio es exactamente el tipo de detalle que se termina olvidando.
const props = defineProps({
  to: { type: [String, Object], default: null },
  muescas: {
    type: String,
    default: 'ambas',
    validator: (v) => ['ambas', 'arriba', 'abajo', 'ninguna'].includes(v),
  },
  relleno: { type: Boolean, default: true },
  // Tarjeta que vive en una rejilla junto a otras.
  //
  // Estira el ticket a todo el alto de su celda y convierte el cuerpo en una
  // columna flexible, que es lo que permite a la tarjeta empujar su pie hacia
  // abajo con `margin-top: auto`. Sin esto el <div> del cuerpo corta la cadena
  // flex y los pies de una misma fila quedan a alturas distintas.
  columna: { type: Boolean, default: false },
})

const etiqueta = computed(() => (props.to ? RouterLink : 'div'))
const arriba = computed(() => ['ambas', 'arriba'].includes(props.muescas))
const abajo = computed(() => ['ambas', 'abajo'].includes(props.muescas))
</script>

<template>
  <component
    :is="etiqueta"
    :to="to ?? undefined"
    class="ticket"
    :class="{ 'ticket--enlace': to, 'ticket--columna': columna }"
  >
    <span v-if="arriba" class="ticket__muescas ticket__muescas--arriba" />

    <div :class="{ ticket__cuerpo: relleno, ticket__contenido: columna }">
      <slot />
    </div>

    <span v-if="abajo" class="ticket__muescas ticket__muescas--abajo" />
  </component>
</template>
