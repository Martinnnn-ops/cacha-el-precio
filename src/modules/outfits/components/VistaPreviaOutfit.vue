<script setup>
import { computed } from 'vue'
import { storeToRefs } from 'pinia'

import { PARTES } from '@/modules/outfits/data/partes'
import { useOutfitStore } from '@/modules/outfits/store/outfit.store'
import PrendaArt from '@/shared/components/PrendaArt.vue'

// Tira de capas del outfit mientras se arma. El resumen de compra ya dice
// cuánto sale, pero para eso hay que leer ocho tickets; acá se ve de un golpe
// qué está puesto y qué falta, en el orden del cuerpo.
const outfit = useOutfitStore()
const { prendas, partesActivas, partesListas } = storeToRefs(outfit)

// Una prenda de cuerpo completo ocupa dos capas: se muestra una sola vez.
const capas = computed(() => {
  const vistas = new Set()

  return PARTES
    .filter((parte) => partesActivas.value.includes(parte.id))
    .map((parte) => ({ parte, producto: prendas.value[parte.id] ?? null }))
    .filter(({ producto }) => {
      if (producto && vistas.has(producto.id)) return false
      if (producto) vistas.add(producto.id)

      return true
    })
})
</script>

<template>
  <section v-if="partesActivas.length > 0" class="vista-previa" aria-label="Vista previa del outfit">
    <div class="vista-previa__cabecera">
      <p class="eyebrow vista-previa__titulo">Así va quedando</p>
      <p class="mono vista-previa__cuenta">
        {{ partesListas }} de {{ partesActivas.length }} capas
      </p>
    </div>

    <ul class="vista-previa__tira">
      <li
        v-for="{ parte, producto } in capas"
        :key="parte.id"
        class="capa"
        :class="{ 'capa--llena': producto }"
        :title="producto ? `${parte.nombre}: ${producto.nombre}` : `${parte.nombre}: sin elegir`"
      >
        <span class="capa__marco">
          <img
            v-if="producto?.imagen"
            :src="producto.imagen"
            :alt="producto.nombre"
            loading="lazy"
            decoding="async"
            class="capa__foto"
          />
          <PrendaArt v-else-if="producto" :alto="48" />
          <span v-else class="capa__hueco" aria-hidden="true">+</span>
        </span>

        <span class="mono capa__nombre">{{ parte.nombre }}</span>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.vista-previa {
  margin-bottom: var(--cep-sp-4);
}
.vista-previa__cabecera {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--cep-sp-3);
  margin-bottom: var(--cep-sp-2);
}
.vista-previa__titulo {
  margin: 0;
}
.vista-previa__cuenta {
  margin: 0;
  font-size: var(--cep-fs-2xs);
  color: var(--cep-muted);
}

/* Tira horizontal: en el teléfono se desliza y en escritorio caben las ocho
   capas. Las vacías quedan con borde discontinuo, como las ranuras. */
.vista-previa__tira {
  display: flex;
  gap: var(--cep-sp-2);
  margin: 0;
  padding: 2px 2px var(--cep-sp-2);
  list-style: none;
  overflow-x: auto;
  scrollbar-width: thin;
  scroll-snap-type: x proximity;
}
.capa {
  display: flex;
  flex: 0 0 78px;
  flex-direction: column;
  align-items: center;
  gap: var(--cep-sp-1);
  scroll-snap-align: start;
}
.capa__marco {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 78px;
  height: 72px;
  overflow: hidden;
  background: var(--cep-bg);
  border: 1px dashed var(--cep-line-fuerte);
  border-radius: var(--cep-r-md);
}
.capa--llena .capa__marco {
  border-style: solid;
  border-color: var(--cep-line-media);
}
.capa__foto {
  width: 100%;
  height: 100%;
  object-fit: contain;
  background: var(--cep-surface);
}
.capa__hueco {
  font-family: var(--cep-font-mono);
  font-size: var(--cep-fs-lg);
  color: var(--cep-muted);
}
.capa__nombre {
  font-size: var(--cep-fs-2xs);
  line-height: var(--cep-lh-snug);
  text-align: center;
  color: var(--cep-muted);
}
.capa--llena .capa__nombre {
  color: var(--cep-ink);
}
</style>
