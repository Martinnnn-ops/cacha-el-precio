<script setup>
import { computed, ref } from 'vue'
import { useTiendas } from '@/modules/comparador/composables/useTiendas'
import { formatearPrecio } from '@/shared/utils/formato'

// ¿El precio de hoy es bueno para ESTA prenda? Lo responde el rango habitual
// que calcula el lector de Kinesis en product-service (promedio ± una
// desviación, con al menos 5 lecturas). Debajo, a un clic, de dónde sale el
// dato: el recorrido del precio desde la tienda hasta esta ficha.

const props = defineProps({
  ofertas: { type: Array, default: () => [] },
  historial: { type: Array, default: () => [] },
})

const { nombreTienda, colorTienda } = useTiendas()

// Cuántas lecturas pide el backend para opinar (EstadisticasPrecioHabitual).
const MUESTRAS_MINIMAS = 5

const conHabitual = computed(() =>
  props.ofertas.filter((o) => o.habitual && typeof o.precio === 'number'),
)

// La oferta de referencia: la más barata con stock que ya tenga rango; si
// ninguna lo tiene, la que lleve más lecturas.
const referencia = computed(() => {
  const lista = conHabitual.value
  if (lista.length === 0) return null

  const listas = lista.filter((o) => o.stock && o.habitual.suficiente)
  if (listas.length > 0) {
    return listas.reduce((a, b) => (b.precio < a.precio ? b : a))
  }

  return lista.reduce((a, b) => (b.habitual.muestras > a.habitual.muestras ? b : a))
})

const VEREDICTOS = {
  bajo: { titulo: 'Bajo lo habitual', clase: 'bajo', frase: 'Buen momento para comprar.' },
  dentro: { titulo: 'Precio normal', clase: 'dentro', frase: 'Está en lo que suele costar.' },
  sobre: { titulo: 'Sobre lo habitual', clase: 'sobre', frase: 'Suele estar más barato: si puedes, espera.' },
}

const veredicto = computed(() => {
  const r = referencia.value
  if (!r || !r.habitual.suficiente) return null
  return VEREDICTOS[r.habitual.ubicacion] ?? null
})

// La barra: una escala que abarca el rango y el precio de hoy, con margen.
const barra = computed(() => {
  const r = referencia.value
  if (!r || !r.habitual.suficiente) return null

  const { piso, techo } = r.habitual
  const desde = Math.min(piso, r.precio)
  const hasta = Math.max(techo, r.precio)
  const margen = Math.max((hasta - desde) * 0.15, hasta * 0.03, 1)
  const a = desde - margen
  const b = hasta + margen
  const pos = (v) => ((v - a) / (b - a)) * 100

  return {
    izq: pos(piso),
    ancho: Math.max(pos(techo) - pos(piso), 1.5),
    hoy: pos(r.precio),
  }
})

// Las otras tiendas, en una línea: "Paris · normal", "Hites · juntando 2/5".
const otras = computed(() =>
  conHabitual.value
    .filter((o) => o !== referencia.value)
    .map((o) => ({
      tienda: o.tienda,
      texto: o.habitual.suficiente
        ? (VEREDICTOS[o.habitual.ubicacion]?.titulo ?? '').toLowerCase()
        : `juntando historial ${Math.min(o.habitual.muestras, MUESTRAS_MINIMAS)}/${MUESTRAS_MINIMAS}`,
    })),
)

const abierto = ref(false)

const ultimaLectura = computed(() => {
  const iso = referencia.value?.habitual?.ultimaCaptura
  if (!iso) return null
  const fecha = new Date(iso)
  if (Number.isNaN(fecha.getTime())) return null
  return new Intl.DateTimeFormat('es-CL', {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  }).format(fecha)
})

// El recorrido, con los números reales de esta prenda donde los hay.
const pasos = computed(() => {
  const r = referencia.value
  const tienda = r ? nombreTienda(r.tienda) : 'la tienda'
  const muestras = r?.habitual?.muestras ?? 0

  return [
    {
      nombre: tienda,
      detalle: 'publica el precio en su sitio',
    },
    {
      nombre: 'Scraper',
      detalle: 'lo lee cada 3 horas',
    },
    {
      nombre: 'Kinesis',
      detalle: 'stream cep-precios: cada lectura es un registro',
      destacado: true,
    },
    {
      nombre: 'Lector',
      detalle: `product-service suma la lectura: ${muestras} hasta ahora`,
    },
    {
      nombre: 'Esta ficha',
      detalle: props.historial.length
        ? `${props.historial.length} días en el gráfico`
        : 'muestra el rango y el gráfico',
    },
  ]
})
</script>

