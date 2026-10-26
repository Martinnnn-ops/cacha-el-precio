<script setup>
import { computed, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { RouterLink } from 'vue-router'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import { useCuentaStore } from '@/modules/cuenta/store/cuenta.store'
import PercheroPrendas from '@/modules/outfits/components/PercheroPrendas.vue'
import ResumenOutfit from '@/modules/outfits/components/ResumenOutfit.vue'
import TableroOutfit from '@/modules/outfits/components/TableroOutfit.vue'
import { useOutfitStore } from '@/modules/outfits/store/outfit.store'
import AdSlot from '@/shared/components/AdSlot.vue'
import AvisoDatos from '@/shared/components/AvisoDatos.vue'
import BaseSkeleton from '@/shared/components/BaseSkeleton.vue'
import { formatearPrecio } from '@/shared/utils/formato'

// El armador como un armario digital: a la izquierda el perchero con todas las
// prendas que se pueden poner, a la derecha el outfit puesto, ordenado de la
// cabeza a los pies, con el total y dónde conviene comprar. En el teléfono el
// perchero va primero y una barra fija abajo lleva al outfit.

// Rectángulo después del armador: queda visible al terminar el flujo sin
// interrumpir la elección de prendas ni competir con el resumen de compra.
const BLOQUE_FICHA = import.meta.env.VITE_ADSENSE_SLOT_FICHA ?? ''

const catalogo = useComparadorStore()
const outfit = useOutfitStore()
const cuenta = useCuentaStore()

const { cargando, error, actualizadoEn, datosDesactualizados } = storeToRefs(catalogo)
const { piezas, total } = storeToRefs(outfit)

// Parte que muestra el perchero; el tablero la cambia con «Elegir piernas».
const parte = ref('todo')
const perchero = ref(null)

function elegir(id) {
  parte.value = id
  perchero.value?.$el?.scrollIntoView?.({ behavior: 'smooth', block: 'start' })
}

const cuantas = computed(() => piezas.value.length)

function irAlOutfit() {
  document.getElementById('tu-outfit')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

onMounted(() => catalogo.cargarProductos())
</script>

<template>
  <div class="armar">
    <AvisoDatos
      :error="error"
      :desactualizado="datosDesactualizados"
      :actualizado-en="actualizadoEn"
      :reintentando="cargando"
      @reintentar="catalogo.cargarProductos({ forzar: true })"
    />

    <header class="armar__cabecera">
      <div>
        <p class="eyebrow">Probador de precios</p>
        <h1 class="display armar__titulo">Arma tu outfit</h1>
        <p class="armar__bajada">
          Elige prendas del perchero y ve cómo queda de la cabeza a los pies. Combina capas —polera, polerón y
          chaqueta— y te decimos dónde conviene comprar cada pieza.
        </p>
      </div>
      <nav class="secciones" aria-label="Outfits">
        <RouterLink to="/armar" class="secciones__item" aria-current="page">Armar</RouterLink>
        <RouterLink to="/outfits" class="secciones__item">Outfits listos</RouterLink>
        <RouterLink v-if="cuenta.autenticado" :to="{ name: 'mi-armario' }" class="secciones__item">Mi armario</RouterLink>
      </nav>
    </header>

    <div class="armar__cuerpo">
      <div v-if="cargando && catalogo.productos.length === 0" class="rejilla-carga">
        <BaseSkeleton v-for="n in 8" :key="n" :alto="240" radio="var(--cep-r-lg)" />
      </div>
      <PercheroPrendas v-else ref="perchero" v-model:parte="parte" />

      <aside id="tu-outfit" class="armar__outfit">
        <TableroOutfit @elegir="elegir" />
        <ResumenOutfit sin-lista />
      </aside>
    </div>

    <AdSlot :bloque="BLOQUE_FICHA" :alto="96" class="armar__anuncio" />

    <!-- Teléfono: el total siempre a la vista mientras se recorre el perchero. -->
    <button type="button" class="barra-outfit" @click="irAlOutfit">
      <span>
        <strong>Tu outfit</strong>
        <span class="mono muted">· {{ cuantas }} {{ cuantas === 1 ? 'prenda' : 'prendas' }}</span>
      </span>
      <span class="mono barra-outfit__total">{{ formatearPrecio(total) }} ↓</span>
    </button>
  </div>
</template>

<style scoped>
.armar__cabecera {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--cep-sp-4);
  margin-bottom: var(--cep-sp-6);
}
.armar__cabecera > div { max-width: 60ch; }
.armar__titulo { margin: 0 0 var(--cep-sp-2); font-size: var(--cep-fs-2xl); }
.armar__bajada { margin: 0; color: var(--cep-muted); }

.secciones { display: flex; gap: var(--cep-sp-1); border-bottom: 1px solid var(--cep-line-media); }
.secciones__item {
  padding: var(--cep-sp-2) var(--cep-sp-3); margin-bottom: -1px;
  font-size: var(--cep-fs-sm); color: var(--cep-muted); text-decoration: none;
  border-bottom: 2px solid transparent;
}
.secciones__item:hover { color: var(--cep-ink); }
.secciones__item[aria-current='page'] { color: var(--cep-ink); font-weight: 600; border-bottom-color: var(--cep-ink); }

.armar__cuerpo {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--cep-sp-6);
  align-items: start;
}
@media (min-width: 900px) {
  .armar__cuerpo { grid-template-columns: minmax(0, 1.7fr) minmax(0, 1fr); }
  /* El outfit acompaña mientras se recorre el perchero. */
  .armar__outfit { position: sticky; top: var(--cep-sp-4); max-height: calc(100vh - var(--cep-sp-8)); overflow-y: auto; }
}
.armar__outfit { display: flex; flex-direction: column; gap: var(--cep-sp-4); scroll-margin-top: var(--cep-sp-4); }
/* El resumen va bajo el tablero, no pegado aparte. */
.armar__outfit :deep(.resumen) { position: static; border-top: 0; }

.rejilla-carga { display: grid; gap: var(--cep-sp-3); grid-template-columns: repeat(2, minmax(0, 1fr)); }
@media (min-width: 620px) { .rejilla-carga { grid-template-columns: repeat(3, minmax(0, 1fr)); } }

.armar__anuncio { margin-top: var(--cep-sp-10); }

.barra-outfit {
  position: sticky; bottom: var(--cep-sp-3); z-index: 5;
  display: flex; align-items: center; justify-content: space-between; gap: var(--cep-sp-3);
  width: 100%; margin-top: var(--cep-sp-6); padding: var(--cep-sp-3) var(--cep-sp-4);
  font: inherit; color: var(--cep-surface); background: var(--cep-ink);
  border: 0; border-radius: var(--cep-r-lg); cursor: pointer; text-align: left;
  box-shadow: 0 6px 20px rgb(0 0 0 / 18%);
}
.barra-outfit .muted { color: color-mix(in srgb, var(--cep-surface) 70%, transparent); }
.barra-outfit__total { font-weight: 700; }
@media (min-width: 900px) { .barra-outfit { display: none; } }
</style>
