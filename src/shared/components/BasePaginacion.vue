<script setup>
import { computed } from 'vue'

import BaseDropdown from '@/shared/components/BaseDropdown.vue'

// Paginación de resultados — mismo patrón visual que el desplegable de
// categoría/marca en el panel de filtros (BaseDropdown + flecha SVG), para
// que no se sienta como un componente ajeno pegado al final de la grilla.
//
// Server-side: emite la página y el tamaño elegidos, no corta ningún arreglo
// acá. Quien use este componente (la vista de resultados) es responsable de
// volver a pedir datos al store/API cuando cambien.
const props = defineProps({
  paginaActual: { type: Number, required: true },
  itemsPorPagina: { type: Number, required: true },
  totalItems: { type: Number, required: true },
  opcionesPorPagina: { type: Array, default: () => [20, 40, 60] },
})

const emit = defineEmits(['update:paginaActual', 'update:itemsPorPagina'])

const totalPaginas = computed(() =>
  Math.max(1, Math.ceil(props.totalItems / props.itemsPorPagina)),
)

const inicio = computed(() =>
  props.totalItems === 0 ? 0 : (props.paginaActual - 1) * props.itemsPorPagina + 1,
)

const fin = computed(() =>
  Math.min(props.paginaActual * props.itemsPorPagina, props.totalItems),
)

const esPrimera = computed(() => props.paginaActual <= 1)
const esUltima = computed(() => props.paginaActual >= totalPaginas.value)

function irA(pagina) {
  const destino = Math.min(Math.max(1, pagina), totalPaginas.value)
  if (destino !== props.paginaActual) emit('update:paginaActual', destino)
}

function cambiarTamano(n) {
  if (n === props.itemsPorPagina) return
  emit('update:itemsPorPagina', n)
  // Un tamaño nuevo cambia qué cae en cada página: siempre se vuelve a la 1
  // para no quedar parado en una página que ya no tiene los mismos ítems.
  emit('update:paginaActual', 1)
}
</script>

<template>
  <div class="paginacion">
    <div class="paginacion__tamano">
      <span class="paginacion__etiqueta">Items por pág.</span>

      <div class="selector-dropdown">
        <BaseDropdown etiqueta="Ítems por página" :ancho="90">
          <template #disparador="{ abierto }">
            <span class="selector__texto">{{ itemsPorPagina }}</span>
            <svg
              class="selector__flecha"
              :class="{ 'selector__flecha--abierta': abierto }"
              viewBox="0 0 20 20"
              aria-hidden="true"
            >
              <path
                d="m5 7.5 5 5 5-5"
                stroke="currentColor"
                stroke-width="1.8"
                fill="none"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
            </svg>
          </template>

          <template #default="{ cerrar }">
            <div class="menu__lista">
              <button
                v-for="n in opcionesPorPagina"
                :key="n"
                type="button"
                class="menu__opcion"
                :class="{ 'menu__opcion--activa': itemsPorPagina === n }"
                @click="cambiarTamano(n); cerrar()"
              >
                {{ n }}
              </button>
            </div>
          </template>
        </BaseDropdown>
      </div>
    </div>

    <div class="paginacion__rango mono">
      {{ inicio }}–{{ fin }} de {{ totalItems }}
    </div>

    <nav class="paginacion__nav" aria-label="Paginación de resultados">
      <button
        type="button"
        class="paginacion__boton"
        :disabled="esPrimera"
        aria-label="Primera página"
        @click="irA(1)"
      >
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <path d="M12.5 4.5 7 10l5.5 5.5" stroke="currentColor" stroke-width="1.8" fill="none" stroke-linecap="round" stroke-linejoin="round" />
          <path d="M8 4.5 2.5 10 8 15.5" stroke="currentColor" stroke-width="1.8" fill="none" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </button>

      <button
        type="button"
        class="paginacion__boton"
        :disabled="esPrimera"
        aria-label="Página anterior"
        @click="irA(paginaActual - 1)"
      >
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <path d="M12.5 4.5 7 10l5.5 5.5" stroke="currentColor" stroke-width="1.8" fill="none" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </button>

      <span class="paginacion__pagina mono">{{ paginaActual }} / {{ totalPaginas }}</span>

      <button
        type="button"
        class="paginacion__boton"
        :disabled="esUltima"
        aria-label="Página siguiente"
        @click="irA(paginaActual + 1)"
      >
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <path d="M7.5 4.5 13 10l-5.5 5.5" stroke="currentColor" stroke-width="1.8" fill="none" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </button>

      <button
        type="button"
        class="paginacion__boton"
        :disabled="esUltima"
        aria-label="Última página"
        @click="irA(totalPaginas)"
      >
        <svg viewBox="0 0 20 20" aria-hidden="true">
          <path d="M7.5 4.5 13 10l-5.5 5.5" stroke="currentColor" stroke-width="1.8" fill="none" stroke-linecap="round" stroke-linejoin="round" />
          <path d="M12 4.5 17.5 10 12 15.5" stroke="currentColor" stroke-width="1.8" fill="none" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
      </button>
    </nav>
  </div>
