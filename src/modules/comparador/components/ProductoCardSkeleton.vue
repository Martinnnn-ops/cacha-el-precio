<script setup>
import BaseSkeleton from '@/shared/components/BaseSkeleton.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'

// Silueta de ProductoCard. Copia su estructura pieza por pieza —ilustración,
// marca, título, precio, divisor, filas de oferta— para que al llegar los datos
// el contenido ocupe el mismo sitio y la página no dé un salto.
//
// El ancho en modo compacto (260px) tiene que coincidir con el de
// ProductoCard.vue: si difiere, el carrusel se recoloca cuando cargan los
// productos. Hay una comprobación en npm run humo que vigila justo eso.
defineProps({
  compacta: { type: Boolean, default: false },
  // Cuántas filas de tienda se insinúan. Tres es lo habitual en el catálogo.
  ofertas: { type: Number, default: 3 },
})
</script>

<template>
  <BaseTicket
    class="silueta"
    :class="{ 'silueta--compacta': compacta }"
    :relleno="false"
    columna
  >
    <div class="silueta__cabecera">
      <BaseSkeleton :alto="compacta ? 108 : 140" radio="var(--cep-r-sm)" />

      <BaseSkeleton :alto="10" ancho="45%" class="silueta__marca" />
      <BaseSkeleton :alto="15" ancho="85%" class="silueta__titulo" />
      <BaseSkeleton :alto="22" ancho="55%" class="silueta__precio" />
    </div>

    <div class="divisor silueta__divisor" />

    <ul class="silueta__ofertas">
      <li v-for="n in ofertas" :key="n" class="silueta__oferta">
        <BaseSkeleton :alto="8" circulo />
        <BaseSkeleton :alto="11" ancho="40%" />
        <BaseSkeleton :alto="11" ancho="28%" class="silueta__monto" />
      </li>
    </ul>

    <div class="silueta__pie">
      <BaseSkeleton :alto="9" ancho="60%" />
    </div>
  </BaseTicket>
</template>

<style scoped>
/* Las medidas son las mismas que las de ProductoCard: si aquí se cambia una,
   allí también hay que cambiarla o vuelve el salto de maquetación. */
/* La columna la pone `columna` de BaseTicket, igual que en ProductoCard. */
.silueta--compacta {
  width: 260px;
}

.silueta__cabecera {
  padding: var(--cep-sp-4) var(--cep-sp-4) 0;
}
.silueta__marca {
  margin-top: var(--cep-sp-3);
}
.silueta__titulo {
  margin-top: var(--cep-sp-2);
}
.silueta__precio {
  margin-top: var(--cep-sp-3);
}

.silueta__divisor {
  margin: var(--cep-sp-3) var(--cep-sp-4);
}

.silueta__ofertas {
  list-style: none;
  margin: 0;
  padding: 0 var(--cep-sp-4);
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-05);
}
.silueta__oferta {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-2);
  padding: var(--cep-sp-15) var(--cep-sp-2);
}
.silueta__monto {
  margin-left: auto;
}

.silueta__pie {
  padding: var(--cep-sp-25) var(--cep-sp-4) var(--cep-sp-4);
  margin-top: var(--cep-sp-3);
}
</style>
