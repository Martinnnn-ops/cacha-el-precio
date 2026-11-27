<script setup>
import { computed } from 'vue'
import { useTiendas } from '@/modules/comparador/composables/useTiendas'
import { formatearPrecio } from '@/shared/utils/formato'

// ¿El precio de hoy es bueno para ESTA prenda? Lo responde el rango habitual
// que calcula el lector de Kinesis en product-service (promedio ± una
// desviación, con al menos 5 lecturas). Cómo se calcula se explica en el panel
// de administración (Mensajería › Flujo 3), no aquí: a quien compra no le sirve.

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
</style>
