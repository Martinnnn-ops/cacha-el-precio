<script setup>
import { computed, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import RanuraParte from '@/modules/outfits/components/RanuraParte.vue'
import ResumenOutfit from '@/modules/outfits/components/ResumenOutfit.vue'
import SelectorPrenda from '@/modules/outfits/components/SelectorPrenda.vue'
import { PARTES } from '@/modules/outfits/data/partes'
import { useOutfitStore } from '@/modules/outfits/store/outfit.store'
import AdSlot from '@/shared/components/AdSlot.vue'
import AvisoDatos from '@/shared/components/AvisoDatos.vue'
import BaseSkeleton from '@/shared/components/BaseSkeleton.vue'

// Rectángulo bajo el resumen: la columna del outfit es donde más rato se
// mira, y ahí un anuncio no se cruza con lo que se está haciendo.
const BLOQUE_FICHA = import.meta.env.VITE_ADSENSE_SLOT_FICHA ?? ''

const catalogo = useComparadorStore()
const outfit = useOutfitStore()

const { cargando, error, actualizadoEn, datosDesactualizados } =
  storeToRefs(catalogo)
const { prendas, partesBloqueadas, seleccion } = storeToRefs(outfit)

// Qué ranura se está editando. null = ninguna.
const editando = ref(null)

const parteEditando = computed(() =>
  PARTES.find((p) => p.id === editando.value) ?? null,
)

const opcionesEditando = computed(() =>
  editando.value ? outfit.opcionesPara(editando.value) : [],
)

function elegir(productoId) {
  outfit.ponerPrenda(editando.value, productoId)
  editando.value = null
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
      <h1 class="display armar__titulo">Arma tu outfit</h1>

      <p class="armar__bajada">
        Elige una prenda para cada parte y te decimos en qué tienda sale más
        barata cada una.
      </p>
    </header>

    <div class="armar__cuerpo">
      <div class="armar__panel">
        <!-- Elegir prenda sustituye a las ranuras en vez de abrirse encima:
             en móvil un panel flotante con una lista larga se maneja peor. -->
        <SelectorPrenda
          v-if="parteEditando"
          :parte="parteEditando"
          :opciones="opcionesEditando"
          :elegido="seleccion[parteEditando.id]"
          @elegir="elegir"
          @cerrar="editando = null"
        />

        <div v-else-if="cargando && catalogo.productos.length === 0" class="ranuras">
          <BaseSkeleton v-for="n in 4" :key="n" :alto="210" radio="var(--cep-r-xs)" />
        </div>

        <div v-else class="ranuras">
          <RanuraParte
            v-for="parte in PARTES"
            :key="parte.id"
            :parte="parte"
            :producto="prendas[parte.id]"
            :bloqueada="partesBloqueadas.includes(parte.id)"
            :opciones="outfit.opcionesPara(parte.id).length"
            @elegir="editando = parte.id"
            @quitar="outfit.quitarPrenda(parte.id)"
          />
        </div>
      </div>

      <aside class="armar__resumen">
        <ResumenOutfit />

        <AdSlot :bloque="BLOQUE_FICHA" :alto="200" />
      </aside>
    </div>
  </div>
</template>

<style scoped>
.armar__cabecera {
  max-width: 56ch;
  margin-bottom: var(--cep-sp-7);
}
.armar__titulo {
  margin: 0 0 var(--cep-sp-2);
  font-size: var(--cep-fs-2xl);
}
.armar__bajada {
  margin: 0;
  color: var(--cep-muted);
}

.armar__cuerpo {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--cep-sp-6);
  align-items: start;
}
@media (min-width: 900px) {
  .armar__cuerpo {
    grid-template-columns: 1.6fr 1fr;
  }
}

.armar__resumen {
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-4);
}

.ranuras {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--cep-sp-4);
}
@media (min-width: 560px) {
  .ranuras {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>
