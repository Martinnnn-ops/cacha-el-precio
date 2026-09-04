<script setup>
import { computed, ref, watch } from 'vue'

import { useTiendas } from '@/modules/comparador/composables/useTiendas'
import { formatearFecha, formatearPrecio } from '@/shared/utils/formato'

// Gráfico de historial de precios, en SVG propio (sin librerías).
//
// La versión anterior dibujaba las líneas y las fechas pero NO el eje de
// precios: se veía la forma de la curva y no cuánto costaba. Un historial de
// precios donde no se pueden leer los precios es medio gráfico.
const props = defineProps({
  historial: { type: Array, default: () => [] },
})

const { nombreTienda, colorTienda } = useTiendas()

const ANCHO = 640
const ALTO = 240
const PAD_IZQ = 58
const PAD_DER = 16
const PAD_ARR = 16
const PAD_ABA = 32
const AREA_ANCHO = ANCHO - PAD_IZQ - PAD_DER
const AREA_ALTO = ALTO - PAD_ARR - PAD_ABA

// Paso "redondo" (1, 2 o 5 × 10^n) para que las guías caigan en cifras que
// alguien diría en voz alta, no en 87.333.
function pasoRedondo(rango, objetivo) {
  const bruto = rango / objetivo
  const magnitud = 10 ** Math.floor(Math.log10(bruto))
  const n = bruto / magnitud
  const factor = n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10

  return factor * magnitud
}

// En el eje no cabe "$104.990" cinco veces.
function etiquetaEje(valor) {
  return valor >= 1000 ? `$${Math.round(valor / 1000)}k` : `$${valor}`
}

const grafico = computed(() => {
  const dias = props.historial

  if (dias.length < 2) return null

  const valores = dias.flatMap((d) => Object.values(d.precios ?? {}))

  if (valores.length === 0) return null

  const min = Math.min(...valores)
  const max = Math.max(...valores)
  // Si todo vale lo mismo el rango es 0: se abre un margen para no dividir
  // entre cero y para que la línea no quede pegada al borde.
  const bruto = max - min || Math.max(1, Math.round(max * 0.1))
  const paso = pasoRedondo(bruto, 4)
  const desde = Math.floor(min / paso) * paso
  const hasta = Math.ceil((max || paso) / paso) * paso
  const rango = hasta - desde || paso

  const x = (i) =>
    dias.length === 1
      ? PAD_IZQ + AREA_ANCHO / 2
      : PAD_IZQ + (i / (dias.length - 1)) * AREA_ANCHO
  const y = (v) => PAD_ARR + AREA_ALTO - ((v - desde) / rango) * AREA_ALTO

  const guias = []

  for (let v = desde; v <= hasta + 0.5; v += paso) {
    guias.push({ valor: v, y: y(v), etiqueta: etiquetaEje(v) })
  }

  // Como mucho 6 fechas: con más se pisan entre ellas.
  const cada = Math.ceil(dias.length / 6)
  const fechas = dias
    // "14 ago" en vez de "2026-08-14": en un eje, la fecha ISO ocupa el triple
    // y no se lee de un vistazo.
    .map((d, i) => ({ i, texto: formatearFecha(d.fecha) || d.fecha, x: x(i) }))
    .filter((f) => f.i % cada === 0 || f.i === dias.length - 1)

  const ids = [...new Set(dias.flatMap((d) => Object.keys(d.precios ?? {})))]

  const series = ids.map((id) => {
    // Los días sin dato de esa tienda se saltan, no se dibujan como 0.
    const puntos = dias
      .map((d, i) => ({ precio: d.precios?.[id], i }))
      .filter((p) => typeof p.precio === 'number')
      .map((p) => ({ ...p, x: x(p.i), y: y(p.precio) }))

    return {
      id,
      nombre: nombreTienda(id),
      color: colorTienda(id),
      puntos,
      linea: puntos.map((p) => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' '),
    }
  })

  // Mínimo global: primera aparición, para no mover la marca si se repite.
  let iMin = 0
  let vMin = Infinity

  dias.forEach((d, i) => {
    Object.values(d.precios ?? {}).forEach((precio) => {
      if (precio < vMin) {
        vMin = precio
        iMin = i
      }
    })
  })

  return {
    dias,
    series,
    guias,
    fechas,
    x,
    y,
    iMin,
    vMin,
    anchoColumna: AREA_ANCHO / Math.max(1, dias.length),
  }
})

// Día consultado. Sin interacción se muestra el último, que es lo que casi
// siempre se quiere ver.
const activo = ref(null)

watch(() => props.historial, () => {
  activo.value = null
})

const indiceLeido = computed(() => {
  const g = grafico.value

  if (!g) return 0

  const ultimo = g.dias.length - 1

  return activo.value === null
    ? ultimo
    : Math.min(Math.max(activo.value, 0), ultimo)
})

// Precio de cada tienda ese día, y cuánto cambió desde la última vez que esa
// tienda sí tuvo dato.
const lectura = computed(() => {
  const g = grafico.value

  if (!g) return null

  const i = indiceLeido.value
  const dia = g.dias[i]

  if (!dia) return null

  return {
    fecha: dia.fecha,
    tiendas: g.series.map((serie) => {
      const precio = dia.precios?.[serie.id] ?? null
      let cambio = null

      if (precio !== null) {
        for (let j = i - 1; j >= 0; j--) {
          const antes = g.dias[j]?.precios?.[serie.id]

          if (antes !== undefined) {
            cambio = precio - antes
            break
          }
        }
      }

      return { ...serie, precio, cambio }
    }),
  }
})

function mover(paso) {
  const g = grafico.value

  if (!g) return

  activo.value = Math.min(
    Math.max(indiceLeido.value + paso, 0),
    g.dias.length - 1,
  )
}

