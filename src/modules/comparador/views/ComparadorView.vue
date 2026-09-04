<script setup>
import { onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { storeToRefs } from 'pinia'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import { textoDeUrl } from '@/shared/utils/rutas'
import PanelFiltros from '@/modules/comparador/components/PanelFiltros.vue'
import ProductoCard from '@/modules/comparador/components/ProductoCard.vue'
import ProductoCardSkeleton from '@/modules/comparador/components/ProductoCardSkeleton.vue'
import AdSlot from '@/shared/components/AdSlot.vue'
import AvisoDatos from '@/shared/components/AvisoDatos.vue'
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseSearchInput from '@/shared/components/BaseSearchInput.vue'

// Anuncio del listado. Va DESPUÉS de los primeros resultados, no antes: quien
// llega a una búsqueda viene a ver productos, y un anuncio por delante de lo
// que buscaba es lo que hace que la gente instale bloqueadores.
const BLOQUE_LISTADO = import.meta.env.VITE_ADSENSE_SLOT_LISTADO ?? ''

const route = useRoute()
const store = useComparadorStore()

// storeToRefs para el estado (mantiene la reactividad al desestructurar);
// las acciones se toman directas del store.
const {
  busqueda,
  categoria,
  orden,
  tiendas,
  tiendasActivas,
  filtrosActivos,
  cargando,
  error,
  actualizadoEn,
  datosDesactualizados,
  productosFiltrados,
  totalResultados,
  hayFiltros,
} = storeToRefs(store)

onMounted(() => store.cargarProductos())

// El buscador de la cabecera entra con ?q=, la portada con ?categoria= y
// el pie con ?tienda=.
watch(
  () => [route.query.q, route.query.categoria, route.query.tienda],
  ([q, cat, tienda]) => {
    busqueda.value = textoDeUrl(q)
    categoria.value = textoDeUrl(cat)

    // soloTienda ya descarta un id que no exista: no hace falta validar aquí.
    if (typeof tienda === 'string') store.soloTienda(tienda)
  },
  { immediate: true },
)
</script>

<template>
  <section class="comparador">
    <header class="comparador__cabecera">
      <h1 class="comparador__titulo">Compara antes de comprar</h1>

      <p class="comparador__bajada">
        El mismo producto en Ripley, Paris, Zara, H&amp;M y Mango, con la tienda
        más barata destacada.
      </p>

      <BaseSearchInput
        v-model="busqueda"
        id="buscar-producto"
        etiqueta="Buscar productos"
        placeholder="Polera, jeans, abrigo…"
      />
    </header>

    <div class="comparador__cuerpo">
      <aside class="comparador__lateral">
        <PanelFiltros />
      </aside>

      <div class="comparador__resultados">
        <AvisoDatos
          :error="error"
          :desactualizado="datosDesactualizados"
          :actualizado-en="actualizadoEn"
          :reintentando="cargando"
          @reintentar="store.cargarProductos({ forzar: true })"
        />


        <!-- carga -->
        <!-- Esqueleto con la forma de las tarjetas: cuando llegan los datos
             ocupan el mismo sitio y la rejilla no se recoloca. -->
        <div v-if="cargando && productosFiltrados.length === 0" class="grilla">
          <ProductoCardSkeleton v-for="n in 6" :key="n" />
        </div>


        <!-- vacío -->
        <div v-else-if="totalResultados === 0" class="aviso">
          <p class="aviso__titulo">No encontramos nada con esos filtros</p>
          <p>Prueba con otra palabra o marca más tiendas.</p>
        </div>

        <!-- resultados -->
        <template v-else>
          <!-- Barra de resultados: cuántos hay, cómo se ordenan y qué filtros
             están puestos, cada uno con su forma de quitarlo. Antes sólo se
             podía "limpiar todo", que obliga a rehacer el resto. -->
        <div class="barra-resultados">
          <p class="mono barra-resultados__conteo">
            {{ totalResultados }}
            {{ totalResultados === 1 ? 'prenda' : 'prendas' }}
          </p>

          <label class="orden">
            <span class="orden__etiqueta mono">Ordenar por</span>
            <select v-model="orden" class="orden__control">
              <option value="barato">Precio: de menor a mayor</option>
              <option value="caro">Precio: de mayor a menor</option>
              <option value="nuevo">Lo más reciente</option>
              <option value="visto">Lo más visto</option>
            </select>
          </label>
        </div>

        <ul v-if="filtrosActivos.length > 0" class="puestos">
          <li v-for="f in filtrosActivos" :key="f.tipo">
            <button
              type="button"
              class="puesto"
              @click="store.quitarFiltro(f.tipo)"
            >
              {{ f.etiqueta }}
              <span aria-hidden="true">✕</span>
              <span class="sr-only">quitar este filtro</span>
            </button>
          </li>
        </ul>


          <div class="grilla">
            <ProductoCard
              v-for="producto in productosFiltrados"
              :key="producto.id"
              :producto="producto"
            />
          </div>

          <AdSlot
            :bloque="BLOQUE_LISTADO"
            :alto="110"
            class="resultados__anuncio"
          />
        </template>
      </div>
    </div>
  </section>
</template>

<style scoped>
.resultados__anuncio {
  margin-top: var(--cep-sp-6);
}

.barra-resultados {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--cep-sp-4);
  flex-wrap: wrap;
  margin-bottom: var(--cep-sp-3);
}
.barra-resultados__conteo {
  margin: 0;
  font-size: var(--cep-fs-sm);
  color: var(--cep-muted);
}

