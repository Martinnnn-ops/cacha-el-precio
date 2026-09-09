<script setup>
import { useRoute } from 'vue-router'
import { computed } from 'vue'

import { useCompararStore } from '@/modules/comparar/store/comparar.store'
import BaseButton from '@/shared/components/BaseButton.vue'

// Barra fija con lo que hay seleccionado.
//
// Lista las prendas y deja quitar cada una desde aquí. En el prototipo sólo
// contaba («Comparando 2 productos»), y para sacar una había que volver a
// buscar su tarjeta en la rejilla.
const comparar = useCompararStore()
const route = useRoute()

// En la propia comparación sobra: ya estás viéndola.
const visible = computed(
  () => comparar.cuantos > 0 && route.name !== 'comparar',
)
</script>

<template>
  <div v-if="visible" class="barra" role="region" aria-label="Comparación">
    <div class="barra__cuenta">
      <span class="mono">{{ comparar.cuantos }} de {{ comparar.MAXIMO }}</span>
    </div>

    <ul class="barra__lista">
      <li v-for="p in comparar.productos" :key="p.id" class="chip">
        <span class="chip__texto">{{ p.nombre }}</span>

        <button
          type="button"
          :aria-label="`Quitar ${p.nombre} de la comparación`"
          @click="comparar.quitar(p.id)"
        >
          ✕
        </button>
      </li>
    </ul>

    <p v-if="comparar.avisoTope" class="barra__aviso" role="status">
      Puedes comparar hasta {{ comparar.MAXIMO }} a la vez. Quita una para
      añadir otra.
    </p>

    <div class="barra__acciones">
      <BaseButton variante="secundario" tamano="chico" @click="comparar.vaciar()">
        Vaciar
      </BaseButton>

      <BaseButton tamano="chico" :to="{ name: 'comparar' }">
        Ver comparación
      </BaseButton>
    </div>
  </div>
</template>

<style scoped>
.barra {
  position: fixed;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: var(--cep-z-nav);
  display: flex;
  align-items: center;
  gap: var(--cep-sp-3);
  flex-wrap: wrap;
  padding: var(--cep-sp-3) var(--cep-gutter);
  background: var(--cep-ink);
  color: var(--cep-on-ink);
  box-shadow: 0 -4px 16px rgb(0 0 0 / 18%);
}
.barra__cuenta {
  flex: none;
  font-size: var(--cep-fs-sm);
}

.barra__lista {
  list-style: none;
  display: flex;
  align-items: center;
  gap: var(--cep-sp-2);
  margin: 0;
  padding: 0;
  flex: 1 1 auto;
  min-width: 0;
  overflow-x: auto;
  scrollbar-width: thin;
}
.chip {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-15);
  flex: none;
  max-width: 220px;
  padding: var(--cep-sp-1) var(--cep-sp-15) var(--cep-sp-1) var(--cep-sp-25);
  /* currentColor y no un blanco fijo: si el bloque cambia de fondo, el borde
     sigue viéndose. */
  border: 1px solid color-mix(in srgb, currentColor 30%, transparent);
  border-radius: var(--cep-r-pill);
  font-size: var(--cep-fs-xs);
}
.chip__texto {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.chip button {
  display: inline-flex;
  padding: 2px;
  background: none;
  border: none;
  color: inherit;
  opacity: 0.75;
  cursor: pointer;
  flex: none;
}
.chip button:hover {
  opacity: 1;
}

.barra__aviso {
  flex: 1 1 100%;
  order: 4;
  margin: 0;
  font-size: var(--cep-fs-xs);
  color: var(--cep-rate);
}

.barra__acciones {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-2);
  flex: none;
  margin-left: auto;
}
/* Sobre fondo oscuro los botones invierten sus colores. */
.barra__acciones :deep(.btn--primario) {
  background: var(--cep-on-ink);
  border-color: var(--cep-on-ink);
  color: var(--cep-ink);
}
.barra__acciones :deep(.btn--secundario) {
  border-color: color-mix(in srgb, currentColor 45%, transparent);
  color: inherit;
}

@media (max-width: 719px) {
  .barra__lista {
    flex: 1 1 100%;
    order: 2;
  }
  .barra__acciones {
    order: 3;
    flex: 1 1 100%;
    margin-left: 0;
  }
  .barra__acciones > * {
    flex: 1 1 0;
  }
}
</style>