defineExpose({ grafico })
</script>

<template>
  <div v-if="grafico" class="grafico">
    <svg
      :viewBox="`0 0 ${ANCHO} ${ALTO}`"
      class="grafico__svg"
      tabindex="0"
      role="img"
      :aria-label="`Historial de precios. Día ${lectura?.fecha ?? ''} seleccionado.`"
      @mouseleave="activo = null"
      @blur="activo = null"
      @keydown.left.prevent="mover(-1)"
      @keydown.right.prevent="mover(1)"
      @keydown.home.prevent="activo = 0"
      @keydown.end.prevent="activo = grafico.dias.length - 1"
    >
      <!-- guías y escala de precios -->
      <g class="grafico__guias">
        <template v-for="g in grafico.guias" :key="g.valor">
          <line :x1="PAD_IZQ" :x2="ANCHO - PAD_DER" :y1="g.y" :y2="g.y" />
          <text :x="PAD_IZQ - 10" :y="g.y + 4" text-anchor="end">
            {{ g.etiqueta }}
          </text>
        </template>
      </g>

      <!-- guía vertical del día leído -->
      <line
        class="grafico__cursor"
        :x1="grafico.x(indiceLeido)"
        :x2="grafico.x(indiceLeido)"
        :y1="PAD_ARR"
        :y2="PAD_ARR + AREA_ALTO"
      />

      <g v-for="serie in grafico.series" :key="serie.id">
        <polyline
          :points="serie.linea"
          fill="none"
          :stroke="serie.color"
          stroke-width="2"
          stroke-linejoin="round"
          stroke-linecap="round"
        />

        <circle
          v-for="p in serie.puntos"
          :key="p.i"
          :cx="p.x"
          :cy="p.y"
          :r="p.i === indiceLeido ? 5 : 3"
          :fill="serie.color"
          stroke="var(--cep-surface)"
          stroke-width="1.5"
        />
      </g>

      <!-- el mínimo, rotulado -->
      <circle
        :cx="grafico.x(grafico.iMin)"
        :cy="grafico.y(grafico.vMin)"
        r="6.5"
        fill="none"
        stroke="var(--cep-exito)"
        stroke-width="2"
      />

      <!-- fechas -->
      <text
        v-for="f in grafico.fechas"
        :key="f.i"
        class="grafico__fecha"
        :x="f.x"
        :y="ALTO - 8"
        text-anchor="middle"
      >
        {{ f.texto }}
      </text>

      <!-- zonas de hover, una por día -->
      <rect
        v-for="(d, i) in grafico.dias"
        :key="d.fecha"
        :x="grafico.x(i) - grafico.anchoColumna / 2"
        :y="PAD_ARR"
        :width="grafico.anchoColumna"
        :height="AREA_ALTO"
        fill="transparent"
        @mouseenter="activo = i"
      />
    </svg>

    <div v-if="lectura" class="lectura" aria-live="polite">
      <span class="mono lectura__fecha">
        {{ formatearFecha(lectura.fecha) || lectura.fecha }}
      </span>

      <span v-for="t in lectura.tiendas" :key="t.id" class="lectura__item">
        <i :style="{ background: t.color }" />
        {{ t.nombre }}

        <b v-if="t.precio !== null" class="mono">
          {{ formatearPrecio(t.precio) }}
        </b>
        <b v-else class="mono muted">sin dato</b>

        <em
          v-if="t.cambio"
          class="mono"
          :class="t.cambio < 0 ? 'lectura__baja' : 'lectura__sube'"
        >
          {{ t.cambio < 0 ? '▼' : '▲' }} {{ formatearPrecio(Math.abs(t.cambio)) }}
        </em>
      </span>
    </div>

    <p class="mono muted grafico__pista">
      Pasa el cursor —o usa ← → con el teclado— para ver el precio de cada día.
      El círculo verde marca el más bajo registrado.
    </p>
  </div>

  <!-- Sin historial se dice, en vez de esconder la sección entera y dejar a
       alguien preguntándose si el gráfico no cargó. -->
  <p v-else class="muted grafico__vacio">
    Todavía no tenemos historial de precios de esta prenda. Lo iremos
    construyendo con las lecturas de cada día.
  </p>
</template>

<style scoped>
.grafico__svg {
  width: 100%;
  height: auto;
  display: block;
  overflow: visible;
}
.grafico__guias line {
  stroke: var(--cep-line);
  stroke-width: 1;
}
.grafico__guias text,
.grafico__fecha {
  font-family: var(--cep-font-mono);
  font-size: 10.5px;
  fill: var(--cep-muted);
}
.grafico__cursor {
  stroke: var(--cep-line-fuerte);
  stroke-width: 1;
  stroke-dasharray: 3 3;
}

.lectura {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--cep-sp-15) var(--cep-sp-4);
  margin-top: var(--cep-sp-3);
  padding: var(--cep-sp-25) var(--cep-sp-3);
  background: var(--cep-sunken);
  border-radius: var(--cep-r-sm);
}
.lectura__fecha {
  font-size: var(--cep-fs-xs);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--cep-muted);
}
.lectura__item {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-15);
  font-size: var(--cep-fs-sm);
}
.lectura__item i {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  flex: none;
}
.lectura__item em {
  font-style: normal;
  font-size: var(--cep-fs-xs);
}
.lectura__baja {
  color: var(--cep-exito);
}
.lectura__sube {
  color: var(--cep-alerta);
}

.grafico__pista {
  margin: var(--cep-sp-2) 0 0;
  font-size: var(--cep-fs-xs);
  line-height: var(--cep-lh-snug);
}
.grafico__vacio {
  margin: 0;
  font-size: var(--cep-fs-sm);
}
</style>
