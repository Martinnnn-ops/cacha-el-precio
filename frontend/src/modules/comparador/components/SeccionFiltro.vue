<script setup>
import { useId } from 'vue'

// Una sección plegable del panel de filtros. Antes era un <details>, que no
// se puede animar en todos los navegadores: abría y cerraba de golpe. Aquí el
// contenido crece de 0 a su alto con grid-template-rows (0fr → 1fr), que sí
// se anima y no obliga a medir alturas con JavaScript.
defineProps({
  titulo: { type: String, required: true },
  // Lo elegido en la sección, para leerlo con la sección plegada.
  elegido: { type: String, default: '' },
})

const abierta = defineModel('abierta', { type: Boolean, default: true })
const id = useId()
</script>

<template>
  <section class="seccion" :class="{ 'seccion--abierta': abierta }">
    <button
      type="button"
      class="seccion__titulo"
      :aria-expanded="abierta"
      :aria-controls="id"
      @click="abierta = !abierta"
    >
      <span>{{ titulo }}</span>
      <span v-if="elegido" class="seccion__elegido">{{ elegido }}</span>
      <span class="seccion__flecha" aria-hidden="true" />
    </button>

    <!-- inert: plegada, lo de adentro no se alcanza con el tabulador ni lo lee
         un lector de pantalla, igual que con el <details> de antes. -->
    <div :id="id" class="seccion__cuerpo" :inert="!abierta">
      <div class="seccion__recorte">
        <div class="seccion__contenido">
          <slot />
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
/* Cada sección separada por una línea punteada, como los tickets del sitio. */
.seccion {
  padding: var(--cep-sp-2) 0;
  border-bottom: 1px dashed var(--cep-line-media);
}
.seccion:first-child {
  padding-top: 0;
}

.seccion__titulo {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-2);
  width: 100%;
  min-height: 36px;
  padding: 0;
  border: none;
  background: none;
  cursor: pointer;
  text-align: left;
  font-family: var(--cep-font-mono);
  font-size: var(--cep-fs-2xs);
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--cep-muted);
  transition: color var(--cep-dur-1) var(--cep-ease);
}
.seccion__titulo:hover {
  color: var(--cep-ink);
}
.seccion__titulo:focus-visible {
  outline: 2px solid var(--cep-accent);
  outline-offset: 2px;
  border-radius: var(--cep-r-xs);
}

.seccion__elegido {
  overflow: hidden;
  min-width: 0;
  padding: 1px var(--cep-sp-2);
  border-radius: var(--cep-r-pill);
  background: var(--cep-ink);
  color: var(--cep-on-ink);
  letter-spacing: 0;
  text-transform: none;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* Botón circular con un chevrón dibujado con bordes: sin íconos ni librerías. */
.seccion__flecha {
  position: relative;
  flex: none;
  width: 22px;
  height: 22px;
  margin-left: auto;
  border: 1px solid var(--cep-line-fuerte);
  border-radius: 50%;
  transition:
    transform var(--cep-dur-3) var(--cep-ease),
    border-color var(--cep-dur-1) var(--cep-ease),
    background var(--cep-dur-1) var(--cep-ease);
}
.seccion__flecha::before {
  content: '';
  position: absolute;
  top: 6px;
  left: 7px;
  width: 6px;
  height: 6px;
  border-right: 1.5px solid currentColor;
  border-bottom: 1.5px solid currentColor;
  transform: rotate(45deg);
}
.seccion__titulo:hover .seccion__flecha {
  border-color: var(--cep-ink);
  background: var(--cep-surface);
}
.seccion--abierta .seccion__flecha {
  transform: rotate(180deg);
}

.seccion__cuerpo {
  display: grid;
  grid-template-rows: 0fr;
  opacity: 0;
  transition:
    grid-template-rows var(--cep-dur-3) var(--cep-ease),
    opacity var(--cep-dur-2) var(--cep-ease);
}
.seccion--abierta .seccion__cuerpo {
  grid-template-rows: 1fr;
  opacity: 1;
}
/* El recorte es lo que permite crecer desde 0. Pero también cortaría el borde
   y el anillo de foco de lo que va pegado al lado (el buscador de marcas se
   veía mordido): por eso el contenido lleva 4 px de aire que el margen
   negativo devuelve, y la sección no se corre de su lugar. */
.seccion__recorte {
  min-height: 0;
  overflow: hidden;
  margin: 0 -4px;
}
.seccion__contenido {
  padding: var(--cep-sp-2) 4px var(--cep-sp-2);
}
</style>