.orden {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-2);
}
.orden__etiqueta {
  font-size: var(--cep-fs-2xs);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--cep-muted);
}
.orden__control {
  min-height: var(--cep-control-h-sm);
  padding: 0 var(--cep-sp-2);
  background: var(--cep-surface);
  color: var(--cep-ink);
  border: 1px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-sm);
  font-size: var(--cep-fs-sm);
  cursor: pointer;
}

/* Filtros puestos: cada uno se quita por su cuenta. */
.puestos {
  list-style: none;
  display: flex;
  flex-wrap: wrap;
  gap: var(--cep-sp-15);
  margin: 0 0 var(--cep-sp-4);
  padding: 0;
}
.puesto {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-15);
  min-height: var(--cep-control-h-sm);
  padding: 0 var(--cep-sp-25);
  background: var(--cep-wash);
  color: var(--cep-ink);
  border: none;
  border-radius: var(--cep-r-pill);
  font-size: var(--cep-fs-sm);
  cursor: pointer;
  transition: background var(--cep-dur-1) var(--cep-ease);
}
.puesto:hover {
  background: var(--cep-wash-2);
}

.comparador__cabecera {
  max-width: 640px;
  margin-bottom: 32px;
}
.comparador__titulo {
  margin: 0 0 6px;
  font-size: 32px;
  line-height: 1.15;
}
.comparador__bajada {
  margin: 0 0 20px;
  color: var(--cep-muted);
}

.comparador__cuerpo {
  display: grid;
  grid-template-columns: 1fr;
  gap: 24px;
  align-items: start;
}
@media (min-width: 900px) {
  .comparador__cuerpo {
    grid-template-columns: 230px 1fr;
  }
  .comparador__lateral {
    position: sticky;
    top: 24px;
  }
}
.comparador__lateral {
  display: flex;
  flex-direction: column;
  gap: 12px;
}



.grilla {
  display: grid;
  grid-template-columns: 1fr;
  gap: 18px;
}
@media (min-width: 620px) {
  .grilla {
    grid-template-columns: repeat(2, 1fr);
  }
}
@media (min-width: 1180px) {
  .grilla {
    grid-template-columns: repeat(3, 1fr);
  }
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
