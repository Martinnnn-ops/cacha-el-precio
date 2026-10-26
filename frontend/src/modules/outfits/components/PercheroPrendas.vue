<script setup>
import { computed, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'

import { useTiendas } from '@/modules/comparador/composables/useTiendas'
import { PARTES, nombreParte } from '@/modules/outfits/data/partes'
import { useOutfitStore } from '@/modules/outfits/store/outfit.store'
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseSearchInput from '@/shared/components/BaseSearchInput.vue'
import PrendaArt from '@/shared/components/PrendaArt.vue'
import { formatearPrecio } from '@/shared/utils/formato'

// El perchero: todas las prendas que se pueden poner en el outfit, en una
// rejilla como la de un armario digital. Se filtra por parte del cuerpo con
// pastillas, y un clic pone (o saca) la prenda en su lugar: el store ya sabe
// en qué parte va cada una. «Mi armario» es otra cosa: lo que la persona guardó.
const props = defineProps({
  // Parte elegida desde el tablero («Elegir piernas»): el perchero salta a ella.
  parte: { type: String, default: 'todo' },
})
const emit = defineEmits(['update:parte'])

const outfit = useOutfitStore()
const { genero, tallaRopa, tallaCalzado, tallasRopa, tallasCalzado, seleccion, partesBloqueadas } =
  storeToRefs(outfit)
const { nombreTienda } = useTiendas()

// Nombres cortos para las pastillas; el largo («Torso · capa base») queda en el tablero.
const CORTOS = {
  cabeza: 'Cabeza',
  'torso-base': 'Poleras y camisas',
  'torso-intermedia': 'Polerones',
  'torso-abrigo': 'Abrigos',
  interior: 'Ropa interior',
  piernas: 'Pantalones',
  calcetines: 'Calcetines',
  calzado: 'Calzado',
}
const GENEROS = ['', 'Mujer', 'Hombre', 'Unisex']

// Cada prenda con la parte donde va y su oferta más barata (en la talla elegida).
const porParte = computed(() =>
  Object.fromEntries(PARTES.map((p) => [p.id, outfit.opcionesPara(p.id)])))

const pastillas = computed(() => [
  { id: 'todo', nombre: 'Todo', total: Object.values(porParte.value).reduce((n, l) => n + l.length, 0) },
  ...PARTES.map((p) => ({ id: p.id, nombre: CORTOS[p.id], total: porParte.value[p.id].length }))
    .filter((p) => p.total > 0),
])
const sinPrendas = computed(() => PARTES.filter((p) => porParte.value[p.id].length === 0).map((p) => p.nombre))

const busqueda = ref('')
const orden = ref('precio')
const limite = ref(24)
watch(() => [props.parte, busqueda.value, orden.value, genero.value, tallaRopa.value, tallaCalzado.value],
  () => { limite.value = 24 })

const prendas = computed(() => {
  const texto = busqueda.value.trim().toLowerCase()
  const partes = props.parte === 'todo' ? PARTES.map((p) => p.id) : [props.parte]
  return partes
    .flatMap((parte) => porParte.value[parte].map((producto) => ({
      producto,
      parte,
      oferta: outfit.ofertaPara(producto, parte),
    })))
    .filter(({ producto }) =>
      !texto || `${producto.marca ?? ''} ${producto.nombre} ${producto.categoria}`.toLowerCase().includes(texto))
    .sort((a, b) =>
      orden.value === 'nombre'
        ? a.producto.nombre.localeCompare(b.producto.nombre, 'es')
        : a.oferta.precio - b.oferta.precio)
})

const puesta = (item) => seleccion.value[item.parte] === item.producto.id
const bloqueada = (item) => partesBloqueadas.value.includes(item.parte)

function alternar(item) {
  if (puesta(item)) outfit.quitarPrenda(item.parte)
  else outfit.ponerPrenda(item.parte, item.producto.id)
}
</script>

<template>
  <section class="perchero" aria-labelledby="perchero-titulo">
    <header class="perchero__cabecera">
      <div>
        <p class="eyebrow">Perchero</p>
        <h2 id="perchero-titulo" class="display perchero__titulo">Elige tus prendas</h2>
      </div>
      <p class="mono muted perchero__conteo">{{ prendas.length }} {{ prendas.length === 1 ? 'prenda' : 'prendas' }}</p>
    </header>

    <div class="pastillas" role="tablist" aria-label="Parte del cuerpo">
      <button v-for="p in pastillas" :key="p.id" type="button" role="tab" class="pastilla"
        :aria-selected="parte === p.id" @click="emit('update:parte', p.id)">
        {{ p.nombre }} <span class="pastilla__total">{{ p.total }}</span>
      </button>
    </div>

    <div class="filtros">
      <div class="filtros__buscar">
        <BaseSearchInput id="buscar-perchero" v-model="busqueda" etiqueta="Buscar en el perchero"
          placeholder="Buscar por nombre o marca…" />
        <label class="filtro">
          <span class="filtro__etiqueta">Orden</span>
          <select v-model="orden" class="filtro__control">
            <option value="precio">Menor precio</option>
            <option value="nombre">Nombre</option>
          </select>
        </label>
      </div>
      <div class="filtros__fila">
        <div class="segmentado" role="radiogroup" aria-label="Para quién">
          <button v-for="g in GENEROS" :key="g" type="button" role="radio" :aria-checked="genero === g"
            @click="genero = g">{{ g || 'Todos' }}</button>
        </div>
        <label class="filtro">
          <span class="filtro__etiqueta">Talla</span>
          <select v-model="tallaRopa" class="filtro__control">
            <option value="">Cualquiera</option>
            <option v-for="t in tallasRopa" :key="t" :value="t">{{ t }}</option>
          </select>
        </label>
        <label class="filtro">
          <span class="filtro__etiqueta">Calzado</span>
          <select v-model="tallaCalzado" class="filtro__control">
            <option value="">Cualquiera</option>
            <option v-for="t in tallasCalzado" :key="t" :value="t">{{ t }}</option>
          </select>
        </label>
      </div>
    </div>

    <ul v-if="prendas.length" class="rejilla">
      <li v-for="item in prendas.slice(0, limite)" :key="`${item.parte}-${item.producto.id}`">
        <button type="button" class="prenda" :class="{ 'prenda--puesta': puesta(item) }"
          :aria-pressed="puesta(item)" :disabled="bloqueada(item)"
          :title="bloqueada(item) ? 'Ya llevas una prenda de cuerpo completo' : ''" @click="alternar(item)">
          <span class="prenda__foto">
            <img v-if="item.producto.imagen" :src="item.producto.imagen" :alt="item.producto.nombre"
              loading="lazy" decoding="async" />
            <PrendaArt v-else :alto="72" />
            <span class="prenda__marca" aria-hidden="true">{{ puesta(item) ? '✓' : '+' }}</span>
          </span>
          <span class="prenda__datos">
            <span class="mono prenda__parte">{{ CORTOS[item.parte] }}</span>
            <span class="prenda__nombre">{{ item.producto.nombre }}</span>
            <span class="prenda__precio">
              <strong class="mono">{{ formatearPrecio(item.oferta.precio) }}</strong>
              <span class="mono muted">{{ nombreTienda(item.oferta.tienda) }}</span>
            </span>
          </span>
        </button>
      </li>
    </ul>
    <p v-else class="muted perchero__vacio">
      No hay prendas con esos filtros. Prueba con otra talla o con «Todos».
    </p>

    <BaseButton v-if="prendas.length > limite" variante="secundario" @click="limite += 24">
      Ver más prendas ({{ prendas.length - limite }})
    </BaseButton>

    <p v-if="sinPrendas.length" class="mono muted perchero__faltan">
      Todavía no tenemos prendas para: {{ sinPrendas.join(' · ').toLowerCase() }}.
    </p>
  </section>
</template>

<style scoped>
.perchero { display: flex; flex-direction: column; gap: var(--cep-sp-4); min-width: 0; }
.perchero__cabecera { display: flex; align-items: flex-end; justify-content: space-between; gap: var(--cep-sp-3); }
.perchero__titulo { margin: 0; font-size: var(--cep-fs-xl); }
.perchero__conteo { margin: 0; font-size: var(--cep-fs-xs); }

/* Pastillas por parte: se desplazan de lado en el teléfono en vez de partirse. */
.pastillas { display: flex; gap: var(--cep-sp-2); overflow-x: auto; padding-bottom: var(--cep-sp-1); scrollbar-width: none; }
.pastillas::-webkit-scrollbar { display: none; }
.pastilla {
  flex: none; display: inline-flex; align-items: center; gap: var(--cep-sp-2);
  min-height: var(--cep-control-h-sm, 2rem); padding: 0 var(--cep-sp-3);
  font: inherit; font-size: var(--cep-fs-sm); color: var(--cep-ink); cursor: pointer;
  background: var(--cep-surface); border: 1px solid var(--cep-line-media); border-radius: var(--cep-r-pill);
}
.pastilla:hover { border-color: var(--cep-line-fuerte); }
.pastilla[aria-selected='true'] { background: var(--cep-ink); color: var(--cep-surface); border-color: var(--cep-ink); }
.pastilla__total { font-family: var(--cep-font-mono); font-size: var(--cep-fs-2xs); opacity: 0.7; }

.filtros { display: grid; gap: var(--cep-sp-3); }
.filtros__fila { display: flex; flex-wrap: wrap; gap: var(--cep-sp-3); align-items: center; }
.segmentado { display: inline-flex; border: 1px solid var(--cep-line-media); border-radius: var(--cep-r-pill); overflow: hidden; }
.segmentado button {
  font: inherit; font-size: var(--cep-fs-sm); padding: var(--cep-sp-1) var(--cep-sp-3); min-height: 2rem;
  border: 0; background: var(--cep-surface); color: var(--cep-muted); cursor: pointer;
}
.segmentado button + button { border-left: 1px solid var(--cep-line); }
.segmentado button[aria-checked='true'] { background: var(--cep-wash, var(--cep-sunken)); color: var(--cep-ink); font-weight: 600; }
.filtro { display: inline-flex; align-items: center; gap: var(--cep-sp-2); }
.filtro__etiqueta { font-size: var(--cep-fs-sm); color: var(--cep-muted); }
.filtros__buscar { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: var(--cep-sp-3); align-items: center; }
.filtros__buscar .filtro { display: none; }
@media (min-width: 620px) { .filtros__buscar .filtro { display: inline-flex; } }
.filtro__control {
  min-height: 2rem; padding: 0 var(--cep-sp-2); font: inherit; font-size: var(--cep-fs-sm);
  background: var(--cep-surface); color: var(--cep-ink); border: 1px solid var(--cep-line-media);
  border-radius: var(--cep-r-md); cursor: pointer;
}

/* La rejilla del armario: fotos grandes sobre blanco, como en una app de closet. */
.rejilla { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--cep-sp-3); grid-template-columns: repeat(2, minmax(0, 1fr)); }
@media (min-width: 620px) { .rejilla { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
@media (min-width: 1180px) { .rejilla { grid-template-columns: repeat(4, minmax(0, 1fr)); } }
.rejilla > li { display: flex; }
.prenda {
  flex: 1; display: flex; flex-direction: column; padding: 0; text-align: left; cursor: pointer;
  font: inherit; color: var(--cep-ink); background: var(--cep-surface);
  border: 1px solid var(--cep-line); border-radius: var(--cep-r-lg); overflow: hidden;
  transition: border-color var(--cep-dur-1, 120ms) var(--cep-ease, ease), transform var(--cep-dur-1, 120ms) var(--cep-ease, ease);
}
.prenda:hover:not(:disabled) { border-color: var(--cep-line-fuerte); transform: translateY(-1px); }
.prenda:focus-visible { outline: 2px solid var(--cep-accent); outline-offset: 2px; }
.prenda:disabled { cursor: not-allowed; opacity: 0.5; }
.prenda--puesta { border-color: var(--cep-exito); box-shadow: inset 0 0 0 1px var(--cep-exito); }
.prenda__foto { position: relative; display: grid; place-items: center; aspect-ratio: 1; overflow: hidden; background: #fff; border-bottom: 1px solid var(--cep-line); }
/* La foto se ajusta al cuadrado: una imagen alta no estira la tarjeta. */
.prenda__foto img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: contain; padding: var(--cep-sp-2); }
.prenda__marca {
  position: absolute; top: var(--cep-sp-2); right: var(--cep-sp-2);
  display: grid; place-items: center; width: 1.75rem; height: 1.75rem; border-radius: 50%;
  font-weight: 700; background: var(--cep-surface); color: var(--cep-ink); border: 1px solid var(--cep-line-media);
}
.prenda--puesta .prenda__marca { background: var(--cep-exito); color: #fff; border-color: var(--cep-exito); }
.prenda__datos { display: grid; gap: 2px; padding: var(--cep-sp-2) var(--cep-sp-3) var(--cep-sp-3); }
.prenda__parte { font-size: var(--cep-fs-2xs); color: var(--cep-muted); text-transform: uppercase; letter-spacing: 0.05em; }
.prenda__nombre {
  font-size: var(--cep-fs-sm); line-height: 1.3;
  display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;
}
.prenda__precio { display: flex; flex-wrap: wrap; align-items: baseline; gap: var(--cep-sp-2); margin-top: var(--cep-sp-1); font-size: var(--cep-fs-xs); }
.prenda__precio strong { font-size: var(--cep-fs-md); color: var(--cep-exito); }
.perchero__vacio { margin: 0; padding: var(--cep-sp-8) 0; text-align: center; }
.perchero__faltan { margin: 0; font-size: var(--cep-fs-xs); }
</style>