<template>
  <div v-if="referencia" class="habitual">
    <!-- veredicto con rango -->
    <div v-if="veredicto" class="habitual__veredicto" :class="`habitual--${veredicto.clase}`">
      <p class="habitual__titulo">
        <span class="habitual__punto" aria-hidden="true" />
        {{ veredicto.titulo }}
        <span class="muted habitual__frase">{{ veredicto.frase }}</span>
      </p>

      <p class="habitual__texto">
        En <b>{{ nombreTienda(referencia.tienda) }}</b> esta prenda suele costar entre
        <b class="mono">{{ formatearPrecio(referencia.habitual.piso) }}</b> y
        <b class="mono">{{ formatearPrecio(referencia.habitual.techo) }}</b>. Hoy está a
        <b class="mono">{{ formatearPrecio(referencia.precio) }}</b>.
      </p>

      <div
        class="barra"
        role="img"
        :aria-label="`Rango habitual de ${formatearPrecio(referencia.habitual.piso)} a ${formatearPrecio(referencia.habitual.techo)}; hoy ${formatearPrecio(referencia.precio)}`"
      >
        <span
          class="barra__rango"
          :style="{ left: `${barra.izq}%`, width: `${barra.ancho}%`, background: colorTienda(referencia.tienda) }"
        />
        <span class="barra__hoy" :style="{ left: `${barra.hoy}%` }">
          <em class="mono">hoy</em>
        </span>
      </div>
    </div>

    <!-- todavía sin rango: se dice cuánto falta -->
    <div v-else class="habitual__veredicto habitual--juntando">
      <p class="habitual__titulo">
        Juntando historial
        <span class="habitual__progreso" aria-hidden="true">
          <i
            v-for="n in MUESTRAS_MINIMAS"
            :key="n"
            :class="{ lleno: n <= referencia.habitual.muestras }"
          />
        </span>
        <span class="mono muted">{{ Math.min(referencia.habitual.muestras, MUESTRAS_MINIMAS) }}/{{ MUESTRAS_MINIMAS }}</span>
      </p>
      <p class="habitual__texto muted">
        Revisamos el precio de {{ nombreTienda(referencia.tienda) }} cada 3 horas. Con
        {{ MUESTRAS_MINIMAS }} lecturas te decimos si hoy está barato o caro para esta prenda.
      </p>
    </div>

    <p v-if="otras.length" class="mono muted habitual__otras">
      <span v-for="o in otras" :key="o.tienda">
        <i :style="{ background: colorTienda(o.tienda) }" />{{ nombreTienda(o.tienda) }} · {{ o.texto }}
      </span>
    </p>

    <!-- de dónde sale: a un clic, sin tooltips -->
    <button
      type="button"
      class="habitual__boton"
      :aria-expanded="abierto"
      @click="abierto = !abierto"
    >
      {{ abierto ? '▾' : '▸' }} ¿De dónde sale este dato?
    </button>

    <div v-if="abierto" class="origen">
      <ol class="flujo">
        <li
          v-for="(p, i) in pasos"
          :key="p.nombre"
          class="flujo__paso"
          :class="{ 'flujo__paso--kinesis': p.destacado }"
        >
          <b>{{ p.nombre }}</b>
          <span class="muted">{{ p.detalle }}</span>
          <span v-if="i < pasos.length - 1" class="flujo__flecha" aria-hidden="true">→</span>
        </li>
      </ol>

      <p v-if="ultimaLectura" class="mono muted origen__ultima">
        Última lectura de {{ nombreTienda(referencia.tienda) }}: {{ ultimaLectura }}
      </p>

      <h3 class="origen__titulo">¿Por qué pasa por Kinesis?</h3>
      <ul class="origen__razones">
        <li>
          <b>Llegan en orden.</b> Todas las lecturas de una misma oferta van al mismo
          carril del stream (la clave es tienda + producto), así que el promedio se
          calcula en el orden en que se capturaron.
        </li>
        <li>
          <b>No se pierden.</b> El stream guarda las lecturas 24 horas. Si el lector se
          cae, retoma desde su marcador (que se guarda junto con el cálculo) y no salta
          ni cuenta dos veces ninguna.
        </li>
        <li>
          <b>Las pueden leer varios.</b> Hoy las lee el cálculo del precio habitual; una
          alerta de bajada o una auditoría podrían leer las mismas lecturas sin tocar el
          scraper. Una cola, en cambio, le entrega cada mensaje a un solo lector.
        </li>
      </ul>
    </div>
  </div>
