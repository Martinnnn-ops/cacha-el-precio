<script setup>
import { computed, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'

import BaseDropdown from '@/shared/components/BaseDropdown.vue'
import BaseSearchInput from '@/shared/components/BaseSearchInput.vue'
import BrandMark from '@/shared/components/BrandMark.vue'
import { textoDeUrl } from '@/shared/utils/rutas'

// Cabecera de la aplicación.
//
// Las tiendas y la sesión llegan por props/eventos y no se importan de un
// módulo: `shared/` no puede depender de `modules/` (si lo hiciera, borrar el
// comparador rompería la cabecera). Quien las conoce es App.vue.
const props = defineProps({
  tiendas: { type: Array, default: () => [] },
  tema: { type: String, default: 'sistema' },
  temas: { type: Array, default: () => ['sistema', 'claro', 'oscuro'] },
  usuario: { type: Object, default: null },
})

const seleccion = defineModel('tiendasActivas', {
  type: Array,
  default: () => [],
})

const emit = defineEmits(['cambiar-tema', 'cerrar-sesion'])

const route = useRoute()
const router = useRouter()

const consulta = ref(textoDeUrl(route.query.q))

// Si se llega con ?q= desde un enlace compartido, la caja no puede salir vacía.
watch(
  () => route.query.q,
  (q) => {
    consulta.value = textoDeUrl(q)
  },
)

function buscar(texto) {
  const limpio = textoDeUrl(texto)

  router.push(
    limpio
      ? { name: 'comparador', query: { q: limpio } }
      : { name: 'comparador' },
  )
}

const todasMarcadas = computed(
  () => seleccion.value.length === props.tiendas.length,
)

const resumenTiendas = computed(() =>
  todasMarcadas.value
    ? 'Todas'
    : `${seleccion.value.length} de ${props.tiendas.length}`,
)

function alternarTienda(id) {
  seleccion.value = seleccion.value.includes(id)
    ? seleccion.value.filter((t) => t !== id)
    : [...seleccion.value, id]
}

const ETIQUETA_TEMA = {
  sistema: 'Como el sistema',
  claro: 'Claro',
  oscuro: 'Oscuro',
}

const iniciales = computed(() => {
  const n = props.usuario?.nombre ?? props.usuario?.correo ?? ''

  return n.slice(0, 2).toUpperCase() || '?'
})
</script>

<template>
  <header class="barra">
    <a class="barra__salto" href="#contenido">Saltar al contenido</a>

    <div class="barra__interior">
      <!-- ——— marca ——— -->
      <RouterLink
        class="barra__marca"
        :to="{ name: 'inicio' }"
        aria-label="Cacha el Precio — ir al inicio"
      >
        <BrandMark :tamano="30" />
        <span class="barra__nombre display">cacha<em>°</em> el precio</span>
      </RouterLink>

      <!-- ——— buscador, al centro ——— -->
      <div class="barra__buscador">
        <BaseSearchInput
          v-model="consulta"
          id="buscar-cabecera"
          etiqueta="Buscar prendas"
          placeholder="Buscar polera, jeans, abrigo…"
          @buscar="buscar"
        />
      </div>

      <nav class="barra__acciones" aria-label="Principal">
        <RouterLink class="barra__enlace" :to="{ name: 'comparador' }">
          Comparador
        </RouterLink>

        <RouterLink class="barra__enlace" :to="{ name: 'armar' }">
          Armar outfit
        </RouterLink>

        <RouterLink class="barra__enlace" :to="{ name: 'comparar' }">
          Comparar
        </RouterLink>

        <!-- ——— tiendas ——— -->
        <BaseDropdown
          v-if="tiendas.length > 1"
          etiqueta="Elegir tiendas"
          alineado="derecha"
          :ancho="250"
        >
          <template #disparador>
            <svg viewBox="0 0 24 24" width="17" height="17" fill="none" aria-hidden="true">
              <path d="M3 9h18l-1.5-4.5A2 2 0 0 0 17.6 3H6.4a2 2 0 0 0-1.9 1.5L3 9Zm0 0v10a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V9M3 9a3 3 0 0 0 6 0 3 3 0 0 0 6 0 3 3 0 0 0 6 0" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round" />
            </svg>
            <span class="barra__resumen mono">{{ resumenTiendas }}</span>
          </template>

          <template #default>
            <p class="menu__titulo">Tiendas que quiero ver</p>

            <label
              v-for="tienda in tiendas"
              :key="tienda.id"
              class="menu__opcion"
            >
              <input
                type="checkbox"
                :checked="seleccion.includes(tienda.id)"
                @change="alternarTienda(tienda.id)"
              />
              <span class="menu__punto" :style="{ background: tienda.color }" />
              <span>{{ tienda.nombre }}</span>
            </label>

            <p v-if="seleccion.length === 0" class="menu__aviso">
              Sin ninguna marcada no hay nada que comparar.
            </p>

            <button
              v-if="!todasMarcadas"
              type="button"
              class="menu__accion"
              @click="seleccion = tiendas.map((t) => t.id)"
            >
              Marcar todas
            </button>
          </template>
        </BaseDropdown>

        <!-- ——— tema ——— -->
        <BaseDropdown etiqueta="Configuración" alineado="derecha" :ancho="220">
          <template #disparador>
            <svg viewBox="0 0 24 24" width="17" height="17" fill="none" aria-hidden="true">
              <circle cx="12" cy="12" r="3.2" stroke="currentColor" stroke-width="1.8" />
              <path d="M12 2.5v2.2M12 19.3v2.2M4.2 4.2l1.6 1.6M18.2 18.2l1.6 1.6M2.5 12h2.2M19.3 12h2.2M4.2 19.8l1.6-1.6M18.2 5.8l1.6-1.6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
            </svg>
          </template>

          <template #default>
            <p class="menu__titulo">Apariencia</p>

            <button
              v-for="valor in temas"
              :key="valor"
              type="button"
              class="menu__opcion menu__opcion--boton"
              :class="{ 'menu__opcion--activa': tema === valor }"
              :aria-pressed="tema === valor"
              @click="emit('cambiar-tema', valor)"
            >
              <span class="menu__marca" aria-hidden="true">
                {{ tema === valor ? '✓' : '' }}
              </span>
              {{ ETIQUETA_TEMA[valor] }}
            </button>
          </template>
        </BaseDropdown>

        <!-- ——— cuenta ——— -->
        <BaseDropdown etiqueta="Mi cuenta" alineado="derecha" :ancho="210">
          <template #disparador>
            <img
              v-if="usuario?.avatar"
              class="barra__foto"
              :src="usuario.avatar"
              alt=""
              referrerpolicy="no-referrer"
            />

            <span v-else-if="usuario" class="barra__avatar mono">
              {{ iniciales }}
            </span>

            <svg v-else viewBox="0 0 24 24" width="17" height="17" fill="none" aria-hidden="true">
              <circle cx="12" cy="8.2" r="3.7" stroke="currentColor" stroke-width="1.8" />
              <path d="M4.5 20.2a7.5 7.5 0 0 1 15 0" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
            </svg>
          </template>

          <template #default="{ cerrar }">
            <template v-if="usuario">
              <p class="menu__titulo">{{ usuario.nombre ?? usuario.correo }}</p>

              <button
                type="button"
                class="menu__opcion menu__opcion--boton"
                @click="emit('cerrar-sesion'); cerrar()"
              >
                Cerrar sesión
              </button>
            </template>

            <template v-else>
              <p class="menu__titulo">Mi cuenta</p>

              <RouterLink
                class="menu__opcion menu__opcion--boton"
                :to="{ name: 'entrar' }"
                @click="cerrar()"
              >
                Entrar con Google
              </RouterLink>

              <RouterLink
                class="menu__opcion menu__opcion--boton"
                :to="{ name: 'registro' }"
                @click="cerrar()"
              >
                Crear cuenta
              </RouterLink>
            </template>
          </template>
        </BaseDropdown>
      </nav>
    </div>
  </header>
</template>

<style scoped>
/* Franja de acento cruzando la parte de arriba. Es el truco más barato que
   hay para que una interfaz sobria no parezca sin terminar: un rectángulo de
   color no exige talento gráfico y da carácter a toda la página. */
.barra {
  position: sticky;
  top: 0;
  z-index: var(--cep-z-nav);
  background: var(--cep-bg);
  border-top: 3px solid var(--cep-accent);
  border-bottom: 1px solid var(--cep-line);
}

.barra__salto {
  position: absolute;
  left: var(--cep-sp-3);
  top: -80px;
  z-index: var(--cep-z-skip, 60);
  padding: var(--cep-sp-25) var(--cep-sp-4);
  background: var(--cep-ink);
  color: var(--cep-on-ink);
  border-radius: var(--cep-r-lg);
  text-decoration: none;
  transition: top var(--cep-dur-2) var(--cep-ease);
}
.barra__salto:focus {
  top: var(--cep-sp-3);
}

.barra__interior {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-4);
  width: 100%;
  max-width: var(--cep-container);
  margin: 0 auto;
  padding: var(--cep-sp-25) var(--cep-gutter);
}