</template>

<style scoped>
.paginacion {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: var(--cep-sp-3);
  padding: var(--cep-sp-3) 0;
  border-top: 1px solid var(--cep-line);
}

.paginacion__tamano {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-2);
}
.paginacion__etiqueta {
  font-size: var(--cep-fs-sm);
  color: var(--cep-muted);
  white-space: nowrap;
}

.selector-dropdown :deep(.menu__boton) {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-1);
  min-height: var(--cep-control-h-sm);
  padding: 0 var(--cep-sp-2);
  border: 1px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-sm);
  background: var(--cep-surface);
}
.selector-dropdown :deep(.menu__boton:hover),
.selector-dropdown :deep(.menu__boton--abierto) {
  border-color: var(--cep-ink);
}
.selector__texto {
  font-size: var(--cep-fs-sm);
}
.selector__flecha {
  width: 12px;
  height: 12px;
  color: var(--cep-muted);
  transition: transform var(--cep-dur-1) var(--cep-ease);
}
.selector__flecha--abierta {
  transform: rotate(180deg);
}
.menu__lista {
  display: flex;
  flex-direction: column;
}
.menu__opcion {
  min-height: var(--cep-control-h-sm);
  padding: var(--cep-sp-1) var(--cep-sp-2);
  border: none;
  border-radius: var(--cep-r-sm);
  background: none;
  color: var(--cep-ink);
  font: inherit;
  font-size: var(--cep-fs-sm);
  text-align: left;
  cursor: pointer;
}
.menu__opcion:hover {
  background: var(--cep-wash);
}
.menu__opcion--activa {
  color: var(--cep-accent);
  font-weight: 600;
}

.paginacion__rango {
  font-size: var(--cep-fs-sm);
  color: var(--cep-muted);
}

.paginacion__nav {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-1);
}
.paginacion__boton {
  display: flex;
  align-items: center;
  justify-content: center;
  width: var(--cep-control-h-sm);
  height: var(--cep-control-h-sm);
  border: 1px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-sm);
  background: var(--cep-surface);
  color: var(--cep-ink);
  cursor: pointer;
  transition:
    background var(--cep-dur-1) var(--cep-ease),
    border-color var(--cep-dur-1) var(--cep-ease),
    opacity var(--cep-dur-1) var(--cep-ease);
}
.paginacion__boton svg {
  width: 16px;
  height: 16px;
}
.paginacion__boton:hover:not(:disabled) {
  border-color: var(--cep-ink);
}
.paginacion__boton:disabled {
  opacity: 0.35;
  cursor: default;
}
.paginacion__pagina {
  min-width: 52px;
  text-align: center;
  font-size: var(--cep-fs-sm);
  color: var(--cep-muted);
}
</style>
