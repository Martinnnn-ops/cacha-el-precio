<script setup>
import BaseSkeleton from '@/shared/components/BaseSkeleton.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'

// Silueta de la ficha de producto. Reproduce la rejilla `detalle__portada` y
// la tabla de precios de ProductoDetailView para que el contenido real caiga
// en el mismo sitio.
</script>

<template>
  <div class="silueta">
    <div class="silueta__portada">
      <BaseTicket :relleno="false">
        <BaseSkeleton :alto="240" radio="var(--cep-r-xs)" />
      </BaseTicket>

      <div>
        <BaseSkeleton :alto="10" ancho="30%" />
        <BaseSkeleton :alto="26" ancho="80%" class="silueta__titulo" />
        <BaseSkeleton :alto="13" ancho="95%" class="silueta__linea" />
        <BaseSkeleton :alto="13" ancho="60%" class="silueta__linea" />
        <BaseSkeleton :alto="24" ancho="45%" class="silueta__precio" />
      </div>
    </div>

    <BaseTicket class="silueta__panel">
      <BaseSkeleton :alto="10" ancho="25%" />

      <div v-for="n in 3" :key="n" class="silueta__fila">
        <BaseSkeleton :alto="9" circulo />
        <BaseSkeleton :alto="12" ancho="35%" />
        <BaseSkeleton :alto="12" ancho="20%" class="silueta__monto" />
      </div>
    </BaseTicket>
  </div>
</template>

<style scoped>
.silueta__portada {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--cep-sp-6);
  align-items: center;
  margin: var(--cep-sp-5) 0 var(--cep-sp-7);
}
@media (min-width: 720px) {
  .silueta__portada {
    grid-template-columns: minmax(0, 0.8fr) minmax(0, 1.2fr);
  }
}
.silueta__titulo {
  margin-top: var(--cep-sp-2);
}
.silueta__linea {
  margin-top: var(--cep-sp-2);
}
.silueta__precio {
  margin-top: var(--cep-sp-4);
}

.silueta__panel {
  margin-bottom: var(--cep-sp-6);
}
.silueta__fila {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-2);
  padding: var(--cep-sp-25) 0;
  border-top: 1px solid var(--cep-line);
  margin-top: var(--cep-sp-3);
}
.silueta__monto {
  margin-left: auto;
}
</style>
