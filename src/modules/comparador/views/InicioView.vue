<script setup>
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import { nombreTienda } from '@/modules/comparador/data/tiendas'
import CategoriaCard from '@/modules/comparador/components/CategoriaCard.vue'
import ProductoCard from '@/modules/comparador/components/ProductoCard.vue'
import RecienteBanner from '@/modules/comparador/components/RecienteBanner.vue'
import ProductoCardSkeleton from '@/modules/comparador/components/ProductoCardSkeleton.vue'
import BaseSkeleton from '@/shared/components/BaseSkeleton.vue'
import OutfitCard from '@/modules/outfits/components/OutfitCard.vue'
import { AUTOMATICAS } from '@/modules/outfits/data/plantillas'
import { useOutfitStore } from '@/modules/outfits/store/outfit.store'
import AdSlot from '@/shared/components/AdSlot.vue'
import AvisoDatos from '@/shared/components/AvisoDatos.vue'
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseCarousel from '@/shared/components/BaseCarousel.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'
import { formatearPrecio } from '@/shared/utils/formato'
import { ahorroMaximo, precioMasBajo } from '@/shared/utils/precios'

const store = useComparadorStore()
const outfit = useOutfitStore()
const router = useRouter()

// Los tres primeros outfits automáticos: la portada es un escaparate, no el
// catálogo entero de plantillas.
const outfitsPortada = AUTOMATICAS.slice(0, 3)

function usarOutfit(prendas) {
  outfit.ponerOutfit(prendas)
  router.push({ name: 'armar' })
}

const {
  productos,
  cargando,
  error,
  actualizadoEn,
  datosDesactualizados,
  destacado,
  comparacionDisponible,
  masRecientes,
  masVistos,
  ofertasDelDia,
  categoriasPopulares,
} = storeToRefs(store)

const visitas = new Intl.NumberFormat('es-CL')

// Ids de los bloques de AdSense. Vacíos hasta crearlos en el panel; mientras
// tanto AdSlot no pinta nada y la portada se ve sin huecos.
const BLOQUE_PORTADA = import.meta.env.VITE_ADSENSE_SLOT_PORTADA ?? ''

onMounted(() => store.cargarProductos())
</script>