.barra__marca {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-2);
  flex: none;
  color: var(--cep-ink);
  font-weight: 700;
  font-size: var(--cep-fs-lg);
  text-decoration: none;
}
.barra__marca em {
  font-style: normal;
  color: var(--cep-accent);
}

/* El buscador ocupa el centro y se lleva el espacio sobrante. */
.barra__buscador {
  flex: 1 1 auto;
  min-width: 0;
  max-width: 460px;
  margin: 0 auto;
}

.barra__acciones {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-1);
  flex: none;
}
.barra__enlace {
  padding: var(--cep-sp-15) var(--cep-sp-3);
  border-radius: var(--cep-r-sm);
  color: var(--cep-muted);
  font-size: var(--cep-fs-sm);
  font-weight: 600;
  text-decoration: none;
  transition:
    background var(--cep-dur-1) var(--cep-ease),
    color var(--cep-dur-1) var(--cep-ease);
}
.barra__enlace:hover {
  background: var(--cep-wash);
  color: var(--cep-ink);
}
/* El activo lleva su propio borde de acento: se distingue de un simple hover
   —que también pinta fondo— y se lee de un vistazo dónde estás. */
.barra__enlace {
  border-bottom: 2px solid transparent;
  border-radius: var(--cep-r-sm) var(--cep-r-sm) 0 0;
}
.barra__enlace.router-link-active {
  color: var(--cep-ink);
  border-bottom-color: var(--cep-accent);
  background: transparent;
}

