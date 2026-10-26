<script setup>
import { computed } from 'vue'
import { storeToRefs } from 'pinia'

import { useTiendas } from '@/modules/comparador/composables/useTiendas'
import { nombreParte } from '@/modules/outfits/data/partes'
import { useOutfitStore } from '@/modules/outfits/store/outfit.store'
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'
import PrendaArt from '@/shared/components/PrendaArt.vue'
import { formatearPrecio } from '@/shared/utils/formato'

// El outfit puesto, ordenado como un cuerpo: de la cabeza a los pies, con el
// torso en sus tres capas una sobre otra. Lo obligatorio (torso, piernas,
// calzado) se ve siempre, aunque esté vacío; lo opcional aparece como «+ gorro»
// y al pulsarlo el perchero salta a esa parte.
const emit = defineEmits(['elegir'])

const outfit = useOutfitStore()
const { prendas, partesBloqueadas, partesActivas, partesListas, completo } = storeToRefs(outfit)
const { nombreTienda } = useTiendas()

const OBLIGATORIAS = ['torso-base', 'piernas', 'calzado']
// Zonas del cuerpo en orden; cada una con sus partes (el torso tiene tres capas).
const ZONAS = [
  { id: 'cabeza', nombre: 'Cabeza', partes: ['cabeza'] },
  { id: 'torso', nombre: 'Torso', partes: ['torso-base', 'torso-intermedia', 'torso-abrigo'] },
  { id: 'interior', nombre: 'Ropa interior', partes: ['interior'] },
  { id: 'piernas', nombre: 'Piernas', partes: ['piernas'] },
  { id: 'pies', nombre: 'Pies', partes: ['calcetines', 'calzado'] },
]
const CAPA = { 'torso-base': 'Base', 'torso-intermedia': 'Intermedia', 'torso-abrigo': 'Abrigo' }

// ¿Hay prendas en el catálogo para esta parte? Sin ellas no se ofrece «+».
const conPrendas = (parte) => outfit.opcionesPara(parte).length > 0

const filas = computed(() =>
  ZONAS.map((zona) => ({
    ...zona,
    puestas: zona.partes
      .filter((p) => prendas.value[p] || OBLIGATORIAS.includes(p))
      .map((p) => ({
        parte: p,
        producto: prendas.value[p],
        oferta: prendas.value[p] ? outfit.ofertaPara(prendas.value[p], p) : null,
        cubierta: partesBloqueadas.value.includes(p),
        hay: conPrendas(p),
      })),
    opcionales: zona.partes.filter((p) => !prendas.value[p] && !OBLIGATORIAS.includes(p) && conPrendas(p)),
  })).filter((z) => z.puestas.length || z.opcionales.length),
)

const etiquetaParte = (p) => CAPA[p] ? `${CAPA[p]}` : nombreParte(p)
</script>

<template>
  <BaseTicket class="tablero" muescas="abajo">
    <header class="tablero__cabecera">
      <div>
        <p class="eyebrow">Probador</p>
        <h2 class="display tablero__titulo">Tu outfit</h2>
      </div>
      <p class="mono tablero__avance">
        {{ completo ? 'Completo' : `${partesListas} de ${partesActivas.length}` }}
      </p>
    </header>
    <progress class="progreso" :value="partesListas" :max="Math.max(partesActivas.length, 1)"
      :aria-label="`${partesListas} de ${partesActivas.length} capas disponibles elegidas`" />
    <p class="sr-only">{{ partesListas }} de {{ partesActivas.length }} capas disponibles</p>

    <ol class="cuerpo">
      <li v-for="zona in filas" :key="zona.id" class="zona">
        <p class="mono zona__nombre">{{ zona.nombre }}</p>

        <ul class="piezas">
          <li v-for="pieza in zona.puestas" :key="pieza.parte">
            <!-- Pieza puesta -->
            <div v-if="pieza.producto && !pieza.cubierta" class="pieza">
              <span class="pieza__foto">
                <img v-if="pieza.producto.imagen" :src="pieza.producto.imagen" :alt="pieza.producto.nombre" />
                <PrendaArt v-else :alto="40" />
              </span>
              <span class="pieza__datos">
                <span v-if="CAPA[pieza.parte]" class="mono pieza__capa">{{ etiquetaParte(pieza.parte) }}</span>
                <span class="pieza__nombre">{{ pieza.producto.nombre }}</span>
                <span v-if="pieza.oferta" class="pieza__precio">
                  <strong class="mono">{{ formatearPrecio(pieza.oferta.precio) }}</strong>
                  <a :href="pieza.oferta.url" target="_blank" rel="noopener sponsored" class="pieza__tienda">
                    {{ nombreTienda(pieza.oferta.tienda) }} ↗
                  </a>
                </span>
                <span v-else class="mono pieza__sin">Sin stock en tu talla</span>
              </span>
              <button type="button" class="pieza__quitar" :aria-label="`Quitar ${pieza.producto.nombre}`"
                @click="outfit.quitarPrenda(pieza.parte)">×</button>
            </div>

            <!-- Cubierta por una prenda de cuerpo completo -->
            <p v-else-if="pieza.cubierta" class="mono muted pieza__cubierta">
              Cubierto por {{ pieza.producto?.nombre ?? 'la prenda de cuerpo completo' }}
            </p>

            <!-- Obligatoria vacía -->
            <button v-else-if="pieza.hay" type="button" class="hueco" @click="emit('elegir', pieza.parte)">
              <span class="hueco__mas" aria-hidden="true">+</span>
              Elegir {{ nombreParte(pieza.parte).toLowerCase() }}
            </button>
            <p v-else class="mono muted hueco hueco--vacio">
              {{ nombreParte(pieza.parte) }}: todavía no hay prendas
            </p>
          </li>
        </ul>

        <div v-if="zona.opcionales.length" class="opcionales">
          <button v-for="p in zona.opcionales" :key="p" type="button" class="opcional" @click="emit('elegir', p)">
            + {{ CAPA[p] ? `capa ${CAPA[p].toLowerCase()}` : nombreParte(p).toLowerCase() }}
          </button>
        </div>
      </li>
    </ol>

    <BaseButton variante="secundario" bloque :disabled="completo" @click="outfit.completarConMasBarato()">
      Completar con lo más barato
    </BaseButton>
  </BaseTicket>
