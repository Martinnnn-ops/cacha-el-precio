<script setup>
import { computed, onMounted, reactive } from 'vue'
import { RouterLink } from 'vue-router'
import { storeToRefs } from 'pinia'

import { useTiendas } from '@/modules/comparador/composables/useTiendas'
import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import SelloDescuento from '@/modules/comparador/components/SelloDescuento.vue'
import { useCompararStore } from '@/modules/comparar/store/comparar.store'
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'
import EnlaceTienda from '@/shared/components/EnlaceTienda.vue'
import PrendaArt from '@/shared/components/PrendaArt.vue'
import { formatearPrecio } from '@/shared/utils/formato'
import { detallesDeNombre } from '@/shared/utils/detalles'
import { ahorroMaximo, minimoHistorico, precioMasBajo } from '@/shared/utils/precios'
import { clasificarTalla } from '@/shared/utils/tallas'
import { slugProducto } from '@/shared/utils/slug'

const catalogo = useComparadorStore()
const comparar = useCompararStore()
const { nombreTienda } = useTiendas()
const { cargando } = storeToRefs(catalogo)

// Cada columna con sus cifras ya resueltas: el template no tiene que llamar a
// las funciones de precio cuatro veces por producto.
const columnas = computed(() =>
  comparar.productos.map((producto) => {
    const hoy = precioMasBajo(producto)
    const minimo = minimoHistorico(producto)

    const tallas = [...new Set(producto.precios.filter((o) => o.stock).flatMap((o) => o.tallas ?? []))]
      .map((nombre) => ({ nombre, ...clasificarTalla(nombre) }))
      .sort((a, b) => a.grupo - b.grupo || a.orden - b.orden)
      .map((t) => t.nombre)

    return {
      producto,
      hoy,
      minimo,
      tallas,
      detalles: Object.fromEntries(detallesDeNombre(producto.nombre).map((d) => [d.clave, d.valor])),
      ahorro: ahorroMaximo(producto),
      // Cuánto se aparta el precio de hoy del mínimo registrado.
      sobreMinimo:
        hoy && minimo && minimo.precio > 0
          ? ((hoy.precio - minimo.precio) / minimo.precio) * 100
          : null,
    }
  }),
)

// El más barato de los comparados, para marcarlo. Sólo tiene sentido con dos
// o más: con uno solo, "el más barato" es el único.
const idMasBarato = computed(() => {
  if (columnas.value.length < 2) return null

  const conPrecio = columnas.value.filter((c) => c.hoy)

  if (conPrecio.length === 0) return null

  return conPrecio.reduce((mejor, c) =>
    c.hoy.precio < mejor.hoy.precio ? c : mejor,
  ).producto.id
})

// Cuánto más cuesta cada una que la más barata de las comparadas: es la
// diferencia que de verdad decide, y antes había que restarla de cabeza.
const precioMasBajoComparado = computed(() => {
  const precios = columnas.value.filter((c) => c.hoy).map((c) => c.hoy.precio)
  return precios.length > 1 ? Math.min(...precios) : null
})

// Las tallas que tienen todas: si buscas la tuya, ahí está la respuesta.
const tallasEnComun = computed(() => {
  if (columnas.value.length < 2) return new Set()
  const [primera, ...resto] = columnas.value.map((c) => new Set(c.tallas))
  return new Set([...primera].filter((t) => resto.every((r) => r.has(t))))
})

// Una fila por cada detalle que aparezca en al menos una de las prendas, en el
// mismo orden para todas: si una no lo dice, su celda queda en "—".
const clavesDetalle = computed(() => {
  const orden = ['Manga', 'Material', 'Cuello', 'Calce']
  const presentes = new Set(columnas.value.flatMap((c) => Object.keys(c.detalles)))
  return orden.filter((clave) => presentes.has(clave))
})

const imagenFallida = reactive({})

onMounted(() => catalogo.cargarProductos())
</script>

