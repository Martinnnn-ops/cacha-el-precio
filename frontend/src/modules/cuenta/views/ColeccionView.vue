<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useColeccionStore } from '@/modules/cuenta/store/coleccion.store'
import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import { useOutfitStore } from '@/modules/outfits/store/outfit.store'
import ProductoCard from '@/modules/comparador/components/ProductoCard.vue'
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'
const coleccion = useColeccionStore()
const catalogo = useComparadorStore()
const outfit = useOutfitStore()
const router = useRouter()
const limite = ref(24)
const productos = computed(() => catalogo.productos.filter(p => coleccion.deseados.includes(p.id)))
const ausentes = computed(() => coleccion.deseados.filter(id => !catalogo.productos.some(p => p.id === id)))
onMounted(() => Promise.all([coleccion.cargar(), catalogo.cargarProductos()]))
function abrir(datos) {
  outfit.tallaRopa = datos.tallaRopa ?? ''
  outfit.tallaCalzado = datos.tallaCalzado ?? ''
  outfit.ponerOutfit(Object.fromEntries(Object.entries(datos.seleccion).map(([k, v]) => [k, String(v)])))
  router.push({ name: 'armar' })
}
</script>

<template>
  <section>
    <p class="eyebrow">Tu selección personal</p>
    <h1 class="display">Mi armario</h1>
    <p class="muted">Tus deseados y outfits guardados en tu cuenta. Los precios y el stock se actualizan al abrirlos.</p>
    <p v-if="coleccion.error || catalogo.error" role="alert">{{ coleccion.error || catalogo.error }}</p>
    <BaseButton v-if="coleccion.error" @click="coleccion.cargar()">Reintentar</BaseButton>
    <p v-if="!coleccion.cargado && !coleccion.error" role="status">Cargando tu armario…</p>
    <h2>Lista de deseados</h2>
    <p v-if="coleccion.cargado && !coleccion.deseados.length" class="muted">Guarda prendas con el corazón del catálogo.</p>
    <div class="coleccion-grid">
      <ProductoCard v-for="producto in productos.slice(0, limite)" :key="producto.id" :producto="producto" />
    </div>
    <BaseButton v-if="productos.length > limite" @click="limite += 24">Ver más deseados</BaseButton>
    <div v-if="!catalogo.cargando && !catalogo.error">
      <p v-for="id in ausentes" :key="id">Producto ya no disponible
        <BaseButton :disabled="coleccion.ocupado" @click="coleccion.alternar(id)">Quitar</BaseButton>
      </p>
    </div>
    <h2>Outfits guardados</h2>
    <p v-if="coleccion.cargado && !coleccion.outfits.length" class="muted">Arma una combinación y guárdala con un nombre.</p>
    <div class="coleccion-grid">
      <BaseTicket v-for="guardado in coleccion.outfits" :key="guardado.id">
        <h3>{{ guardado.datos.nombre }}</h3>
        <p>{{ Object.keys(guardado.datos.seleccion).length }} capas · {{ guardado.datos.tallaRopa || 'Sin talla de ropa' }}</p>
        <BaseButton :disabled="catalogo.cargando || !!catalogo.error" @click="abrir(guardado.datos)">Usar outfit</BaseButton>
        <BaseButton variante="texto" :disabled="coleccion.ocupado" @click="coleccion.quitarOutfit(guardado.id)">Eliminar</BaseButton>
      </BaseTicket>
    </div>
  </section>
</template>

<style scoped>
.coleccion-grid { display: grid; grid-template-columns: minmax(0, 1fr); gap: 1.25rem; }
@media (min-width: 620px) { .coleccion-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (min-width: 1180px) { .coleccion-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
h2 { margin-top: 2.5rem; }
</style>
