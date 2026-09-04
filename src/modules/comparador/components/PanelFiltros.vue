<script setup>
import { computed } from 'vue'
import { storeToRefs } from 'pinia'

import FiltroTiendas from '@/modules/comparador/components/FiltroTiendas.vue'
import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import BaseButton from '@/shared/components/BaseButton.vue'
import { formatearPrecio } from '@/shared/utils/formato'

// Panel lateral de filtros.
//
// Antes aquí sólo estaba el filtro de tiendas, que se oculta cuando hay una
// sola fuente de precio — y con el backend actual la hay. Resultado: la barra
// quedaba vacía y no se podía filtrar por nada, aunque el store sí sabía
// hacerlo por categoría.
const store = useComparadorStore()

const {
  categoria,
  marcas,
  talla,
  atributos,
  marcasDisponibles,
  tallasDisponibles,
  atributosDisponibles,
  precioMaximo,
  tiendas,
  tiendasActivas,
  conteoPorCategoria,
  rangoPrecios,
  hayFiltros,
} = storeToRefs(store)

// El tope arranca en el precio más alto del catálogo: mover la barra siempre
// acota, nunca amplía, que es como se espera que funcione un presupuesto.
const tope = computed({
  get: () => precioMaximo.value ?? rangoPrecios.value?.max ?? 0,
  set: (valor) => {
    const max = rangoPrecios.value?.max ?? 0

    precioMaximo.value = Number(valor) >= max ? null : Number(valor)
  },
})

// Paso redondo para que el presupuesto caiga en cifras que alguien diría en
// voz alta, no en 27.431.
const paso = computed(() => {
  const max = rangoPrecios.value?.max ?? 0

  return max > 100000 ? 5000 : 1000
})
</script>

<template>
  <div class="panel">
    <!-- ——— categoría ——— -->
    <fieldset v-if="conteoPorCategoria.length > 1" class="grupo">
      <legend class="grupo__titulo">Categoría</legend>

      <div class="grupo__opciones">
        <button
          type="button"
          class="pildora"
          :class="{ 'pildora--activa': categoria === '' }"
          :aria-pressed="categoria === ''"
          @click="categoria = ''"
        >
          Todas
        </button>

        <button
          v-for="c in conteoPorCategoria"
          :key="c.nombre"
          type="button"
          class="pildora"
          :class="{ 'pildora--activa': categoria === c.nombre }"
          :aria-pressed="categoria === c.nombre"
          @click="categoria = categoria === c.nombre ? '' : c.nombre"
        >
          {{ c.nombre }}
          <span class="pildora__cuenta">{{ c.total }}</span>
        </button>
      </div>
    </fieldset>

    <!-- ——— marca ——— -->
    <fieldset v-if="marcasDisponibles.length > 1" class="grupo">
      <legend class="grupo__titulo">Marca</legend>

      <div class="grupo__opciones">
        <button
          v-for="m in marcasDisponibles"
          :key="m.nombre"
          type="button"
          class="pildora"
          :class="{ 'pildora--activa': marcas.includes(m.nombre) }"
          :aria-pressed="marcas.includes(m.nombre)"
          @click="store.alternarMarca(m.nombre)"
        >
          {{ m.nombre }}
          <span class="pildora__cuenta">{{ m.total }}</span>
        </button>
      </div>
    </fieldset>

    <!-- ——— talla ——— -->
    <fieldset v-if="tallasDisponibles.length > 1" class="grupo">
      <legend class="grupo__titulo">Talla</legend>

      <div class="grupo__opciones">
        <button
          v-for="t in tallasDisponibles"
          :key="t.nombre"
          type="button"
          class="pildora pildora--talla"
          :class="{ 'pildora--activa': talla === t.nombre }"
          :aria-pressed="talla === t.nombre"
          @click="talla = talla === t.nombre ? '' : t.nombre"
        >
          {{ t.nombre }}
          <span class="pildora__cuenta">{{ t.total }}</span>
        </button>
      </div>

      <p class="grupo__nota mono">
        Cuenta las tiendas con stock: una talla puede estar en una y no en otra.
      </p>
    </fieldset>

    <!-- ——— atributos ———
         Cuáles salen depende de lo que se esté mirando: al filtrar zapatillas
         aparecen Suela y Terreno; con ropa, Corte y Cuello. Se derivan del
         catálogo, no de una lista escrita a mano por tipo de prenda. -->
    <fieldset
      v-for="atributo in atributosDisponibles"
      :key="atributo.clave"
      class="grupo"
    >
      <legend class="grupo__titulo">{{ atributo.clave }}</legend>

      <div class="grupo__opciones">
        <button
          v-for="v in atributo.valores"
          :key="v.valor"
          type="button"
          class="pildora"
          :class="{ 'pildora--activa': atributos[atributo.clave] === v.valor }"
          :aria-pressed="atributos[atributo.clave] === v.valor"
          @click="store.ponerAtributo(atributo.clave, v.valor)"
        >
          {{ v.valor }}
          <span class="pildora__cuenta">{{ v.total }}</span>
        </button>
      </div>
    </fieldset>

    <!-- ——— presupuesto ——— -->
    <fieldset v-if="rangoPrecios" class="grupo">
      <legend class="grupo__titulo">Presupuesto</legend>

      <label class="rango">
        <span class="rango__valor mono">
          <template v-if="precioMaximo === null">
            Sin tope · hasta {{ formatearPrecio(rangoPrecios.max) }}
          </template>
          <template v-else>
            Hasta {{ formatearPrecio(precioMaximo) }}
          </template>
        </span>

        <input
          v-model="tope"
          type="range"
          class="rango__control"
          :min="rangoPrecios.min"
          :max="rangoPrecios.max"
          :step="paso"
          aria-label="Precio máximo"
        />
      </label>
    </fieldset>

    <!-- ——— tiendas: sólo si hay más de una que comparar ——— -->
    <FiltroTiendas
      v-if="tiendas.length > 1"
      v-model="tiendasActivas"
      :tiendas="tiendas"
    />

    <BaseButton
      v-if="hayFiltros"
      variante="secundario"
      tamano="chico"
      bloque
      @click="store.limpiarFiltros()"
    >
      Limpiar filtros
    </BaseButton>
  </div>
