<script setup>
import { computed } from 'vue'
import { RouterLink } from 'vue-router'

import BrandMark from '@/shared/components/BrandMark.vue'
import { CIUDAD, CONTACTO, MISION, NOMBRE } from '@/shared/config/sitio'
import { tiempoRelativo } from '@/shared/utils/formato'

// Pie de la aplicación.
//
// Las tiendas y las categorías llegan por props y no se importan de
// `modules/comparador`: `shared/` no puede depender de un módulo (si lo
// hiciera, borrar el comparador rompería el pie). Quien las conoce es el
// layout, que es el punto de composición.
const props = defineProps({
  tiendas: { type: Array, default: () => [] },
  categorias: { type: Array, default: () => [] },
  totalProductos: { type: Number, default: 0 },
  actualizadoEn: { type: Number, default: null },
})

const anio = computed(() => new Date().getFullYear())

// Cuánto seguimos y desde cuándo. Es la línea que ningún otro comparador pone,
// y es la que respalda lo que promete la misión: si decimos que medimos a
// diario, lo mínimo es enseñar cuándo fue la última vez.
const cobertura = computed(() => {
  if (props.totalProductos === 0) return ''

  const prendas = `${props.totalProductos} ${props.totalProductos === 1 ? 'prenda' : 'prendas'}`
  const donde =
    props.tiendas.length > 1 ? ` en ${props.tiendas.length} tiendas` : ''
  const cuando = props.actualizadoEn ? ` · precios de ${tiempoRelativo(props.actualizadoEn)}` : ''

  return `Seguimos ${prendas}${donde}${cuando}`
})
</script>

<template>
  <footer class="pie">
    <div class="pie__interior">
      <!-- ——— quiénes somos ——— -->
      <div class="pie__marca">
        <RouterLink class="pie__logo" :to="{ name: 'inicio' }">
          <BrandMark :tamano="26" />
          <span class="display">cacha<em>°</em> el precio</span>
        </RouterLink>

        <p class="pie__mision">{{ MISION }}</p>

        <p v-if="cobertura" class="mono pie__cobertura">{{ cobertura }}</p>
      </div>

      <!-- ——— navega ——— -->
      <nav class="pie__columna" aria-labelledby="pie-navega">
        <h2 id="pie-navega" class="pie__titulo">Navega</h2>

        <ul class="pie__lista">
          <li>
            <RouterLink class="pie__enlace" :to="{ name: 'armar' }">
              Armar un outfit
            </RouterLink>
          </li>
          <li>
            <RouterLink class="pie__enlace" :to="{ name: 'outfits' }">
              Outfits listos
            </RouterLink>
          </li>
          <li>
            <RouterLink class="pie__enlace" :to="{ name: 'comparador' }">
              Todo el catálogo
            </RouterLink>
          </li>

          <li v-for="categoria in categorias" :key="categoria">
            <RouterLink
              class="pie__enlace"
              :to="{ name: 'comparador', query: { categoria } }"
            >
              {{ categoria }}
            </RouterLink>
          </li>
        </ul>
      </nav>

      <!-- ——— tiendas ——— -->
      <nav
        v-if="tiendas.length > 1"
        class="pie__columna"
        aria-labelledby="pie-tiendas"
      >
        <h2 id="pie-tiendas" class="pie__titulo">Tiendas</h2>

        <ul class="pie__lista">
          <li v-for="tienda in tiendas" :key="tienda.id">
            <!-- Cada una entra al comparador ya filtrada por esa tienda. -->
            <RouterLink
              class="pie__enlace pie__enlace--tienda"
              :to="{ name: 'comparador', query: { tienda: tienda.id } }"
            >
              <span class="pie__punto" :style="{ background: tienda.color }" />
              {{ tienda.nombre }}
            </RouterLink>
          </li>
        </ul>
      </nav>

      <!-- ——— legal ——— -->
      <nav class="pie__columna" aria-labelledby="pie-legal">
        <h2 id="pie-legal" class="pie__titulo">Legal</h2>

        <ul class="pie__lista">
          <li>
            <RouterLink class="pie__enlace" :to="{ name: 'terminos' }">
              Términos y condiciones
            </RouterLink>
          </li>
          <li>
            <RouterLink class="pie__enlace" :to="{ name: 'preguntas' }">
              Preguntas frecuentes
            </RouterLink>
          </li>
          <li>
            <RouterLink class="pie__enlace" :to="{ name: 'privacidad' }">
              Política de privacidad
            </RouterLink>
          </li>
        </ul>
      </nav>

      <!-- ——— contacto ——— -->
      <div class="pie__columna">
        <h2 class="pie__titulo">Contacto</h2>

        <a class="pie__enlace pie__correo" :href="`mailto:${CONTACTO}`">
          {{ CONTACTO }}
        </a>

        <p class="pie__nota">
          ¿Un precio mal puesto o dos prendas mezcladas en una? Avísanos y lo
          corregimos.
        </p>
      </div>
    </div>

    <!-- ——— avisos y cierre ——— -->
    <div class="pie__interior pie__legal">
      <p class="pie__aviso">
        Somos independientes: ninguna de las tiendas que comparamos nos posee ni
        nos patrocina, y ninguna decide el orden de los resultados, que siempre
        va de más barato a más caro. Sí podemos recibir una comisión si compras
        a través de nuestros enlaces, y eso no cambia el precio que pagas.
      </p>

      <p class="pie__aviso">
        Los precios se recogen del catálogo público de cada tienda una vez al
        día y pueden cambiar sin aviso: el válido es siempre el que la tienda
        muestra al pagar.
      </p>
      <p class="pie__copyright mono">
        {{ NOMBRE }} {{ anio }} · Todos los derechos reservados ·
        {{ CIUDAD }}
      </p>
    </div>
  </footer>
