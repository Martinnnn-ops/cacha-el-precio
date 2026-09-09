<script setup>
import { computed, ref } from 'vue'

import { useTiendas } from '@/modules/comparador/composables/useTiendas'
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseSearchInput from '@/shared/components/BaseSearchInput.vue'
import PrendaArt from '@/shared/components/PrendaArt.vue'
import { formatearPrecio } from '@/shared/utils/formato'
import { precioMasBajo } from '@/shared/utils/precios'

// Panel para elegir la prenda de una ranura. Las opciones llegan ya filtradas
// por parte desde el store; aquí sólo se buscan y se ordenan.
const props = defineProps({
  parte: { type: Object, required: true },
  opciones: { type: Array, default: () => [] },
  elegido: { type: String, default: null },
})

defineEmits(['elegir', 'cerrar'])

const { nombreTienda } = useTiendas()
const busqueda = ref('')

const listadas = computed(() => {
  const texto = busqueda.value.trim().toLowerCase()

  return props.opciones
    .filter(
      (p) =>
        texto === '' ||
        `${p.marca} ${p.nombre} ${p.categoria}`.toLowerCase().includes(texto),
    )
    .map((producto) => ({ producto, oferta: precioMasBajo(producto) }))
    .sort((a, b) => {
      // Las que no se pueden comprar, al final.
      if (!a.oferta) return 1
      if (!b.oferta) return -1

      return a.oferta.precio - b.oferta.precio
    })
})
</script>

<template>
  <section class="selector" aria-labelledby="selector-titulo">
    <header class="selector__cabecera">
      <div>
        <h2 id="selector-titulo" class="display selector__titulo">
          Elegir {{ parte.nombre.toLowerCase() }}
        </h2>
        <p class="mono muted selector__conteo">
          {{ listadas.length }}
          {{ listadas.length === 1 ? 'prenda' : 'prendas' }} · de más barata a
          más cara
        </p>
      </div>

      <BaseButton variante="texto" tamano="chico" @click="$emit('cerrar')">
        Cerrar
      </BaseButton>
    </header>

    <BaseSearchInput
      v-model="busqueda"
      :id="`buscar-${parte.id}`"
      :etiqueta="`Buscar en ${parte.nombre.toLowerCase()}`"
      placeholder="Buscar por nombre o marca…"
    />

    <ul class="selector__lista">
      <li v-for="{ producto, oferta } in listadas" :key="producto.id">
        <button
          type="button"
          class="opcion"
          :class="{ 'opcion--elegida': producto.id === elegido }"
          :aria-pressed="producto.id === elegido"
          @click="$emit('elegir', producto.id)"
        >
          <img
            v-if="producto.imagen"
            :src="producto.imagen"
            :alt="producto.nombre"
            loading="lazy"
            class="opcion__foto"
          />
          <PrendaArt v-else :alto="52" />

          <span class="opcion__datos">
            <span class="opcion__nombre">{{ producto.nombre }}</span>
            <span class="mono muted opcion__meta">{{ producto.categoria }}</span>
          </span>

          <span v-if="oferta" class="mono opcion__precio">
            {{ formatearPrecio(oferta.precio) }}
            <small class="muted">{{ nombreTienda(oferta.tienda) }}</small>
          </span>

          <span v-else class="mono opcion__agotada">Sin stock</span>
        </button>
      </li>
    </ul>

    <p v-if="listadas.length === 0" class="muted selector__vacio">
      No encontramos prendas con esa búsqueda.
    </p>
  </section>
</template>

<style scoped>
.selector {
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-4);
}
.selector__cabecera {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--cep-sp-3);
}
.selector__titulo {
  margin: 0;
  font-size: var(--cep-fs-xl);
}
.selector__conteo {
  margin: var(--cep-sp-1) 0 0;
  font-size: var(--cep-fs-xs);
}

.selector__lista {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-2);
  max-height: 420px;
  overflow-y: auto;
}

.opcion {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-3);
  width: 100%;
  padding: var(--cep-sp-2);
  background: var(--cep-surface);
  border: 1px solid var(--cep-line);
  border-radius: var(--cep-r-lg);
  text-align: left;
  cursor: pointer;
  transition:
    border-color var(--cep-dur-1) var(--cep-ease),
    background var(--cep-dur-1) var(--cep-ease);
}
.opcion:hover {
  border-color: var(--cep-accent);
}
.opcion--elegida {
  border-color: var(--cep-exito);
  background: color-mix(in srgb, var(--cep-exito) 10%, transparent);
}
.opcion__foto {
  width: 48px;
  height: 48px;
  object-fit: cover;
  border-radius: var(--cep-r-md);
  flex-shrink: 0;
  background: var(--cep-suave);
}
.opcion__datos {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.opcion__nombre {
  font-size: var(--cep-fs-sm);
  font-weight: 600;
}
.opcion__meta {
  font-size: var(--cep-fs-xs);
}
.opcion__precio {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  flex: none;
  font-size: var(--cep-fs-sm);
  font-weight: 600;
  color: var(--cep-exito);
}
.opcion__precio small {
  font-weight: 400;
  font-size: var(--cep-fs-2xs);
}
.opcion__agotada {
  flex: none;
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}
.selector__vacio {
  margin: 0;
  padding: var(--cep-sp-6) 0;
  text-align: center;
  font-size: var(--cep-fs-sm);
}
</style>