</template>

<style scoped>
.habitual {
  display: grid;
  gap: var(--cep-sp-3);
  margin-bottom: var(--cep-sp-5);
}
.habitual__veredicto {
  padding: var(--cep-sp-3) var(--cep-sp-4);
  border: 1px solid var(--cep-line);
  border-left: 4px solid var(--tono, var(--cep-line-fuerte));
  border-radius: var(--cep-r-sm);
  background: var(--cep-sunken);
}
.habitual--bajo {
  --tono: var(--cep-exito);
}
.habitual--sobre {
  --tono: var(--cep-alerta);
}
.habitual--dentro {
  --tono: var(--cep-muted);
}
.habitual__titulo {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--cep-sp-2);
  margin: 0;
  font-weight: 600;
}
.habitual__punto {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--tono, var(--cep-muted));
}
.habitual__frase {
  font-weight: 400;
  font-size: var(--cep-fs-sm);
}
.habitual__texto {
  margin: var(--cep-sp-2) 0 0;
  font-size: var(--cep-fs-sm);
  line-height: var(--cep-lh-snug);
}

.barra {
  position: relative;
  height: 10px;
  margin: var(--cep-sp-4) 0 var(--cep-sp-3);
  border-radius: 5px;
  background: var(--cep-line);
}
.barra__rango {
  position: absolute;
  top: 0;
  bottom: 0;
  border-radius: 5px;
  opacity: 0.55;
}
.barra__hoy {
  position: absolute;
  top: -4px;
  width: 3px;
  height: 18px;
  margin-left: -1.5px;
  border-radius: 2px;
  background: var(--tono, var(--cep-muted));
}
.barra__hoy em {
  position: absolute;
  top: 20px;
  left: 50%;
  transform: translateX(-50%);
  font-style: normal;
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}

.habitual__progreso {
  display: inline-flex;
  gap: 4px;
}
.habitual__progreso i {
  width: 14px;
  height: 6px;
  border-radius: 3px;
  background: var(--cep-line);
}
.habitual__progreso i.lleno {
  background: var(--cep-line-fuerte);
}

.habitual__otras {
  display: flex;
  flex-wrap: wrap;
  gap: var(--cep-sp-15) var(--cep-sp-4);
  margin: 0;
  font-size: var(--cep-fs-xs);
}
.habitual__otras i {
  display: inline-block;
  width: 8px;
  height: 8px;
  margin-right: 6px;
  border-radius: 50%;
}

.habitual__boton {
  justify-self: start;
  padding: 0;
  border: 0;
  background: none;
  font: inherit;
  font-size: var(--cep-fs-sm);
  color: var(--cep-muted);
  cursor: pointer;
  text-decoration: underline;
  text-underline-offset: 3px;
}
.habitual__boton:hover,
.habitual__boton:focus-visible {
  color: inherit;
}

.origen {
  padding: var(--cep-sp-4);
  border: 1px dashed var(--cep-line-fuerte);
  border-radius: var(--cep-r-sm);
}
.flujo {
  display: flex;
  flex-wrap: wrap;
  gap: var(--cep-sp-2) var(--cep-sp-5);
  margin: 0;
  padding: 0;
  list-style: none;
}
.flujo__paso {
  position: relative;
  display: grid;
  gap: 2px;
  min-width: 110px;
  max-width: 170px;
  font-size: var(--cep-fs-sm);
}
.flujo__paso span {
  font-size: var(--cep-fs-xs);
  line-height: var(--cep-lh-snug);
}
.flujo__paso--kinesis b {
  color: var(--cep-exito);
}
.flujo__flecha {
  position: absolute;
  top: 0;
  right: calc(-1 * var(--cep-sp-4));
  color: var(--cep-muted);
}
.origen__ultima {
  margin: var(--cep-sp-3) 0 0;
  font-size: var(--cep-fs-xs);
}
.origen__titulo {
  margin: var(--cep-sp-4) 0 var(--cep-sp-2);
  font-size: var(--cep-fs-sm);
}
.origen__razones {
  display: grid;
  gap: var(--cep-sp-2);
  margin: 0;
  padding-left: var(--cep-sp-4);
  font-size: var(--cep-fs-sm);
  line-height: var(--cep-lh-snug);
}
</style>
