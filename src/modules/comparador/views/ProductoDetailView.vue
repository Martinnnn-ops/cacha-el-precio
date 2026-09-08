<script setup>
import { computed, watch } from 'vue'
import { storeToRefs } from 'pinia'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import { useTiendas } from '@/modules/comparador/composables/useTiendas'
import { formatearFecha, formatearPrecio } from '@/shared/utils/formato'
import { descuento, minimoHistorico, precioMasBajo } from '@/shared/utils/precios'
import { slugProducto } from '@/shared/utils/slug'
import DetalleSkeleton from '@/modules/comparador/components/DetalleSkeleton.vue'
import EligeTuTienda from '@/modules/comparador/components/EligeTuTienda.vue'
import FichaCaracteristicas from '@/modules/comparador/components/FichaCaracteristicas.vue'
import GraficoPrecios from '@/modules/comparador/components/GraficoPrecios.vue'
import SelloDescuento from '@/modules/comparador/components/SelloDescuento.vue'
import AdSlot from '@/shared/components/AdSlot.vue'
import BaseButton from '@/shared/components/BaseButton.vue'
import MigasDePan from '@/shared/components/MigasDePan.vue'
import EnlaceTienda from '@/shared/components/EnlaceTienda.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'
import PrendaArt from '@/shared/components/PrendaArt.vue'

const props = defineProps({
  slug: { type: String, required: true },
})

const store = useComparadorStore()
const { producto, cargando, error } = storeToRefs(store)
const { nombreTienda, colorTienda } = useTiendas()

// La URL del detalle es el slug, pero la API identifica los productos por id.
// La vista resuelve slug -> id mirando el catálogo ya cargado en el store (si
// no está cargado, se trae antes). Con el slug se llega a la URL y con el id
// se pide la ficha; si el slug no corresponde a ningún producto, se muestra
// como si ya no estuviera.
async function cargarPorSlug(slug) {
  if (store.productos.length === 0) {
    await store.cargarProductos()
  }

  const encontrado = store.productos.find((p) => slugProducto(p) === slug)

  if (!encontrado) {
    store.producto = null
    return
  }

  await store.cargarProducto(encontrado.id)

}

watch(() => props.slug, cargarPorSlug, { immediate: true })

const masBarato = computed(() => precioMasBajo(producto.value))

// Con una sola fuente de precio no hay comparación: la ficha deja de hablar de
// "la más barata" y de titular una tabla como "Precio por tienda".
const comparando = computed(() => (producto.value?.precios?.length ?? 0) > 1)

// La marca no siempre viene (la API no la guarda). Sin este guard el eyebrow
// salía como " · Poleras", con el separador colgando al principio.
// Hueco de anuncio de la ficha: la columna del dinero es donde más tiempo se
// mira, así que es donde un anuncio vale algo sin estorbar la lectura.
const BLOQUE_FICHA = import.meta.env.VITE_ADSENSE_SLOT_FICHA ?? ''

const minimo = computed(() => minimoHistorico(producto.value))

// Cuánto se aparta el precio de hoy del mínimo que hemos visto nunca. Es el
// número que da sentido al sello: el descuento que declara la tienda compara
// contra su propia referencia, esto compara contra la realidad.
const sobreElMinimo = computed(() => {
  const hoy = masBarato.value
  const min = minimo.value

  if (!hoy || !min || min.precio <= 0) return null

  return ((hoy.precio - min.precio) / min.precio) * 100
})

const migas = computed(() => {
  const p = producto.value

  if (!p) return []

  const camino = [{ texto: 'Inicio', a: { name: 'inicio' } }]

  if (p.categoria) {
    camino.push({
      texto: p.categoria,
      a: { name: 'comparador', query: { categoria: p.categoria } },
    })
  }

  // El último sin enlace: es la página donde ya estás.
  camino.push({ texto: p.nombre })

  return camino
})

// Las specs pueden venir agrupadas (array) o planas (objeto): las dos formas
// cuentan como "tiene ficha".
const tieneFicha = computed(() => {
  const specs = producto.value?.specs

  if (Array.isArray(specs)) {
    return specs.some((g) => Object.keys(g?.filas ?? {}).length > 0)
  }

  return Object.keys(specs ?? {}).length > 0
})

const tieneDestacadas = computed(
  () => (producto.value?.destacadas?.length ?? 0) > 0,
)
const tieneJuicio = computed(
  () =>
    (producto.value?.pros?.length ?? 0) > 0 ||
    (producto.value?.contras?.length ?? 0) > 0,
)

const subtitulo = computed(() =>
  [producto.value?.marca, producto.value?.categoria].filter(Boolean).join(' · '),
)

// Serie por tienda a partir del historial, para el gráfico.

</script>

