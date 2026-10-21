<script setup>
import { onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { storeToRefs } from 'pinia'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import { textoDeUrl } from '@/shared/utils/rutas'
import PanelFiltros from '@/modules/comparador/components/PanelFiltros.vue'
import ProductoCard from '@/modules/comparador/components/ProductoCard.vue'
import ProductoCardSkeleton from '@/modules/comparador/components/ProductoCardSkeleton.vue'
import AdSlot from '@/shared/components/AdSlot.vue'
import AvisoDatos from '@/shared/components/AvisoDatos.vue'
import BasePaginacion from '@/shared/components/BasePaginacion.vue'
import BaseSearchInput from '@/shared/components/BaseSearchInput.vue'

// Anuncio del listado. Va DESPUÉS de los primeros resultados, no antes: quien
// llega a una búsqueda viene a ver productos, y un anuncio por delante de lo
// que buscaba es lo que hace que la gente instale bloqueadores.
const BLOQUE_LISTADO = import.meta.env.VITE_ADSENSE_SLOT_LISTADO ?? ''

const route = useRoute()
const store = useComparadorStore()

// Plegado de partida en móvil. En escritorio da igual: el CSS lo enseña
// siempre a partir de 900px, así que no hace falta escuchar el tamaño.
const filtrosAbiertos = ref(false)

// Cómo se ve la grilla: "comoda" (tres por fila, una en el teléfono) o "densa"
// (cuatro, y dos en el teléfono) para recorrer más prendas sin bajar tanto.
// Se recuerda en este navegador; si el almacenamiento está bloqueado, parte
// en cómoda y listo.
const CLAVE_VISTA = 'cep:vista-catalogo'
const vista = ref('comoda')
try {
  if (localStorage.getItem(CLAVE_VISTA) === 'densa') vista.value = 'densa'
} catch { /* sin almacenamiento: vale el valor por defecto */ }
watch(vista, (v) => {
  try { localStorage.setItem(CLAVE_VISTA, v) } catch { /* idem */ }
})

// storeToRefs para el estado (mantiene la reactividad al desestructurar);
// las acciones se toman directas del store.
const {
  busqueda,
  categoria,
  orden,
  paginaActual,
  itemsPorPagina,
  tiendas,
  tiendasActivas,
  filtrosActivos,
  cargando,
  error,
  actualizadoEn,
  datosDesactualizados,
  productosFiltrados,
  productosPagina,
  totalResultados,
  hayFiltros,
} = storeToRefs(store)

// El anuncio entra después de dos filas completas en escritorio (tres
// tarjetas por fila), DENTRO de la página actual. Va como un ítem más de la
// misma rejilla: antes había dos <div class="grilla"> y la segunda reiniciaba
// las filas, así que en vista densa la última fila quedaba con huecos.
const CORTE_ANUNCIO = 6

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
        Reunimos las tiendas disponibles y destacamos dónde conviene comprar
        cada prenda.
      </p>

      <BaseSearchInput
        v-model="busqueda"
        id="buscar-producto"
        etiqueta="Buscar productos"
        placeholder="Polera, jeans, abrigo…"
      />
    </header>

    <div class="comparador__cuerpo">
      <!-- En móvil el panel va plegado detrás de un botón.
           Desplegado ocupaba 3.500px: había que bajar casi cinco pantallas
           enteras antes de ver el primer producto, cuando en escritorio se ven
           a los 323px. Plegar los filtros en pantallas estrechas es lo normal
           precisamente por esto. -->
      <button
        type="button"
        class="filtros-movil"
        :aria-expanded="filtrosAbiertos"
        aria-controls="panel-filtros"
        @click="filtrosAbiertos = !filtrosAbiertos"
      >
        <span>{{ filtrosAbiertos ? 'Ocultar filtros' : 'Filtrar' }}</span>

        <!-- Cuántos filtros hay puestos, para que no se olviden al plegarlos. -->
        <span v-if="filtrosActivos.length > 0" class="filtros-movil__cuenta mono">
          {{ filtrosActivos.length }}
        </span>
      </button>

      <aside
        id="panel-filtros"
        class="comparador__lateral"
        :class="{ 'comparador__lateral--plegado': !filtrosAbiertos }"
      >
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

          <div class="controles">
            <label class="orden">
              <span class="orden__etiqueta mono">Ordenar por</span>
              <span class="orden__caja">
                <select v-model="orden" class="orden__control" aria-label="Ordenar por">
                  <option value="barato">Menor precio</option>
                  <option value="caro">Mayor precio</option>
                  <option value="nuevo">Lo más reciente</option>
                  <option value="visto">Lo más visto</option>
                </select>
              </span>
            </label>

            <!-- Dos botones redondos con el dibujo de la grilla: tres bloques
                 grandes o cuatro chicos. -->
            <div class="vista" role="group" aria-label="Tamaño de las tarjetas">
              <button
                type="button"
                class="vista__boton"
                :aria-pressed="vista === 'comoda'"
                title="Tarjetas grandes"
                @click="vista = 'comoda'"
              >
                <span class="vista__dibujo vista__dibujo--comoda" aria-hidden="true">
                  <i /><i /><i />
                </span>
                <span class="sr-only">Tarjetas grandes</span>
              </button>
              <button
                type="button"
                class="vista__boton"
                :aria-pressed="vista === 'densa'"
                title="Ver más prendas"
                @click="vista = 'densa'"
              >
                <span class="vista__dibujo vista__dibujo--densa" aria-hidden="true">
                  <i /><i /><i /><i />
                </span>
                <span class="sr-only">Ver más prendas</span>
              </button>
            </div>
          </div>
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


          <div class="grilla" :class="{ 'grilla--densa': vista === 'densa' }">
            <template v-for="(producto, i) in productosPagina" :key="producto.id">
              <AdSlot
                v-if="i === CORTE_ANUNCIO"
                :bloque="BLOQUE_LISTADO"
                :alto="110"
                class="grilla__anuncio"
              />

              <ProductoCard
                :producto="producto"
                :densa="vista === 'densa'"
              />
            </template>

            <!-- Si la página no llega a siete prendas, el anuncio va al final
                 en vez de desaparecer: sigue viéndose una sola vez por bloque. -->
            <AdSlot
              v-if="productosPagina.length <= CORTE_ANUNCIO"
              :bloque="BLOQUE_LISTADO"
              :alto="110"
              class="grilla__anuncio"
            />
          </div>

          <BasePaginacion
            class="resultados__paginacion"
            :pagina-actual="paginaActual"
            :items-por-pagina="itemsPorPagina"
            :total-items="totalResultados"
            @update:pagina-actual="store.irAPagina"
            @update:items-por-pagina="store.cambiarItemsPorPagina"
          />
        </template>
      </div>
    </div>
  </section>
</template>

<style scoped>
.grilla__anuncio {
  grid-column: 1 / -1;
}

.resultados__paginacion {
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

.controles {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-3);
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
/* El select se dibuja como las pastillas del sitio, con el mismo chevrón
   que los filtros, en vez de la caja cuadrada del sistema operativo. */
.orden__caja {
  position: relative;
  display: inline-flex;
}
.orden__caja::after {
  content: '';
  position: absolute;
  top: 50%;
  right: 14px;
  width: 6px;
  height: 6px;
  margin-top: -5px;
  border-right: 1.5px solid currentColor;
  border-bottom: 1.5px solid currentColor;
  transform: rotate(45deg);
  pointer-events: none;
}
.orden__control {
  appearance: none;
  min-height: var(--cep-control-h-sm);
  padding: 0 var(--cep-sp-8) 0 var(--cep-sp-3);
  background: var(--cep-surface);
  color: var(--cep-ink);
  border: 1px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-pill);
  font: inherit;
  font-size: var(--cep-fs-sm);
  cursor: pointer;
  transition: border-color var(--cep-dur-1) var(--cep-ease);
}
.orden__control:hover {
  border-color: var(--cep-ink);
}

/* En el teléfono los controles ocupan su propia fila, repartidos a lo ancho,
   y la etiqueta se esconde: el select ya dice lo que hace. */
@media (max-width: 719px) {
  .controles {
    width: 100%;
    justify-content: space-between;
  }
  .orden__etiqueta {
    display: none;
  }
}

.vista {
  display: inline-flex;
  gap: var(--cep-sp-1);
  padding: 3px;
  border: 1px solid var(--cep-line-media);
  border-radius: var(--cep-r-pill);
}
.vista__boton {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 30px;
  height: 30px;
  padding: 0;
  border: none;
  border-radius: 50%;
  background: none;
  color: var(--cep-muted);
  cursor: pointer;
  transition:
    background var(--cep-dur-1) var(--cep-ease),
    color var(--cep-dur-1) var(--cep-ease);
}
.vista__boton:hover {
  color: var(--cep-ink);
}
.vista__boton[aria-pressed='true'] {
  background: var(--cep-ink);
  color: var(--cep-on-ink);
}
/* El dibujo de la grilla, con cuadritos de CSS. */
.vista__dibujo {
  display: grid;
  gap: 2px;
}
.vista__dibujo i {
  display: block;
  border-radius: 1.5px;
  background: currentColor;
}
.vista__dibujo--comoda {
  grid-template-columns: repeat(3, 4px);
}
.vista__dibujo--comoda i {
  height: 12px;
}
.vista__dibujo--densa {
  grid-template-columns: repeat(2, 5px);
}
.vista__dibujo--densa i {
  height: 5px;
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
  grid-template-columns: minmax(0, 1fr);
  gap: 24px;
  align-items: start;
}
.filtros-movil {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--cep-sp-2);
  width: 100%;
  min-height: var(--cep-control-h);
  padding: 0 var(--cep-sp-4);
  background: var(--cep-surface);
  color: var(--cep-ink);
  border: 1.5px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-md);
  font: inherit;
  font-weight: 600;
  cursor: pointer;
}
.filtros-movil__cuenta {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 22px;
  height: 22px;
  padding: 0 6px;
  background: var(--cep-ink);
  color: var(--cep-on-ink);
  border-radius: var(--cep-r-pill);
  font-size: var(--cep-fs-2xs);
}
.comparador__lateral {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* Va DESPUÉS de la regla base a propósito: las dos son de una sola clase, así
   que gana la última. Puesta antes, el `display: flex` de arriba la anulaba y
   el panel no se plegaba nunca. */
.comparador__lateral--plegado {
  display: none;
}
/* En el teléfono el panel aparece deslizándose bajo el botón "Filtrar", en
   vez de saltar de golpe. */
@media (max-width: 899px) {
  .comparador__lateral:not(.comparador__lateral--plegado) {
    animation: desplegar-filtros var(--cep-dur-3) var(--cep-ease);
    /* El panel desplegado hacía crecer la página entera: para llegar a la
       última sección había que bajar por todo el catálogo. Ahora se desplaza
       por dentro y el scroll de la página queda quieto. */
    max-height: calc(100dvh - 140px);
    overflow-x: hidden;
    overflow-y: auto;
    overscroll-behavior: contain;
    padding-right: var(--cep-sp-2);
  }
}
@keyframes desplegar-filtros {
  from {
    opacity: 0;
    transform: translateY(-8px);
  }
}

@media (min-width: 900px) {
  .comparador__cuerpo {
    grid-template-columns: 230px minmax(0, 1fr);
  }
  .comparador__lateral {
    position: sticky;
    top: 84px;
    max-height: calc(100dvh - 100px);
    overflow-x: hidden;
    overflow-y: auto;
    overscroll-behavior: contain;
    padding-right: var(--cep-sp-2);
    /* La barra de desplazamiento no se ve hasta que el mouse entra al panel,
       y aun así delgada y del color de las líneas. Antes había tres a la vista
       (panel, marcas y una horizontal) y se comían la columna. */
    scrollbar-width: thin;
    scrollbar-color: transparent transparent;
    transition: scrollbar-color var(--cep-dur-2) var(--cep-ease);
  }
  .comparador__lateral:hover,
  .comparador__lateral:focus-within {
    scrollbar-color: var(--cep-line-fuerte) transparent;
  }
  .comparador__lateral::-webkit-scrollbar {
    width: 6px;
  }
  .comparador__lateral::-webkit-scrollbar-thumb {
    border-radius: var(--cep-r-pill);
    background: transparent;
  }
  .comparador__lateral:hover::-webkit-scrollbar-thumb {
    background: var(--cep-line-fuerte);
  }
  /* Aquí ya hay columna lateral: el botón sobra y el panel se enseña siempre,
     esté como esté el estado de plegado. El bloque va el último de todos para
     que gane sin necesidad de !important. */
  .filtros-movil {
    display: none;
  }
  .comparador__lateral--plegado {
    display: flex;
  }
}



.grilla {
  display: grid;
  align-items: start;
  grid-template-columns: minmax(0, 1fr);
  gap: 18px;
}
@media (min-width: 620px) {
  .grilla {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
@media (min-width: 1180px) {
  .grilla {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}
/* Densa: dos por fila desde el teléfono, tres con la barra lateral y cuatro
   en pantalla ancha. */
.grilla--densa {
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}
@media (min-width: 900px) {
  .grilla--densa {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}
@media (min-width: 1180px) {
  .grilla--densa {
    grid-template-columns: repeat(4, minmax(0, 1fr));
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