<template>
  <div class="comparacion">
    <header class="comparacion__cabecera">
      <h1 class="display comparacion__titulo">Comparación</h1>

      <p v-if="comparar.cuantos > 0" class="comparacion__bajada">
        {{ comparar.cuantos }}
        {{ comparar.cuantos === 1 ? 'prenda' : 'prendas' }} lado a lado.
        Puedes comparar hasta {{ comparar.MAXIMO }}.
      </p>
    </header>

    <!-- vacío -->
    <div v-if="comparar.cuantos === 0 && !cargando" class="vacio">
      <p class="display vacio__titulo">Todavía no has elegido nada</p>

      <p class="vacio__texto">
        Pulsa «Comparar» en las prendas que te interesen y aparecerán aquí, una
        al lado de la otra.
      </p>

      <BaseButton :to="{ name: 'comparador' }">Ver el catálogo</BaseButton>
    </div>

    <div v-else class="columnas" :style="{ '--cuantas': columnas.length }">
      <BaseTicket
        v-for="col in columnas"
        :key="col.producto.id"
        class="columna"
        :class="{ 'columna--mejor': col.producto.id === idMasBarato }"
      >
        <!-- La foto es lo que distingue una polera blanca de otra polera
             blanca. Antes la columna dibujaba siempre la silueta genérica. -->
        <RouterLink
          class="columna__arte"
          :to="{ name: 'producto-detalle', params: { slug: slugProducto(col.producto) } }"
        >
          <img
            v-if="col.producto.imagen && !imagenFallida[col.producto.id]"
            :src="col.producto.imagen"
            :alt="col.producto.nombre"
            loading="lazy"
            decoding="async"
            @error="imagenFallida[col.producto.id] = true"
          />
          <PrendaArt v-else :alto="160" />
        </RouterLink>

        <p v-if="col.producto.marca || col.producto.categoria" class="eyebrow">
          {{ [col.producto.marca, col.producto.categoria].filter(Boolean).join(' · ') }}
        </p>

        <h2 class="display columna__nombre">{{ col.producto.nombre }}</h2>

        <div class="divisor" />

        <!-- Las mismas filas en el mismo orden en todas las columnas: es lo
             que permite comparar de un vistazo horizontal. -->
        <dl class="filas">
          <div class="fila">
            <dt class="mono fila__clave">Precio hoy</dt>
            <dd class="fila__valor">
              <template v-if="col.hoy">
                {{ formatearPrecio(col.hoy.precio) }}
                <span
                  v-if="precioMasBajoComparado !== null && col.hoy.precio === precioMasBajoComparado"
                  class="mono chip chip--mejor"
                >la más barata</span>
                <span v-else-if="precioMasBajoComparado !== null" class="mono chip">
                  +{{ formatearPrecio(col.hoy.precio - precioMasBajoComparado) }}
                </span>
                <span class="mono fila__donde">
                  en {{ nombreTienda(col.hoy.tienda) }}
                </span>
              </template>
              <span v-else class="muted">sin stock</span>
            </dd>
          </div>

          <div v-if="columnas.some((c) => c.producto.genero)" class="fila">
            <dt class="mono fila__clave">Para</dt>
            <dd class="fila__valor">{{ col.producto.genero || '—' }}</dd>
          </div>

          <div class="fila">
            <dt class="mono fila__clave">
              Tallas con stock
              <span v-if="tallasEnComun.size" class="fila__nota">· en negrita, las que tienen todas</span>
            </dt>
            <dd class="fila__valor fila__tallas">
              <template v-if="col.tallas.length">
                <span
                  v-for="t in col.tallas"
                  :key="t"
                  class="mono talla"
                  :class="{ 'talla--comun': tallasEnComun.has(t) }"
                >{{ t }}</span>
              </template>
              <span v-else class="muted">—</span>
            </dd>
          </div>

          <div v-for="clave in clavesDetalle" :key="clave" class="fila">
            <dt class="mono fila__clave">{{ clave }} <span class="fila__nota">· según el nombre</span></dt>
            <dd class="fila__valor">
              <span v-if="col.detalles[clave]">{{ col.detalles[clave] }}</span>
              <span v-else class="muted">—</span>
            </dd>
          </div>

          <div class="fila">
            <dt class="mono fila__clave">Mínimo registrado</dt>
            <dd class="fila__valor">
              <span v-if="col.minimo" class="verde">
                {{ formatearPrecio(col.minimo.precio) }}
              </span>
              <span v-else class="muted">—</span>
            </dd>
          </div>

          <div class="fila">
            <dt class="mono fila__clave">Diferencia entre tiendas</dt>
            <dd class="fila__valor">
              <span v-if="col.ahorro > 0">{{ formatearPrecio(col.ahorro) }}</span>
              <span v-else class="muted">—</span>
            </dd>
          </div>

          <div class="fila">
            <dt class="mono fila__clave">Tiendas que la venden</dt>
            <dd class="fila__valor">
              {{ col.producto.precios.filter((o) => o.stock).length }}
            </dd>
          </div>
        </dl>

        <div class="columna__sello">
          <SelloDescuento :porcentaje="col.sobreMinimo" />
        </div>

        <div class="columna__acciones">
          <EnlaceTienda
            v-if="col.hoy"
            :url="col.hoy.url"
            :tienda="nombreTienda(col.hoy.tienda)"
            tamano="chico"
          />

          <RouterLink
            class="columna__ficha"
            :to="{ name: 'producto-detalle', params: { slug: slugProducto(col.producto) } }"
          >
            Ver ficha completa
          </RouterLink>

          <button
            type="button"
            class="columna__quitar"
            @click="comparar.quitar(col.producto.id)"
          >
            Quitar de la comparación
          </button>
        </div>
      </BaseTicket>
    </div>
  </div>