<template>
  <div class="inicio">
    <!-- El aviso va ARRIBA del contenido y no en su lugar: la página se sigue
         viendo entera debajo, aunque el backend esté caído. -->
    <AvisoDatos
      :error="error"
      :desactualizado="datosDesactualizados"
      :actualizado-en="actualizadoEn"
      :reintentando="cargando"
      @reintentar="store.cargarProductos({ forzar: true })"
    />

    <!-- ═══ portada ═══ -->
    <section class="hero">
      <div class="hero__texto">
        <p class="eyebrow">Comparador de ropa · Chile</p>

        <template v-if="comparacionDisponible">
          <h1 class="display hero__titulo">
            La tienda dice <span class="rojo">–40%</span>.<br />
            Nosotros te decimos
            <span class="verde">dónde está más barato</span>.
          </h1>

          <p class="hero__bajada">
            Seguimos el precio de la misma prenda, todos los días, en varias
            tiendas. Así el cartel de la vitrina se puede contrastar con lo
            que cobra el resto.
          </p>
        </template>

        <!-- Con una sola fuente de precio la promesa de comparar no se puede
             cumplir, así que no se hace: se ofrece lo que sí hay. -->
        <template v-else>
          <h1 class="display hero__titulo">
            Todo el catálogo,
            <span class="verde">con su precio a la vista</span>.
          </h1>

          <p class="hero__bajada">
            Busca por prenda o por categoría y mira el precio de cada una sin
            dar vueltas por el sitio.
          </p>
        </template>

        <div class="hero__acciones">
          <BaseButton to="/comparador">Ver el comparador</BaseButton>

          <span class="mono muted hero__nota">
            {{ productos.length }} prendas seguidas hoy
          </span>
        </div>
      </div>

      <!-- El ticket de la portada: el desglose que resume de qué va la app -->
      <BaseTicket v-if="destacado && comparacionDisponible" class="hero__ticket">
        <p class="eyebrow">{{ destacado.marca }} · {{ destacado.categoria }}</p>

        <h2 class="display hero__ticket-nombre">{{ destacado.nombre }}</h2>

        <div class="fila">
          <span class="fila__etiqueta">Más barato hoy</span>
          <span class="fila__valor verde">
            {{ formatearPrecio(precioMasBajo(destacado)?.precio) }}
          </span>
        </div>

        <div class="fila">
          <span class="fila__etiqueta">Tienda</span>
          <span class="fila__valor">
            {{ nombreTienda(precioMasBajo(destacado)?.tienda) }}
          </span>
        </div>

        <div class="fila">
          <span class="fila__etiqueta">Comparado con la más cara</span>
          <span class="fila__valor rojo">
            +{{ formatearPrecio(ahorroMaximo(destacado)) }}
          </span>
        </div>

        <div class="divisor" />

        <div class="hero__ticket-pie">
          <p class="mono muted hero__ticket-glosa">
            Lo que te ahorras eligiendo bien
          </p>

          <span class="insignia verde-borde">
            AHORRAS {{ formatearPrecio(ahorroMaximo(destacado)) }}
          </span>
        </div>
      </BaseTicket>
    </section>

    <!-- ═══ anuncio, entre la portada y el contenido ═══ -->
    <AdSlot :bloque="BLOQUE_PORTADA" :alto="96" />

    <!-- ═══ lo más reciente ═══ -->
    <section class="seccion">
      <header class="seccion__cabecera">
        <h2 class="seccion-titulo">Lo más reciente</h2>
        <p class="mono muted seccion__nota">Las últimas prendas que subimos</p>
      </header>

      <BaseCarousel v-if="masRecientes.length > 0">
        <RecienteBanner
          v-for="(p, i) in masRecientes"
          :key="p.id"
          :producto="p"
          :indice="i"
        />
      </BaseCarousel>

      <div v-else-if="cargando" class="carrusel-hueso">
        <ProductoCardSkeleton v-for="n in 4" :key="n" compacta />
      </div>

      <p v-else class="mono muted seccion__vacia">
        Todavía no hay prendas nuevas que mostrar.
      </p>
    </section>

    <!-- ═══ outfits listos ═══ -->
    <section class="seccion">
      <header class="seccion__cabecera">
        <h2 class="seccion-titulo">Vístete completo por menos</h2>
        <p class="mono muted seccion__nota">
          Conjuntos armados con lo más barato de cada tipo
        </p>
      </header>

      <div class="outfits">
        <OutfitCard
          v-for="plantilla in outfitsPortada"
          :key="plantilla.id"
          :plantilla="plantilla"
          @usar="usarOutfit"
        />
      </div>
    </section>

    <!-- ═══ lo más visto ═══ -->
    <section class="seccion">
      <header class="seccion__cabecera">
        <h2 class="seccion-titulo">Lo más visto</h2>
        <p class="mono muted seccion__nota">Lo que más se está comparando</p>
      </header>

      <BaseCarousel v-if="masVistos.length > 0">
        <ProductoCard
          v-for="p in masVistos"
          :key="p.id"
          :producto="p"
          compacta
          :insignia="`${visitas.format(p.vistas ?? 0)} visitas`"
        />
      </BaseCarousel>

      <div v-else-if="cargando" class="carrusel-hueso">
        <ProductoCardSkeleton v-for="n in 4" :key="n" compacta />
      </div>

      <p v-else class="mono muted seccion__vacia">
        Aún no tenemos suficientes visitas para este ranking.
      </p>
    </section>

    <!-- ═══ categorías populares ═══ -->
    <section class="seccion">
      <header class="seccion__cabecera">
        <h2 class="seccion-titulo">Categorías populares</h2>
      </header>

      <div v-if="categoriasPopulares.length > 0" class="categorias">
        <CategoriaCard
          v-for="(c, i) in categoriasPopulares"
          :key="c.categoria"
          :categoria="c.categoria"
          :cantidad="c.cantidad"
          :indice="i"
        />
      </div>

      <div v-else-if="cargando" class="categorias">
        <BaseSkeleton v-for="n in 3" :key="n" :alto="148" radio="var(--cep-r-lg)" />
      </div>

      <p v-else class="mono muted seccion__vacia">
        Las categorías aparecerán cuando carguen las prendas.
      </p>
    </section>

    <!-- ═══ ofertas del día ═══ -->
    <!-- Se oculta si no hay nada que comparar: una sección "ofertas" vacía
         no informa, sólo deja un hueco raro en la portada. -->
    <section v-if="ofertasDelDia.length > 0 || cargando" class="seccion">
      <header class="seccion__cabecera">
        <h2 class="seccion-titulo">Ofertas del día</h2>
        <p class="mono muted seccion__nota">
          Donde más cambia el precio de una tienda a otra
        </p>
      </header>

      <BaseCarousel v-if="ofertasDelDia.length > 0">
        <ProductoCard
          v-for="o in ofertasDelDia"
          :key="o.producto.id"
          :producto="o.producto"
          compacta
          :insignia="`Ahorra ${formatearPrecio(o.ahorro)}`"
        />
      </BaseCarousel>

      <div v-else class="carrusel-hueso">
        <ProductoCardSkeleton v-for="n in 4" :key="n" compacta />
      </div>
    </section>

  </div>