</template>

<style scoped>
.tablero { border-top: 3px solid var(--cep-accent); }
.tablero__cabecera { display: flex; align-items: flex-end; justify-content: space-between; gap: var(--cep-sp-3); }
.tablero__titulo { margin: 0; font-size: var(--cep-fs-xl); }
.tablero__avance { margin: 0; font-size: var(--cep-fs-xs); color: var(--cep-muted); }
.progreso {
  width: 100%; height: var(--cep-sp-15); margin: var(--cep-sp-3) 0 var(--cep-sp-4);
  border: 0; border-radius: var(--cep-r-pill); overflow: hidden; background: var(--cep-sunken);
}
.progreso::-webkit-progress-bar { background: var(--cep-sunken); }
.progreso::-webkit-progress-value { background: var(--cep-exito); }
.progreso::-moz-progress-bar { background: var(--cep-exito); }

/* El cuerpo: una línea vertical une las zonas, de la cabeza a los pies. */
.cuerpo { list-style: none; margin: 0 0 var(--cep-sp-4); padding: 0; display: grid; gap: var(--cep-sp-3); }
.zona { position: relative; padding-left: var(--cep-sp-4); border-left: 2px solid var(--cep-line-media); }
.zona__nombre {
  margin: 0 0 var(--cep-sp-2); font-size: var(--cep-fs-2xs); color: var(--cep-muted);
  text-transform: uppercase; letter-spacing: 0.06em;
}
.zona__nombre::before {
  content: ''; position: absolute; left: -5px; top: 4px; width: 8px; height: 8px;
  border-radius: 50%; background: var(--cep-surface); border: 2px solid var(--cep-line-fuerte);
}
.piezas { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--cep-sp-2); }

.pieza {
  display: grid; grid-template-columns: 3.5rem minmax(0, 1fr) auto; gap: var(--cep-sp-3); align-items: center;
  padding: var(--cep-sp-2); background: var(--cep-surface); border: 1px solid var(--cep-line); border-radius: var(--cep-r-lg);
}
.pieza__foto { display: grid; place-items: center; width: 3.5rem; height: 3.5rem; background: #fff; border-radius: var(--cep-r-md); overflow: hidden; }
.pieza__foto img { width: 100%; height: 100%; object-fit: contain; }
.pieza__datos { display: grid; gap: 2px; min-width: 0; }
.pieza__capa { font-size: var(--cep-fs-2xs); color: var(--cep-accent); text-transform: uppercase; letter-spacing: 0.05em; }
.pieza__nombre { font-size: var(--cep-fs-sm); line-height: 1.3; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.pieza__precio { display: flex; flex-wrap: wrap; align-items: center; gap: var(--cep-sp-2); }
.pieza__precio strong { color: var(--cep-exito); font-size: var(--cep-fs-sm); }
.pieza__tienda { font-size: var(--cep-fs-xs); color: var(--cep-accent); text-decoration: none; }
.pieza__tienda:hover { text-decoration: underline; }
.pieza__sin { font-size: var(--cep-fs-2xs); color: var(--cep-alerta); }
.pieza__quitar {
  align-self: start; width: 1.75rem; height: 1.75rem; border-radius: 50%; cursor: pointer;
  font-size: 1.1rem; line-height: 1; color: var(--cep-muted); background: transparent; border: 1px solid transparent;
}
.pieza__quitar:hover { color: var(--cep-alerta); border-color: var(--cep-line-media); }
.pieza__cubierta { margin: 0; font-size: var(--cep-fs-xs); padding: var(--cep-sp-2) 0; }

.hueco {
  display: flex; align-items: center; gap: var(--cep-sp-3); width: 100%; min-height: 3.5rem;
  padding: var(--cep-sp-2) var(--cep-sp-3); font: inherit; font-size: var(--cep-fs-sm); color: var(--cep-ink);
  background: transparent; border: 1.5px dashed var(--cep-line-fuerte); border-radius: var(--cep-r-lg); cursor: pointer; text-align: left;
}
button.hueco:hover { border-color: var(--cep-accent); color: var(--cep-accent); }
.hueco__mas { display: grid; place-items: center; width: 2rem; height: 2rem; border-radius: 50%; background: var(--cep-sunken); font-weight: 700; }
.hueco--vacio { margin: 0; cursor: default; font-size: var(--cep-fs-xs); border-color: var(--cep-line-media); }

.opcionales { display: flex; flex-wrap: wrap; gap: var(--cep-sp-2); margin-top: var(--cep-sp-2); }
.opcional {
  font: inherit; font-size: var(--cep-fs-xs); padding: var(--cep-sp-1) var(--cep-sp-3); cursor: pointer;
  color: var(--cep-muted); background: transparent; border: 1px dashed var(--cep-line-fuerte); border-radius: var(--cep-r-pill);
}
.opcional:hover { color: var(--cep-ink); border-style: solid; }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
</style>