<template>
  <section class="detalle">
    <BaseButton variante="texto" to="/">← Volver al comparador</BaseButton>

    <DetalleSkeleton v-if="cargando" />

    <div v-else-if="error" class="aviso aviso--error" role="alert">
      <p>{{ error }}</p>
      <BaseButton @click="cargarPorSlug(props.slug)">Reintentar</BaseButton>
    </div>

    <div v-else-if="!producto" class="aviso">
      <p class="aviso__titulo">Este producto ya no está</p>
      <p>Puede que la tienda lo haya retirado del catálogo.</p>
    </div>

    <template v-else>
      <MigasDePan :items="migas" />

      <!-- ——— la prenda, con su sello al lado ——— -->
      <header class="cabecera">
        <BaseTicket class="cabecera__imagen" :relleno="false">
          <div class="detalle__marco">
            <img
              v-if="producto.imagen"
              :src="producto.imagen"
              :alt="producto.nombre"
              class="detalle__foto"
            />
            <PrendaArt v-else :alto="220" :fondo="false" />
          </div>
        </BaseTicket>

        <div class="cabecera__texto">
          <p v-if="subtitulo" class="eyebrow">
            {{ subtitulo
            }}<template v-if="producto.codigo"> · {{ producto.codigo }}</template>
          </p>

          <h1 class="display cabecera__titulo">{{ producto.nombre }}</h1>

          <div v-if="masBarato" class="cabecera__resumen">
            <div>
              <p class="mono cabecera__etiqueta">Desde</p>
              <p class="cabecera__monto">
                {{ formatearPrecio(masBarato.precio) }}
              </p>
              <p v-if="minimo" class="mono cabecera__minimo">
                Mínimo que hemos visto: {{ formatearPrecio(minimo.precio) }}
              </p>
            </div>

            <!-- El sello se queda arriba: es lo que dice de un vistazo si hoy
                 es buen momento, y ahí se ve sin bajar la página. -->
            <SelloDescuento :porcentaje="sobreElMinimo" />
          </div>
        </div>
      </header>

      <!-- ——— dónde comprarla: el bloque que resuelve la visita ——— -->
      <EligeTuTienda :ofertas="producto.precios" :comparando="comparando" />

      <!-- ——— características ——— -->
      <section v-if="tieneFicha" class="bloque">
        <h2 class="display bloque__titulo">Características</h2>
        <FichaCaracteristicas :specs="producto.specs" />
      </section>

      <!-- ——— descripción ——— -->
      <section v-if="producto.descripcion" class="bloque">
        <h2 class="display bloque__titulo">Descripción</h2>

        <BaseTicket>
          <p class="bloque__parrafo">{{ producto.descripcion }}</p>
        </BaseTicket>
      </section>

      <!-- ——— lo que hay que saber ——— -->
      <section v-if="tieneDestacadas" class="bloque">
        <h2 class="display bloque__titulo">Lo que hay que saber</h2>

        <BaseTicket>
          <ul class="destacadas">
            <li v-for="(x, i) in producto.destacadas" :key="i">{{ x }}</li>
          </ul>
        </BaseTicket>
      </section>

      <!-- ——— a favor / en contra ——— -->
      <section v-if="tieneJuicio" class="bloque">
        <h2 class="display bloque__titulo">A favor y en contra</h2>

        <BaseTicket>
          <div class="juicio">
            <div v-if="producto.pros?.length">
              <p class="eyebrow juicio__pros">A favor</p>
              <ul class="juicio__lista juicio__lista--pros">
                <li v-for="(x, i) in producto.pros" :key="i">{{ x }}</li>
              </ul>
            </div>

            <div v-if="producto.contras?.length">
              <p class="eyebrow juicio__contras">En contra</p>
              <ul class="juicio__lista juicio__lista--contras">
                <li v-for="(x, i) in producto.contras" :key="i">{{ x }}</li>
              </ul>
            </div>
          </div>
        </BaseTicket>
      </section>

      <AdSlot :bloque="BLOQUE_FICHA" :alto="120" />

      <!-- ——— historial ———
           La sección se muestra SIEMPRE. Antes se ocultaba entera cuando no
           había datos, y con un backend sin historial nadie llegaba a saber
           que esta parte existe. -->
      <section class="panel">
        <h2 class="panel__titulo">Historial de precios</h2>

        <GraficoPrecios :historial="producto.historial ?? []" />
      </section>
    </template>
  </section>
</template>

<style scoped>
/* ——— cabecera: la prenda y su sello ——— */
.cabecera {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--cep-sp-5);
  align-items: center;
  margin: var(--cep-sp-4) 0 var(--cep-sp-7);
}
@media (min-width: 560px) {
  .cabecera {
    grid-template-columns: minmax(0, 0.8fr) minmax(0, 1.2fr);
  }
}
.cabecera__titulo {
  margin: 0 0 var(--cep-sp-4);
  font-size: var(--cep-fs-2xl);
}
.cabecera__resumen {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--cep-sp-4);
  flex-wrap: wrap;
}
.cabecera__etiqueta {
  margin: 0;
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}
.cabecera__monto {
  margin: 0;
  font-family: var(--cep-font-display);
  font-size: var(--cep-fs-3xl);
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  line-height: 1;
}
.cabecera__minimo {
  margin: var(--cep-sp-15) 0 0;
  font-size: var(--cep-fs-xs);
  color: var(--cep-exito);
}