</template>

<style scoped>
.panel {
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-5);
}

.grupo {
  margin: 0;
  padding: 0;
  border: none;
}
.grupo__titulo {
  padding: 0;
  margin-bottom: var(--cep-sp-2);
  font-family: var(--cep-font-mono);
  font-size: var(--cep-fs-2xs);
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--cep-muted);
}
.grupo__opciones {
  display: flex;
  flex-wrap: wrap;
  gap: var(--cep-sp-15);
}

.pildora {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-15);
  min-height: var(--cep-control-h-sm);
  padding: 0 var(--cep-sp-3);
  background: var(--cep-surface);
  color: var(--cep-ink);
  border: 1px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-pill);
  font-size: var(--cep-fs-sm);
  cursor: pointer;
  transition:
    background var(--cep-dur-1) var(--cep-ease),
    border-color var(--cep-dur-1) var(--cep-ease),
    color var(--cep-dur-1) var(--cep-ease);
}
.pildora:hover {
  border-color: var(--cep-ink);
}
.pildora--activa {
  background: var(--cep-ink);
  border-color: var(--cep-ink);
  color: var(--cep-on-ink);
}
.pildora__cuenta {
  font-family: var(--cep-font-mono);
  font-size: var(--cep-fs-2xs);
  opacity: 0.65;
}

.pildora--talla {
  min-width: 52px;
  justify-content: center;
  font-family: var(--cep-font-mono);
}
.grupo__nota {
  margin: var(--cep-sp-2) 0 0;
  font-size: var(--cep-fs-xs);
  line-height: var(--cep-lh-snug);
  color: var(--cep-muted);
}

.rango {
  display: block;
}
.rango__valor {
  display: block;
  margin-bottom: var(--cep-sp-2);
  font-size: var(--cep-fs-sm);
}
.rango__control {
  width: 100%;
  accent-color: var(--cep-accent);
  cursor: pointer;
}
</style>