</template>

<style scoped>
.inicio {
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-12);
}

/* ——— portada ——— */
.hero {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--cep-sp-8);
  align-items: center;
}
@media (min-width: 900px) {
  .hero {
    grid-template-columns: 1.1fr 0.9fr;
  }
}
.hero__titulo {
  margin: var(--cep-sp-15) 0 var(--cep-sp-3);
  font-size: var(--cep-fs-3xl);
}
@media (min-width: 720px) {
  .hero__titulo {
    font-size: var(--cep-fs-4xl);
  }
}
.hero__bajada {
  margin: 0;
  max-width: 48ch;
  font-size: var(--cep-fs-base);
  color: var(--cep-muted);
}
.hero__aviso {
  margin: var(--cep-sp-4) 0 0;
  padding: var(--cep-sp-25) var(--cep-sp-3);
  max-width: 48ch;
  background: var(--cep-surface);
  border-left: 3px solid var(--cep-rate);
  border-radius: var(--cep-r-sm);
  font-size: var(--cep-fs-sm);
  line-height: var(--cep-lh-snug);
}

.hero__acciones {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-3);
  flex-wrap: wrap;
  margin-top: var(--cep-sp-5);
}
.hero__nota {
  font-size: var(--cep-fs-xs);
}

.hero__ticket {
  justify-self: center;
  width: 100%;
  max-width: 360px;
}
.hero__ticket-nombre {
  margin: var(--cep-sp-05) 0 var(--cep-sp-3);
  font-size: var(--cep-fs-lg);
}
.hero__ticket-pie {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--cep-sp-3);
}
.hero__ticket-glosa {
  margin: 0;
  max-width: 150px;
  font-size: var(--cep-fs-2xs);
  line-height: var(--cep-lh-snug);
}

/* ——— secciones ——— */
.seccion__cabecera {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--cep-sp-3);
  flex-wrap: wrap;
}
.seccion__nota {
  margin: 0 0 var(--cep-sp-3);
  font-size: var(--cep-fs-xs);
}

.outfits {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--cep-sp-4);
}
@media (min-width: 620px) {
  .outfits {
    grid-template-columns: repeat(3, 1fr);
  }
}

.categorias {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--cep-sp-4);
}
@media (min-width: 720px) {
  .categorias {
    grid-template-columns: repeat(2, 1fr);
  }
}
@media (min-width: 1024px) {
  .categorias {
    grid-template-columns: repeat(3, 1fr);
  }
}

/* ——— color ——— */
.verde {
  color: var(--cep-exito);
}
.rojo {
  color: var(--cep-alerta);
}
.verde-borde {
  color: var(--cep-exito);
  border-color: var(--cep-exito);
}

.carrusel-hueso {
  display: flex;
  gap: var(--cep-sp-4);
  overflow: hidden;
  padding: 2px 2px var(--cep-sp-15);
}
.seccion__vacia {
  margin: 0;
  padding: var(--cep-sp-6) 0;
  font-size: var(--cep-fs-sm);
}

.aviso {
  padding: var(--cep-sp-16) var(--cep-sp-5);
  text-align: center;
  color: var(--cep-muted);
}
.aviso__titulo {
  margin: 0 0 var(--cep-sp-1);
  font-size: var(--cep-fs-lg);
  font-weight: 600;
  color: var(--cep-alerta);
}
.aviso .btn {
  margin-top: var(--cep-sp-4);
}
</style>