/* ——— bloques de la ficha ——— */
.bloque {
  margin-top: var(--cep-sp-8);
}
.bloque__titulo {
  margin: 0 0 var(--cep-sp-3);
  font-size: var(--cep-fs-xl);
}
.bloque__parrafo {
  margin: 0;
  font-size: var(--cep-fs-base);
  line-height: var(--cep-lh-normal);
}

.destacadas {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-25);
}
.destacadas li {
  display: flex;
  gap: var(--cep-sp-2);
  font-size: var(--cep-fs-base);
  line-height: var(--cep-lh-snug);
}
.destacadas li::before {
  content: '·';
  color: var(--cep-accent);
  font-weight: 700;
}


/* Tres columnas en escritorio; en pantallas medias el dinero pasa debajo de la
   prenda y la ficha ocupa el ancho, que se lee mejor que tres tiras estrechas. */

.detalle__marco {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: var(--cep-sp-25);
  min-height: 280px;
}
.detalle__foto {
  width: 100%;
  height: 280px;
  object-fit: cover;
  border-radius: var(--cep-r-xs);
}
.detalle__nota {
  margin: var(--cep-sp-2) 0 0;
  font-size: var(--cep-fs-2xs);
}


/* ——— ficha técnica ——— */

/* ——— a favor / en contra ——— */
.juicio {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--cep-sp-4);
}
@media (min-width: 560px) {
  .juicio {
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  }
}
.juicio__pros {
  color: var(--cep-exito);
}
.juicio__contras {
  color: var(--cep-alerta);
}
.juicio__lista {
  list-style: none;
  margin: var(--cep-sp-2) 0 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-2);
}
.juicio__lista li {
  display: flex;
  gap: var(--cep-sp-2);
  font-size: var(--cep-fs-sm);
  line-height: var(--cep-lh-snug);
}
.juicio__lista--pros li::before {
  content: '+';
  color: var(--cep-exito);
  font-weight: 700;
}
.juicio__lista--contras li::before {
  content: '–';
  color: var(--cep-alerta);
  font-weight: 700;
}

/* ——— el dinero ——— */
.tachado {
  color: var(--cep-muted);
  text-decoration: line-through;
  text-decoration-color: var(--cep-alerta);
}
.verde {
  color: var(--cep-exito);
}

.detalle__mejor strong {
  color: var(--cep-exito);
}

.panel {
  margin-bottom: 24px;
  padding: 18px;
  background: var(--cep-surface);
  border: 1px solid var(--cep-line);
  border-radius: 10px;
}
.panel__titulo {
  margin: 0 0 12px;
  font-size: 13px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--cep-muted);
}
.panel__vacio {
  margin: 0;
  color: var(--cep-muted);
}

.tabla {
  width: 100%;
  border-collapse: collapse;
  font-size: 14px;
}
.tabla th {
  text-align: left;
  padding: 0 8px 8px;
  font-size: 11px;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--cep-muted);
  font-weight: 500;
}
.tabla td {
  padding: 10px 8px;
  border-top: 1px solid var(--cep-line);
}
.tabla__fila--mejor {
  background: color-mix(in srgb, var(--cep-exito) 10%, transparent);
}
.tabla__fila--mejor .tabla__monto {
  color: var(--cep-exito);
  font-weight: 600;
}
.tabla__monto {
  font-variant-numeric: tabular-nums;
}
.tabla__antes {
  color: var(--cep-muted);
  font-size: 13px;
}
.tabla__descuento {
  margin-left: 6px;
  color: var(--cep-alerta);
  font-weight: 600;
}

.tabla__ir {
  text-align: right;
  white-space: nowrap;
}

.punto {
  display: inline-block;
  width: 9px;
  height: 9px;
  border-radius: 50%;
  margin-right: 7px;
}

.chip {
  display: inline-block;
  padding: 2px 9px;
  border-radius: 999px;
  border: 1px solid var(--cep-line-fuerte);
  font-size: 11px;
  color: var(--cep-muted);
}
.chip--ok {
  border-color: var(--cep-exito);
  color: var(--cep-exito);
}

.gráfico {
  width: 100%;
  height: auto;
}
.gráfico__fecha {
  font-size: 10px;
  fill: var(--cep-muted);
}



.aviso {
  padding: 56px 20px;
  text-align: center;
  color: var(--cep-muted);
}
.aviso__titulo {
  margin: 0 0 4px;
  font-size: 17px;
  font-weight: 600;
  color: var(--cep-ink);
}
.aviso--error {
  color: var(--cep-alerta);
}
.aviso--error p {
  margin-bottom: 16px;
}
</style>