</template>

<style scoped>
.pie {
  margin-top: var(--cep-sp-16);
  background: var(--cep-sunken);
  /* El fondo distinto ya separa el pie del contenido: no hace falta además un
     borde. Menos líneas, menos ruido. */
}

.pie__interior {
  width: 100%;
  max-width: var(--cep-container);
  margin: 0 auto;
  padding: 0 var(--cep-gutter);
}

/* La columna de marca ocupa más porque lleva la misión; el resto reparten. */
.pie__interior:first-child {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: var(--cep-sp-8);
  padding-top: var(--cep-sp-10);
  padding-bottom: var(--cep-sp-8);
}
@media (min-width: 720px) {
  .pie__interior:first-child {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
@media (min-width: 1024px) {
  .pie__interior:first-child {
    grid-template-columns: minmax(0, 1.7fr) minmax(0, 1fr) minmax(0, 1fr) minmax(0, 1.2fr) minmax(0, 1.2fr);
    gap: var(--cep-sp-6);
  }
}

/* ——— marca ——— */
.pie__logo {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-2);
  color: var(--cep-ink);
  font-weight: 700;
  font-size: var(--cep-fs-base);
  text-decoration: none;
}
.pie__logo em {
  font-style: normal;
  color: var(--cep-accent);
}
.pie__mision {
  margin: var(--cep-sp-3) 0 0;
  max-width: 40ch;
  font-size: var(--cep-fs-sm);
  color: var(--cep-muted);
}
.pie__cobertura {
  margin: var(--cep-sp-3) 0 0;
  padding-top: var(--cep-sp-3);
  border-top: 1px solid var(--cep-line);
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}

/* ——— columnas ——— */
.pie__titulo {
  margin: 0 0 var(--cep-sp-3);
  font-family: var(--cep-font-mono);
  font-size: var(--cep-fs-2xs);
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--cep-muted);
}
.pie__lista {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-05);
}
.pie__enlace {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-2);
  /* Alto cómodo para el dedo, sin separar visualmente los enlaces. */
  min-height: var(--cep-control-h-sm);
  margin-left: calc(var(--cep-sp-2) * -1);
  padding: 0 var(--cep-sp-2);
  border-radius: var(--cep-r-sm);
  color: var(--cep-ink);
  font-size: var(--cep-fs-sm);
  text-decoration: none;
  transition:
    background var(--cep-dur-1) var(--cep-ease),
    color var(--cep-dur-1) var(--cep-ease);
}
.pie__enlace:hover {
  background: var(--cep-wash);
  color: var(--cep-accent);
}
.pie__correo {
  color: var(--cep-accent);
  font-weight: 600;
  word-break: break-word;
}
.pie__punto {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex: none;
}
.pie__nota {
  margin: var(--cep-sp-2) 0 0;
  font-size: var(--cep-fs-sm);
  color: var(--cep-muted);
}

/* ——— cierre ——— */
.pie__legal {
  padding-top: var(--cep-sp-5);
  padding-bottom: var(--cep-sp-10);
  border-top: 1px solid var(--cep-line-media);
}
.pie__aviso {
  margin: 0 0 var(--cep-sp-4);
  max-width: 78ch;
  font-size: var(--cep-fs-xs);
  line-height: var(--cep-lh-snug);
  color: var(--cep-muted);
}
.pie__copyright {
  margin: 0;
  font-size: var(--cep-fs-2xs);
  color: var(--cep-muted);
}
</style>
