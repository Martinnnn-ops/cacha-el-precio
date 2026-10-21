<script setup>
import { computed, ref, watch } from 'vue'

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
  talla: { type: String, default: '' },
})

defineEmits(['elegir', 'cerrar'])

const { nombreTienda } = useTiendas()
const busqueda = ref('')
const orden = ref('precio')
const limite = ref(24)
watch(() => [busqueda.value, orden.value, props.parte.id, props.talla], () => { limite.value = 24 })

function tallasDe(producto) {
  return [...new Set(
    producto.precios
      .filter((oferta) => oferta.stock)
      .flatMap((oferta) => oferta.tallas ?? []),
  )]
}

const listadas = computed(() => {
  const texto = busqueda.value.trim().toLowerCase()

  return props.opciones
    .filter(
      (p) =>
        texto === '' ||
        `${p.marca} ${p.nombre} ${p.categoria}`.toLowerCase().includes(texto),
    )
    .map((producto) => ({ producto, oferta: precioMasBajo(producto, props.talla) }))
    .filter(({ oferta }) => oferta !== null)
    .sort((a, b) => {
      if (orden.value === 'nombre') {
        return a.producto.nombre.localeCompare(b.producto.nombre, 'es')
      }

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

    <div class="selector__filtros">
      <BaseSearchInput
        v-model="busqueda"
        :id="`buscar-${parte.id}`"
        :etiqueta="`Buscar en ${parte.nombre.toLowerCase()}`"
        placeholder="Buscar por nombre o marca…"
      />

      <label class="selector__orden">
        <span class="mono selector__orden-etiqueta">Ordenar</span>
        <select v-model="orden" class="selector__orden-control">
          <option value="precio">Menor precio</option>
          <option value="nombre">Nombre</option>
        </select>
      </label>
    </div>

    <ul class="selector__lista">
      <li v-for="{ producto, oferta } in listadas.slice(0, limite)" :key="producto.id">
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
            <span v-if="talla" class="mono opcion__talla">Talla {{ talla }} disponible</span>
            <span v-else-if="tallasDe(producto).length" class="mono muted opcion__tallas">
              Tallas {{ tallasDe(producto).slice(0, 5).join(' · ') }}
            </span>
          </span>

          <span v-if="oferta" class="mono opcion__precio">
            {{ formatearPrecio(oferta.precio) }}
            <small class="muted">{{ nombreTienda(oferta.tienda) }}</small>
          </span>

          <span v-else class="mono opcion__agotada">Sin stock</span>
        </button>
      </li>
    </ul>
    <BaseButton v-if="listadas.length > limite" @click="limite += 24">Ver más prendas</BaseButton>

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

.selector__filtros {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--cep-sp-3);
}
@media (min-width: 620px) {
  .selector__filtros {
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: end;
  }
}
.selector__orden {
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-1);
}
.selector__orden-etiqueta {
  font-size: var(--cep-fs-2xs);
  color: var(--cep-muted);
  letter-spacing: 0.05em;
  text-transform: uppercase;
}
.selector__orden-control {
  min-height: var(--cep-control-h);
  padding: 0 var(--cep-sp-3);
  background: var(--cep-surface);
  color: var(--cep-ink);
  border: 1px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-md);
  cursor: pointer;
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
.opcion__talla,
.opcion__tallas {
  margin-top: var(--cep-sp-05);
  font-size: var(--cep-fs-2xs);
}
.opcion__talla {
  color: var(--cep-exito);
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
