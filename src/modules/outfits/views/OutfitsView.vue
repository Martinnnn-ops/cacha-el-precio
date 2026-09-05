<script setup>
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import OutfitCard from '@/modules/outfits/components/OutfitCard.vue'
import { AUTOMATICAS, FIJAS } from '@/modules/outfits/data/plantillas'
import { useOutfitStore } from '@/modules/outfits/store/outfit.store'
import AdSlot from '@/shared/components/AdSlot.vue'
import AvisoDatos from '@/shared/components/AvisoDatos.vue'
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseSkeleton from '@/shared/components/BaseSkeleton.vue'

const router = useRouter()
const catalogo = useComparadorStore()
const outfit = useOutfitStore()

const { cargando, error, actualizadoEn, datosDesactualizados, productos } =
  storeToRefs(catalogo)

const BLOQUE_LISTADO = import.meta.env.VITE_ADSENSE_SLOT_LISTADO ?? ''

function usar(prendas) {
  outfit.ponerOutfit(prendas)
  router.push({ name: 'armar' })
}

onMounted(() => catalogo.cargarProductos())
</script>

<template>
  <div class="galeria">
    <AvisoDatos
      :error="error"
      :desactualizado="datosDesactualizados"
      :actualizado-en="actualizadoEn"
      :reintentando="cargando"
      @reintentar="catalogo.cargarProductos({ forzar: true })"
    />

    <header class="galeria__cabecera">
      <h1 class="display galeria__titulo">Outfits listos</h1>

      <p class="galeria__bajada">
        Conjuntos ya armados con las prendas más baratas de cada tipo. Úsalos
        tal cual o cámbiales lo que quieras.
      </p>

      <BaseButton variante="secundario" :to="{ name: 'armar' }">
        Prefiero armarlo yo
      </BaseButton>
    </header>

    <div v-if="cargando && productos.length === 0" class="rejilla">
      <BaseSkeleton v-for="n in 4" :key="n" :alto="300" radio="var(--cep-r-xs)" />
    </div>

    <template v-else>
      <section class="seccion">
        <h2 class="seccion-titulo">Se arman solos</h2>
        <p class="mono muted seccion__nota">
          Cogen la prenda más barata de cada tipo, así que cambian cuando
          cambian los precios.
        </p>

        <div class="rejilla">
          <OutfitCard
            v-for="plantilla in AUTOMATICAS"
            :key="plantilla.id"
            :plantilla="plantilla"
            @usar="usar"
          />
        </div>
      </section>

      <AdSlot :bloque="BLOQUE_LISTADO" :alto="110" />

      <section v-if="FIJAS.length > 0" class="seccion">
        <h2 class="seccion-titulo">Combinaciones elegidas</h2>

        <div class="rejilla">
          <OutfitCard
            v-for="plantilla in FIJAS"
            :key="plantilla.id"
            :plantilla="plantilla"
            @usar="usar"
          />
        </div>
      </section>
    </template>
  </div>
</template>

<style scoped>
.galeria {
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-8);
}
.galeria__cabecera {
  max-width: 56ch;
}
.galeria__titulo {
  margin: 0 0 var(--cep-sp-2);
  font-size: var(--cep-fs-2xl);
}
.galeria__bajada {
  margin: 0 0 var(--cep-sp-4);
  color: var(--cep-muted);
}
.seccion__nota {
  margin: 0 0 var(--cep-sp-4);
  font-size: var(--cep-fs-xs);
}
.rejilla {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--cep-sp-4);
}
@media (min-width: 620px) {
  .rejilla {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
@media (min-width: 1024px) {
  .rejilla {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
}
</style>