</template>

<style scoped>
.comparacion__cabecera {
  max-width: 56ch;
  margin-bottom: var(--cep-sp-6);
}
.comparacion__titulo {
  margin: 0 0 var(--cep-sp-2);
  font-size: var(--cep-fs-2xl);
}
.comparacion__bajada {
  margin: 0;
  color: var(--cep-muted);
}

/* Una columna por prenda. En el teléfono no se apilan (una debajo de la otra
   no se compara: hay que acordarse de la de arriba) ni se aprietan a 100 px:
   van lado a lado y se deslizan, con la siguiente asomándose. */
.columnas {
  display: grid;
  grid-auto-flow: column;
  grid-auto-columns: min(82%, 300px);
  gap: var(--cep-sp-3);
  align-items: stretch;
  overflow-x: auto;
  scroll-snap-type: x mandatory;
  padding-bottom: var(--cep-sp-2);
  scrollbar-width: none;
}
.columnas::-webkit-scrollbar {
  display: none;
}
.columnas > * {
  scroll-snap-align: start;
}
@media (min-width: 720px) {
  .columnas {
    grid-auto-flow: row;
    grid-template-columns: repeat(var(--cuantas), minmax(0, 1fr));
    overflow: visible;
  }
}

.columna {
  display: flex;
  flex-direction: column;
}
.columna--mejor {
  border-left: 3px solid var(--cep-exito);
}
.columna__arte {
  display: block;
  margin-bottom: var(--cep-sp-3);
  overflow: hidden;
  border-radius: var(--cep-r-sm);
}
.columna__arte img {
  display: block;
  width: 100%;
  height: 200px;
  object-fit: contain;
  background: #fff;
}
.columna__nombre {
  margin: 0;
  font-size: var(--cep-fs-lg);
}

.filas {
  margin: 0;
}
.fila {
  padding: var(--cep-sp-2) 0;
  border-bottom: 1px solid var(--cep-line);
}
.fila:last-child {
  border-bottom: none;
}
.fila__clave {
  margin: 0 0 2px;
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}
.fila__valor {
  margin: 0;
  font-size: var(--cep-fs-base);
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}
.fila__donde {
  display: block;
  font-size: var(--cep-fs-xs);
  font-weight: 400;
  color: var(--cep-muted);
}
.fila__nota {
  color: var(--cep-muted);
  opacity: 0.75;
}
.chip {
  display: inline-block;
  margin-left: var(--cep-sp-15);
  padding: 1px var(--cep-sp-2);
  border: 1px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-pill);
  font-size: var(--cep-fs-2xs);
  font-weight: 400;
  color: var(--cep-muted);
  vertical-align: middle;
}
.chip--mejor {
  border-color: var(--cep-exito);
  color: var(--cep-exito);
}
.fila__tallas {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 4px;
}
.talla {
  padding: 1px 6px;
  border: 1px solid var(--cep-line-media);
  border-radius: var(--cep-r-xs);
  font-size: var(--cep-fs-2xs);
  font-weight: 400;
}
.talla--comun {
  border-color: var(--cep-ink);
  font-weight: 700;
}
.verde {
  color: var(--cep-exito);
}

.columna__sello {
  display: flex;
  justify-content: center;
  padding: var(--cep-sp-4) 0;
}
.columna__acciones {
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-2);
  margin-top: auto;
}
.columna__ficha {
  color: var(--cep-accent);
  font-size: var(--cep-fs-sm);
  font-weight: 600;
  text-align: center;
  text-decoration: none;
}
.columna__ficha:hover {
  text-decoration: underline;
}
.columna__quitar {
  padding: var(--cep-sp-15);
  background: none;
  border: none;
  color: var(--cep-muted);
  font-size: var(--cep-fs-xs);
  cursor: pointer;
}
.columna__quitar:hover {
  color: var(--cep-alerta);
}

.vacio {
  max-width: 46ch;
  margin: 0 auto;
  padding: var(--cep-sp-16) 0;
  text-align: center;
}
.vacio__titulo {
  margin: 0 0 var(--cep-sp-2);
  font-size: var(--cep-fs-xl);
}
.vacio__texto {
  margin: 0 0 var(--cep-sp-5);
  color: var(--cep-muted);
}
</style>
