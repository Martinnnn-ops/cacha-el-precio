<script setup>
import { nextTick, onBeforeUnmount, ref, useTemplateRef, watch } from 'vue'

// Menú desplegable. Lo usan las tres piezas nuevas de la cabecera (tiendas,
// tema, cuenta), así que el comportamiento vive aquí una sola vez:
// cerrar al pulsar fuera, cerrar con Escape y devolver el foco al disparador.
// Un menú que no cierra con Escape deja atrapado a quien navega con teclado.

defineProps({
  etiqueta: { type: String, required: true },
  // Ancla el panel a la derecha del disparador (menús del extremo derecho).
  alineado: {
    type: String,
    default: 'izquierda',
    validator: (v) => ['izquierda', 'derecha'].includes(v),
  },
  ancho: { type: Number, default: 240 },
})

const abierto = ref(false)
const raiz = useTemplateRef('raiz')
const disparador = useTemplateRef('disparador')
const panel = useTemplateRef('panel')

function alternar() {
  abierto.value = !abierto.value
}

function cerrar({ devolverFoco = false } = {}) {
  if (!abierto.value) return

  abierto.value = false

  if (devolverFoco) disparador.value?.focus()
}

function alPulsarFuera(evento) {
  if (!raiz.value?.contains(evento.target)) cerrar()
}

function alTeclear(evento) {
  if (evento.key === 'Escape') {
    evento.preventDefault()
    cerrar({ devolverFoco: true })
  }
}

watch(abierto, async (estaAbierto) => {
  if (estaAbierto) {
    document.addEventListener('pointerdown', alPulsarFuera)
    document.addEventListener('keydown', alTeclear)

    await nextTick()
    panel.value?.focus()
  } else {
    document.removeEventListener('pointerdown', alPulsarFuera)
    document.removeEventListener('keydown', alTeclear)
  }
})

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', alPulsarFuera)
  document.removeEventListener('keydown', alTeclear)
})

defineExpose({ cerrar })
</script>

<template>
  <div ref="raiz" class="menu">
    <button
      ref="disparador"
      type="button"
      class="menu__boton"
      :class="{ 'menu__boton--abierto': abierto }"
      :aria-expanded="abierto"
      aria-haspopup="true"
      :aria-label="etiqueta"
      @click="alternar"
    >
      <slot name="disparador" :abierto="abierto" />
    </button>

    <Transition name="menu">
      <div
        v-if="abierto"
        ref="panel"
        class="menu__panel"
        :class="{ 'menu__panel--derecha': alineado === 'derecha' }"
        :style="{ minWidth: `${ancho}px` }"
        role="group"
        :aria-label="etiqueta"
        tabindex="-1"
      >
        <slot :cerrar="cerrar" />
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.menu {
  position: relative;
}

.menu__boton {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--cep-sp-15);
  min-height: var(--cep-control-h);
  padding: 0 var(--cep-sp-25);
  background: none;
  border: 1px solid transparent;
  border-radius: var(--cep-r-sm);
  color: var(--cep-ink);
  cursor: pointer;
  transition:
    background var(--cep-dur-1) var(--cep-ease),
    border-color var(--cep-dur-1) var(--cep-ease);
}
.menu__boton:hover,
.menu__boton--abierto {
  background: var(--cep-wash);
  border-color: var(--cep-line-media);
}

.menu__panel {
  position: absolute;
  top: calc(100% + var(--cep-sp-15));
  left: 0;
  z-index: var(--cep-z-dropdown);
  padding: var(--cep-sp-2);
  background: var(--cep-surface);
  border: 1px solid var(--cep-line-media);
  border-radius: var(--cep-r-lg);
  box-shadow: var(--cep-shadow-3);
  outline: none;
}
.menu__panel--derecha {
  left: auto;
  right: 0;
}
/* En móvil un panel anclado al borde derecho se salía de la pantalla. */
@media (max-width: 479px) {
  .menu__panel {
    max-width: calc(100vw - var(--cep-sp-8));
  }
}

.menu-enter-active,
.menu-leave-active {
  transition:
    opacity var(--cep-dur-2) var(--cep-ease),
    transform var(--cep-dur-2) var(--cep-ease);
}
.menu-enter-from,
.menu-leave-to {
  opacity: 0;
  transform: translateY(-4px) scale(0.98);
}
</style>
