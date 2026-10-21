<script setup>
import { computed, reactive, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'

import FiltroTiendas from '@/modules/comparador/components/FiltroTiendas.vue'
import SeccionFiltro from '@/modules/comparador/components/SeccionFiltro.vue'
import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import BaseButton from '@/shared/components/BaseButton.vue'
import { formatearPrecio } from '@/shared/utils/formato'
import { GRUPOS_TALLA } from '@/shared/utils/tallas'

// Panel lateral de filtros.
//
// Antes aquí sólo estaba el filtro de tiendas, que se oculta cuando hay una
// sola fuente de precio — y con el backend actual la hay. Resultado: la barra
// quedaba vacía y no se podía filtrar por nada, aunque el store sí sabía
// hacerlo por categoría.
const store = useComparadorStore()
const buscarMarca = ref('')
const limiteMarcas = ref(10)
watch(buscarMarca, () => { limiteMarcas.value = 10 })
const marcasVisibles = computed(() => store.marcasDisponibles.filter(m =>
  m.nombre.toLocaleLowerCase('es').includes(buscarMarca.value.trim().toLocaleLowerCase('es'))))

// Cada sección se pliega. Antes el panel medía cerca de 2.000 px por las ~60
// tallas, y para llegar al presupuesto había que pasar por todas. Talla y los
// atributos parten plegados, salvo que ya traigan algo elegido (un enlace con
// filtros); el resto, abierto.
const abiertos = reactive({
  categoria: true,
  marca: true,
  talla: store.talla !== '',
  presupuesto: true,
})

const {
  categoria,
  marcas,
  talla,
  atributos,
  marcasDisponibles,
  tallasDisponibles,
  atributosDisponibles,
  precioMinimo,
  precioMaximo,
  tiendas,
  tiendasActivas,
  conteoPorCategoria,
  rangoPrecios,
  hayFiltros,
} = storeToRefs(store)

// El piso y el techo arrancan en los extremos del catálogo: mover una barra
// siempre acota, nunca amplía. Si el piso pasa el techo, el último que se movió
// manda y empuja al otro, en vez de dejar un rango imposible.
const piso = computed({
  get: () => precioMinimo.value ?? rangoPrecios.value?.min ?? 0,
  set: (valor) => {
    const min = rangoPrecios.value?.min ?? 0
    const max = rangoPrecios.value?.max ?? 0
    const v = Number(valor)

    precioMinimo.value = v <= min ? null : v

    if (precioMaximo.value !== null && v > precioMaximo.value) {
      precioMaximo.value = v >= max ? null : v
    }
  },
})

const tope = computed({
  get: () => precioMaximo.value ?? rangoPrecios.value?.max ?? 0,
  set: (valor) => {
    const min = rangoPrecios.value?.min ?? 0
    const max = rangoPrecios.value?.max ?? 0
    const v = Number(valor)

    precioMaximo.value = v >= max ? null : v

    if (precioMinimo.value !== null && v < precioMinimo.value) {
      precioMinimo.value = v <= min ? null : v
    }
  },
})

// Atajos con cifras redondas. Se ocultan si caen fuera del catálogo o si
// cruzarían el otro extremo: un atajo que deja la lista vacía no es un atajo.
const atajosPrecio = computed(() => {
  const rango = rangoPrecios.value

  if (!rango) return []

  const desde = [10000, 20000]
    .filter((v) => v > rango.min && v < rango.max)
    .filter((v) => precioMaximo.value === null || v < precioMaximo.value)
    .map((v) => ({
      tipo: 'desde',
      valor: v,
      etiqueta: `Desde ${formatearPrecio(v)}`,
      activo: precioMinimo.value === v,
    }))

  const hasta = [20000, 30000]
    .filter((v) => v > rango.min && v < rango.max)
    .filter((v) => precioMinimo.value === null || v > precioMinimo.value)
    .map((v) => ({
      tipo: 'hasta',
      valor: v,
      etiqueta: `Hasta ${formatearPrecio(v)}`,
      activo: precioMaximo.value === v,
    }))

  return [...desde, ...hasta]
})

function usarAtajo(atajo) {
  if (atajo.tipo === 'desde') {
    precioMinimo.value = precioMinimo.value === atajo.valor ? null : atajo.valor
  } else {
    precioMaximo.value = precioMaximo.value === atajo.valor ? null : atajo.valor
  }
}

// Lo que se lee en la cabecera de la sección cuando está plegada.
const presupuestoElegido = computed(() => {
  const desde = precioMinimo.value !== null ? `desde ${formatearPrecio(precioMinimo.value)}` : ''
  const hasta = precioMaximo.value !== null ? `hasta ${formatearPrecio(precioMaximo.value)}` : ''

  return [desde, hasta].filter(Boolean).join(' · ')
})

const marcaElegida = computed(() => {
  if (marcas.value.length === 0) return ''
  return marcas.value.length === 1 ? marcas.value[0] : `${marcas.value.length} elegidas`
})

// Las tallas por sistema (ropa, calzado, CL, US), cada grupo con un tope que
// se abre con "+N". Mezcladas eran una sola pared de pastillas.
const TOPE_TALLAS = 10
const gruposAbiertos = reactive({})
const gruposTalla = computed(() => GRUPOS_TALLA
  .map((g) => {
    const todas = tallasDisponibles.value.filter((t) => t.grupo === g.id)
    const abierto = gruposAbiertos[g.id] || todas.length <= TOPE_TALLAS + 2
    // La talla elegida se ve aunque el grupo esté recortado.
    const visibles = abierto
      ? todas
      : todas.filter((t, i) => i < TOPE_TALLAS || t.nombre === talla.value)

    return { ...g, visibles, ocultas: todas.length - visibles.length }
  })
  .filter((g) => g.visibles.length > 0))

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
    <SeccionFiltro
      v-if="conteoPorCategoria.length > 1"
      v-model:abierta="abiertos.categoria"
      titulo="Categoría"
      :elegido="categoria"
    >

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
          <span class="truncar">{{ c.nombre }}</span>
          <span class="pildora__cuenta">{{ c.total }}</span>
        </button>
      </div>
    </SeccionFiltro>

    <!-- ——— marca ——— -->
    <SeccionFiltro
      v-if="marcasDisponibles.length > 1"
      v-model:abierta="abiertos.marca"
      titulo="Marca"
      :elegido="marcaElegida"
    >
      <input v-model="buscarMarca" type="search" class="buscar-marca" aria-label="Buscar marca" placeholder="Buscar marca…" />

      <div class="grupo__opciones" aria-label="Marcas disponibles">
        <button
          v-for="m in marcasVisibles.slice(0, limiteMarcas)"
          :key="m.nombre"
          type="button"
          class="pildora"
          :class="{ 'pildora--activa': marcas.includes(m.nombre) }"
          :aria-pressed="marcas.includes(m.nombre)"
          @click="store.alternarMarca(m.nombre)"
        >
          <span class="truncar">{{ m.nombre }}</span>
          <span class="pildora__cuenta">{{ m.total }}</span>
        </button>
      </div>
      <BaseButton v-if="marcasVisibles.length > limiteMarcas" variante="texto" tamano="chico" @click="limiteMarcas += 20">Ver más marcas</BaseButton>
    </SeccionFiltro>

    <!-- ——— talla ——— -->
    <SeccionFiltro
      v-if="tallasDisponibles.length > 1"
      v-model:abierta="abiertos.talla"
      titulo="Talla"
      :elegido="talla"
    >

      <div v-for="g in gruposTalla" :key="g.id" class="subgrupo">
        <p v-if="gruposTalla.length > 1" class="subgrupo__titulo">{{ g.nombre }}</p>

        <div class="grupo__opciones">
          <button
            v-for="t in g.visibles"
            :key="t.nombre"
            type="button"
            class="pildora pildora--talla"
            :class="{ 'pildora--activa': talla === t.nombre }"
            :aria-pressed="talla === t.nombre"
            @click="talla = talla === t.nombre ? '' : t.nombre"
          >
            <span class="truncar">{{ t.nombre }}</span>
            <span class="pildora__cuenta">{{ t.total }}</span>
          </button>

          <button
            v-if="g.ocultas > 0"
            type="button"
            class="pildora pildora--mas"
            :aria-label="`Ver ${g.ocultas} tallas más de ${g.nombre}`"
            @click="gruposAbiertos[g.id] = true"
          >
            +{{ g.ocultas }}
          </button>
        </div>
      </div>

      <p class="grupo__nota mono">
        Cuenta las tiendas con stock: una talla puede estar en una y no en otra.
      </p>
    </SeccionFiltro>

    <!-- ——— atributos ———
         Cuáles salen depende de lo que se esté mirando: al filtrar zapatillas
         aparecen Suela y Terreno; con ropa, Corte y Cuello. Se derivan del
         catálogo, no de una lista escrita a mano por tipo de prenda. -->
    <SeccionFiltro
      v-for="atributo in atributosDisponibles"
      :key="atributo.clave"
      :abierta="abiertos[atributo.clave] ?? atributos[atributo.clave] !== undefined"
      @update:abierta="abiertos[atributo.clave] = $event"
      :titulo="atributo.clave"
      :elegido="atributos[atributo.clave] ?? ''"
    >

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
          <span class="truncar">{{ v.valor }}</span>
          <span class="pildora__cuenta">{{ v.total }}</span>
        </button>
      </div>
    </SeccionFiltro>

    <!-- ——— presupuesto ——— -->
    <SeccionFiltro
      v-if="rangoPrecios"
      v-model:abierta="abiertos.presupuesto"
      titulo="Presupuesto"
      :elegido="presupuestoElegido"
    >

      <p class="rango__valor mono">
        <span>{{ precioMinimo !== null ? formatearPrecio(precioMinimo) : 'Sin mínimo' }}</span>
        <span class="rango__guion" aria-hidden="true">–</span>
        <span>{{ precioMaximo !== null ? formatearPrecio(precioMaximo) : 'Sin tope' }}</span>
      </p>

      <!-- Dos barras, no una: con un solo control "hasta" no había forma de
           pedir "prendas de más de $10.000" sin dejar el techo a mano. -->
      <label class="rango__campo">
        <span class="mono rango__etiqueta">Desde</span>
        <input
          v-model="piso"
          type="range"
          class="rango__control"
          :min="rangoPrecios.min"
          :max="rangoPrecios.max"
          :step="paso"
          aria-label="Precio mínimo"
        />
      </label>

      <label class="rango__campo">
        <span class="mono rango__etiqueta">Hasta</span>
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

      <div v-if="atajosPrecio.length > 0" class="atajos">
        <button
          v-for="atajo in atajosPrecio"
          :key="`${atajo.tipo}-${atajo.valor}`"
          type="button"
          class="pildora pildora--atajo"
          :class="{ 'pildora--activa': atajo.activo }"
          :aria-pressed="atajo.activo"
          @click="usarAtajo(atajo)"
        >
          {{ atajo.etiqueta }}
        </button>
      </div>
    </SeccionFiltro>

    <!-- ——— tiendas: sólo si hay más de una que comparar ——— -->
    <FiltroTiendas
      v-if="tiendas.length > 1"
      v-model="tiendasActivas"
      :tiendas="tiendas"
    />

    <BaseButton
      v-if="hayFiltros"
      class="panel__limpiar"
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
.buscar-marca { width: 100%; min-height: 36px; margin-bottom: .75rem; padding: .4rem .75rem;
  border: 1px solid var(--cep-line-media); border-radius: var(--cep-r-sm);
  border-radius: var(--cep-r-pill);
  background: var(--cep-surface); color: var(--cep-ink); }
.panel {
  display: flex;
  flex-direction: column;
}
.panel__limpiar {
  margin-top: var(--cep-sp-4);
}

.subgrupo + .subgrupo {
  margin-top: var(--cep-sp-3);
}
.subgrupo__titulo {
  margin: 0 0 var(--cep-sp-15);
  font-size: var(--cep-fs-xs);
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
  /* Los nombres de marca los ponen las tiendas. Sin este tope, uno largo hacía
     la pastilla más ancha que el panel y se salía por el lado derecho. */
  max-width: 100%;
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
  /* La cuenta no se recorta nunca: es el dato, el nombre es la etiqueta. */
  flex: none;
}

.pildora--talla {
  min-width: 52px;
  justify-content: center;
  font-family: var(--cep-font-mono);
}
.pildora--mas {
  justify-content: center;
  min-width: 44px;
  border-style: dashed;
  color: var(--cep-muted);
  font-family: var(--cep-font-mono);
}
.grupo__nota {
  margin: var(--cep-sp-2) 0 0;
  font-size: var(--cep-fs-xs);
  line-height: var(--cep-lh-snug);
  color: var(--cep-muted);
}

.rango__valor {
  display: flex;
  align-items: baseline;
  gap: var(--cep-sp-2);
  margin: 0 0 var(--cep-sp-2);
  font-size: var(--cep-fs-sm);
}
.rango__guion {
  color: var(--cep-muted);
}
.rango__campo {
  display: grid;
  grid-template-columns: 3.25rem minmax(0, 1fr);
  align-items: center;
  gap: var(--cep-sp-2);
  margin-top: var(--cep-sp-1);
}
.rango__etiqueta {
  font-size: var(--cep-fs-2xs);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--cep-muted);
}
/* Las dos barras usan el mismo lenguaje que las pastillas: pista fina y pulgar
   redondo de tinta, en vez del control azul del sistema operativo. */
