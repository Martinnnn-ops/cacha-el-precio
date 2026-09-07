<script setup>
import { computed, onMounted } from 'vue'
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
import { ahorroMaximo, minimoHistorico, precioMasBajo } from '@/shared/utils/precios'
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

    return {
      producto,
      hoy,
      minimo,
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
        <div class="columna__arte">
          <PrendaArt :alto="110" />
        </div>

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
                <span class="mono fila__donde">
                  en {{ nombreTienda(col.hoy.tienda) }}
                </span>
              </template>
              <span v-else class="muted">sin stock</span>
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
            :to="{ name: 'producto-detalle', params: { id: col.producto.id, slug: slugProducto(col.producto) } }"
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

/* Una columna por prenda. En móvil van apiladas: tres columnas de 100px no se
   comparan, se adivinan. */
.columnas {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--cep-sp-4);
  align-items: start;
}
@media (min-width: 720px) {
  .columnas {
    grid-template-columns: repeat(var(--cuantas), minmax(0, 1fr));
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
  margin-bottom: var(--cep-sp-3);
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