.barra__resumen {
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}

.barra__foto {
  width: 26px;
  height: 26px;
  border-radius: 50%;
  object-fit: cover;
}
.barra__avatar {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 50%;
  background: var(--cep-accent);
  color: var(--cep-on-accent, #fff);
  font-size: var(--cep-fs-2xs);
  font-weight: 600;
}

/* ——— contenido de los menús ——— */
.menu__titulo {
  margin: 0 0 var(--cep-sp-1);
  padding: 0 var(--cep-sp-2);
  font-family: var(--cep-font-mono);
  font-size: var(--cep-fs-2xs);
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--cep-muted);
}
.menu__opcion {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-2);
  width: 100%;
  min-height: var(--cep-control-h-sm);
  padding: var(--cep-sp-1) var(--cep-sp-2);
  border: none;
  border-radius: var(--cep-r-sm);
  background: none;
  color: var(--cep-ink);
  font-size: var(--cep-fs-sm);
  text-align: left;
  text-decoration: none;
  cursor: pointer;
  transition: background var(--cep-dur-1) var(--cep-ease);
}
.menu__opcion:hover {
  background: var(--cep-wash);
}
.menu__opcion input {
  width: 16px;
  height: 16px;
  accent-color: var(--cep-accent);
  cursor: pointer;
}
.menu__opcion--activa {
  color: var(--cep-accent);
  font-weight: 600;
}
.menu__marca {
  width: 14px;
  flex: none;
  color: var(--cep-accent);
}
.menu__punto {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  flex: none;
}
.menu__aviso {
  margin: var(--cep-sp-2) var(--cep-sp-2) 0;
  font-size: var(--cep-fs-xs);
  line-height: var(--cep-lh-snug);
  color: var(--cep-alerta);
}
.menu__accion {
  width: 100%;
  margin-top: var(--cep-sp-1);
  padding: var(--cep-sp-15) var(--cep-sp-2);
  border: none;
  border-radius: var(--cep-r-sm);
  background: none;
  color: var(--cep-accent);
  font-size: var(--cep-fs-xs);
  font-weight: 600;
  text-align: left;
  cursor: pointer;
}
.menu__accion:hover {
  background: var(--cep-wash);
}

/* ——— responsive ——— */
/* Bajo 900px el buscador no cabe en la misma fila: se va a la suya, completa. */
@media (max-width: 899px) {
  .barra__interior {
    flex-wrap: wrap;
    gap: var(--cep-sp-2);
  }
  .barra__buscador {
    order: 3;
    flex: 1 1 100%;
    max-width: none;
    margin: 0 0 var(--cep-sp-1);
  }
  .barra__acciones {
    margin-left: auto;
  }
}
@media (max-width: 479px) {
  .barra__nombre,
  .barra__enlace,
  .barra__resumen {
    display: none;
  }
}
</style>
