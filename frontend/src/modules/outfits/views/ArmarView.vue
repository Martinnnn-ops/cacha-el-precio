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
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseSkeleton from '@/shared/components/BaseSkeleton.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'

// Rectángulo después del armador: queda visible al terminar el flujo sin
// interrumpir la elección de prendas ni competir con el resumen de compra.
const BLOQUE_FICHA = import.meta.env.VITE_ADSENSE_SLOT_FICHA ?? ''

const catalogo = useComparadorStore()
const outfit = useOutfitStore()

const { cargando, error, actualizadoEn, datosDesactualizados } = storeToRefs(catalogo)
const {
  prendas,
  partesBloqueadas,
  seleccion,
  tallaRopa,
  tallaCalzado,
  tallasRopa,
  tallasCalzado,
  partesListas,
  partesActivas,
  completo,
  genero,
} = storeToRefs(outfit)

// Qué ranura se está editando. null = ninguna.
const editando = ref(null)

const parteEditando = computed(() => PARTES.find((p) => p.id === editando.value) ?? null)

const opcionesEditando = computed(() => (editando.value ? outfit.opcionesPara(editando.value) : []))

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
      <p class="eyebrow">Probador de precios</p>
      <h1 class="display armar__titulo">Arma tu outfit</h1>

      <p class="armar__bajada">
        Combina capas sin reemplazar lo que va debajo: polera con polerón, calcetines con calzado, y
        te decimos dónde conviene cada pieza.
      </p>
    </header>

    <BaseTicket class="herramientas" muescas="abajo">
      <div class="herramientas__progreso">
        <div>
          <p class="eyebrow herramientas__paso">Tu avance</p>
          <p class="display herramientas__estado">
            {{
              completo
                ? 'Outfit completo'
                : `${partesListas} de ${partesActivas.length} capas disponibles`
            }}
          </p>
        </div>

        <progress
          class="progreso"
          :value="partesListas"
          :max="Math.max(partesActivas.length, 1)"
          :aria-label="`${partesListas} de ${partesActivas.length} capas disponibles elegidas`"
        />
      </div>

      <div class="herramientas__acciones">
        <div class="campos-talla">
          <label class="campo-talla">
            <span class="mono campo-talla__etiqueta">Prendas</span>
            <select v-model="genero" class="campo-talla__control">
              <option value="">Todas</option><option>Mujer</option><option>Hombre</option><option>Unisex</option>
            </select>
          </label>
          <label class="campo-talla">
            <span class="mono campo-talla__etiqueta">Talla de ropa</span>
            <select v-model="tallaRopa" class="campo-talla__control">
              <option value="">Cualquiera</option>
              <option v-for="talla in tallasRopa" :key="talla" :value="talla">
                {{ talla }}
              </option>
            </select>
          </label>

          <label class="campo-talla">
            <span class="mono campo-talla__etiqueta">Talla de calzado</span>
            <select v-model="tallaCalzado" class="campo-talla__control">
              <option value="">Cualquiera</option>
              <option v-for="talla in tallasCalzado" :key="talla" :value="talla">
                {{ talla }}
              </option>
            </select>
          </label>
        </div>

        <BaseButton
          variante="secundario"
          :disabled="catalogo.productos.length === 0 || completo"
          @click="outfit.completarConMasBarato()"
        >
          Completar con lo más barato
        </BaseButton>
      </div>

      <p class="mono muted herramientas__nota">
        Gorro, ropa interior, calcetines y abrigos son opcionales. Puedes usar polera, polerón y chaqueta juntos.
        Separamos ropa y calzado para no mezclar una M con un 39. El cálculo usa sólo ofertas con
        stock en tus tallas.
      </p>
    </BaseTicket>

    <div class="armar__cuerpo">
      <div class="armar__panel">
        <!-- Elegir prenda sustituye a las ranuras en vez de abrirse encima:
             en móvil un panel flotante con una lista larga se maneja peor. -->
        <SelectorPrenda
          v-if="parteEditando"
          :parte="parteEditando"
          :opciones="opcionesEditando"
          :elegido="seleccion[parteEditando.id]"
          :talla="outfit.tallaParaParte(parteEditando.id)"
          @elegir="elegir"
          @cerrar="editando = null"
        />

        <div v-else-if="cargando && catalogo.productos.length === 0" class="ranuras">
          <BaseSkeleton v-for="n in PARTES.length" :key="n" :alto="210" radio="var(--cep-r-xs)" />
        </div>

        <div v-else class="ranuras">
          <RanuraParte
            v-for="parte in PARTES"
            :key="parte.id"
            :parte="parte"
            :producto="prendas[parte.id]"
            :oferta="outfit.ofertaPara(prendas[parte.id], parte.id)"
            :talla="outfit.tallaParaParte(parte.id)"
            :bloqueada="partesBloqueadas.includes(parte.id)"
            :opciones="outfit.opcionesPara(parte.id).length"
            @elegir="editando = parte.id"
            @quitar="outfit.quitarPrenda(parte.id)"
          />
        </div>
      </div>

      <aside class="armar__resumen">
        <ResumenOutfit />
      </aside>
    </div>

    <AdSlot :bloque="BLOQUE_FICHA" :alto="96" class="armar__anuncio" />
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

.herramientas {
  margin-bottom: var(--cep-sp-6);
}
.herramientas__progreso,
.herramientas__acciones {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--cep-sp-4);
  flex-wrap: wrap;
}
.herramientas__progreso {
  padding-bottom: var(--cep-sp-3);
  border-bottom: 1px dashed var(--cep-line-media);
}
.herramientas__paso,
.herramientas__estado,
.herramientas__nota {
  margin: 0;
}
.herramientas__estado {
  font-size: var(--cep-fs-lg);
}
.progreso {
  width: min(100%, 16.25rem);
  height: var(--cep-sp-2);
  overflow: hidden;
  border: 0;
  border-radius: var(--cep-r-pill);
  background: var(--cep-sunken);
}
.progreso::-webkit-progress-bar {
  background: var(--cep-sunken);
}
.progreso::-webkit-progress-value {
  background: var(--cep-exito);
}
.progreso::-moz-progress-bar {
  background: var(--cep-exito);
}
.herramientas__acciones {
  margin-top: var(--cep-sp-3);
}
.campos-talla {
  display: flex;
  flex-wrap: wrap;
  gap: var(--cep-sp-3);
}
.campo-talla {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-2);
}
.campo-talla__etiqueta {
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}
.campo-talla__control {
  min-height: var(--cep-control-h);
  padding: 0 var(--cep-sp-3);
  background: var(--cep-bg);
  color: var(--cep-ink);
  border: 1px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-md);
  cursor: pointer;
}
.herramientas__nota {
  margin-top: var(--cep-sp-2);
  font-size: var(--cep-fs-xs);
}

.armar__cuerpo {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--cep-sp-6);
  align-items: start;
}
@media (min-width: 900px) {
  .armar__cuerpo {
    grid-template-columns: minmax(0, 1.6fr) minmax(0, 1fr);
  }
}

.armar__resumen {
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-4);
}

.armar__anuncio {
  margin-top: var(--cep-sp-10);
}

.ranuras {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--cep-sp-4);
}
@media (min-width: 560px) {
  .ranuras {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