.rango__control {
  width: 100%;
  height: 24px;
  margin: 0;
  appearance: none;
  background: transparent;
  cursor: pointer;
  accent-color: var(--cep-accent);
}
.rango__control::-webkit-slider-runnable-track {
  height: 4px;
  border-radius: var(--cep-r-pill);
  background: var(--cep-line-media);
}
.rango__control::-moz-range-track {
  height: 4px;
  border-radius: var(--cep-r-pill);
  background: var(--cep-line-media);
}
.rango__control::-webkit-slider-thumb {
  appearance: none;
  width: 16px;
  height: 16px;
  margin-top: -6px;
  border: 3px solid var(--cep-surface);
  border-radius: 50%;
  background: var(--cep-ink);
  box-shadow: 0 0 0 1px var(--cep-line-fuerte);
}
.rango__control::-moz-range-thumb {
  width: 16px;
  height: 16px;
  border: 3px solid var(--cep-surface);
  border-radius: 50%;
  background: var(--cep-ink);
  box-shadow: 0 0 0 1px var(--cep-line-fuerte);
}
.rango__control:focus-visible {
  outline: 2px solid var(--cep-accent);
  outline-offset: 4px;
  border-radius: var(--cep-r-pill);
}
.atajos {
  display: flex;
  flex-wrap: wrap;
  gap: var(--cep-sp-15);
  margin-top: var(--cep-sp-3);
}
.pildora--atajo {
  min-height: 30px;
  padding: 0 var(--cep-sp-2);
  font-family: var(--cep-font-mono);
  font-size: var(--cep-fs-2xs);
}
</style>
