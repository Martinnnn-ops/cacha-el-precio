<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import BaseButton from '@/shared/components/BaseButton.vue'
import { formatearPrecio } from '@/shared/utils/formato'
import * as api from '@/modules/admin/services/mensajeria.service'

// «Seguir un barrido» (PLAN-EFT §3, paso 4). Cada barrido lleva un número de
// seguimiento (correlationId) que viaja en cada mensaje: SQS, Kinesis, SNS.
// Cada servicio anota cuántas cosas hizo con ese número, y el gateway lo junta.
// Aquí se dibuja como una línea de tiempo, etapa por etapa.

const barridos = ref([])
const elegido = ref(null)
const traza = ref(null)
const error = ref('')
const cargando = ref(false)
const copiado = ref(false)
let reloj = null

const ESTADOS = {
  encolado: { texto: 'En cola', clase: 'activo' },
  'en curso': { texto: 'En curso', clase: 'activo' },
  ok: { texto: 'Terminado', clase: 'ok' },
  error: { texto: 'Falló', clase: 'mal' },
}
const estado = (e) => ESTADOS[e] ?? { texto: e ?? '—', clase: 'neutro' }

const hora = (iso) =>
  iso
    ? new Intl.DateTimeFormat('es-CL', {
        day: '2-digit',
        month: 'short',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
      }).format(new Date(iso))
    : '—'

function cuenta(etapa) {
  return (traza.value?.etapas ?? []).find((e) => e.etapa === etapa) ?? null
}
const n = (etapa) => cuenta(etapa)?.cantidad ?? 0

// Las etapas en el orden en que las recorre un precio. Cada una dice qué
// servicio la hizo, por qué pieza de AWS pasó y qué significa un cero.
const pasos = computed(() => {
  const t = traza.value
  if (!t) return []
  const s = t.scraper
  const sospechosos = n('cambio-sospechoso')
  const duplicados = n('duplicado-ignorado')
  const repetidos = n('aviso-repetido-saltado')

  return [
    {
      servicio: 'scraper-service',
      via: 'timer cep-barrido',
      titulo: s ? `Barrido de ${s.tienda}: ${estado(s.estado).texto.toLowerCase()}` : 'Barrido',
      detalle: s
        ? `${s.urls ?? '?'} fichas pedidas · ${s.guardados ?? '…'} guardadas · ${s.enviados ?? '…'} enviadas` +
          (s.segundos ? ` · ${Math.round(s.segundos)} s` : '')
        : 'El scraper ya no lo tiene en memoria (se reinició), pero sus huellas siguen abajo.',
      cuando: s?.inicio ?? s?.encolado,
      estado: s ? (s.estado === 'ok' ? 'ok' : s.estado === 'error' ? 'mal' : 'activo') : 'no',
    },
    {
      servicio: 'product-service',
      via: 'SQS cep-ofertas-crudas',
      titulo: `${n('oferta-guardada')} ofertas guardadas en el catálogo`,
      detalle: 'Cada oferta viajó en un mensaje con este número; el consumidor la guardó y confirmó (ACK).',
      cuando: cuenta('oferta-guardada')?.ultimaVez,
      estado: n('oferta-guardada') ? 'ok' : 'no',
    },
    {
      servicio: 'product-service',
      via: 'Kinesis cep-precios',
      titulo: `${n('precio-procesado')} precios leídos del stream`,
      detalle: 'El lector sumó cada precio al historial y al precio habitual de su oferta.',
      cuando: cuenta('precio-procesado')?.ultimaVez,
      estado: n('precio-procesado') ? 'ok' : 'no',
    },
    {
      servicio: 'product-service',
      via: 'SNS cep-cambios-oferta',
      titulo: `${n('cambio-publicado')} cambios publicados`,
      detalle: n('cambio-publicado')
        ? 'Bajas de precio, agotados o reposiciones: SNS los reparte a las colas según sus reglas.'
        : 'Ningún precio bajó ni se agotó nada: no había cambios que publicar.',
      cuando: cuenta('cambio-publicado')?.ultimaVez,
      estado: n('cambio-publicado') ? 'ok' : 'no',
    },
    {
      servicio: 'product-service',
      via: 'SQS cep-auditoria-precios',
      titulo:
        `${n('cambio-auditado')} cambios auditados` +
        (sospechosos ? ` · ${sospechosos} 🚩 sospechosos` : '') +
        (duplicados ? ` · ${duplicados} duplicados ignorados` : ''),
      detalle: duplicados
        ? 'SQS entregó algún mensaje dos veces: se reconoció por su eventId y se guardó una sola vez.'
        : 'Cada cambio queda registrado una sola vez por su eventId (idempotencia).',
      cuando: cuenta('cambio-auditado')?.ultimaVez,
      estado: n('cambio-auditado') ? (sospechosos ? 'alerta' : 'ok') : 'no',
    },
    {
      servicio: 'notification-service',
      via: 'SQS cep-notificaciones',
      titulo: `${n('aviso-enviado')} avisos enviados` + (n('cambio-recibido') ? ` de ${n('cambio-recibido')} cambios recibidos` : ''),
      detalle: repetidos
        ? `${repetidos} avisos ya se habían enviado antes y se saltaron (idempotencia por eventId + persona).`
        : 'Solo llegan las bajas de 10% o más, los agotados y las reposiciones, y solo a quien sigue la prenda.',
      cuando: cuenta('aviso-enviado')?.ultimaVez ?? cuenta('cambio-recibido')?.ultimaVez,
      estado: n('aviso-enviado') ? 'ok' : 'no',
    },
  ]
})

const comando = computed(() =>
  traza.value ? `docker compose logs --no-log-prefix | grep '${traza.value.correlationId}'` : '',
)

async function cargarBarridos() {
  try {
    barridos.value = await api.listarBarridos()
    error.value = ''
    if (!elegido.value && barridos.value.length) await elegir(barridos.value[0].correlationId)
  } catch (e) {
    error.value = api.leerError(e).mensaje
  }
}

async function elegir(id) {
  elegido.value = id
  cargando.value = true
  try {
    traza.value = await api.verTraza(id)
    error.value = ''
  } catch (e) {
    error.value = api.leerError(e).mensaje
  } finally {
    cargando.value = false
  }
}

async function refrescar() {
  await cargarBarridos()
  if (elegido.value) await elegir(elegido.value)
}

async function copiar() {
  try {
    await navigator.clipboard.writeText(comando.value)
    copiado.value = true
    setTimeout(() => (copiado.value = false), 1500)
  } catch {
    // Sin permiso de portapapeles: el comando sigue a la vista para copiarlo a mano.
  }
}

// Mientras el barrido elegido siga en curso, se refresca solo cada 10 s.
onMounted(async () => {
  await cargarBarridos()
  reloj = setInterval(() => {
    const e = traza.value?.scraper?.estado
    if (e === 'encolado' || e === 'en curso') refrescar()
  }, 10000)
})
onBeforeUnmount(() => clearInterval(reloj))
</script>

<template>
  <section class="seguir">
    <h2>Seguir un barrido</h2>
    <p class="muted">
      Cada barrido lleva un <strong>número de seguimiento</strong> (correlationId) que viaja dentro de cada
      mensaje: SQS, Kinesis y SNS. Cada servicio anota qué hizo con ese número; aquí se ve el recorrido
      completo.
    </p>

    <p v-if="error" role="alert" class="error">{{ error }}</p>

    <div class="cuerpo">
      <div class="lista">
        <div class="lista__cabeza">
          <strong>Últimos barridos</strong>
          <BaseButton variante="texto" tamano="chico" @click="refrescar">Actualizar</BaseButton>
        </div>
        <p v-if="!barridos.length && !error" class="muted">Todavía no hay barridos con número de seguimiento.</p>
        <button
          v-for="b in barridos"
          :key="b.correlationId"
          type="button"
          class="barrido"
          :class="{ elegido: b.correlationId === elegido }"
          @click="elegir(b.correlationId)"
        >
          <span class="barrido__tienda">{{ b.tienda }}</span>
          <span class="mono muted">{{ hora(b.inicio ?? b.encolado) }}</span>
          <span class="chip" :class="estado(b.estado).clase">{{ estado(b.estado).texto }}</span>
        </button>
      </div>

      <div v-if="traza" class="detalle" :aria-busy="cargando">
        <p class="mono id">{{ traza.correlationId }}</p>
        <p v-if="traza.faltan?.length" class="muted">
          Sin respuesta de {{ traza.faltan.join(', ') }}: su parte se muestra vacía.
        </p>

        <ol class="linea">
          <li v-for="(p, i) in pasos" :key="i" :class="p.estado">
            <span class="linea__punto" aria-hidden="true" />
            <div>
              <p class="linea__titulo">{{ p.titulo }}</p>
              <p class="mono muted linea__via">{{ p.servicio }} · {{ p.via }} · {{ hora(p.cuando) }}</p>
              <p class="linea__detalle">{{ p.detalle }}</p>
            </div>
          </li>
        </ol>

        <template v-if="traza.auditoria?.length">
          <h3>Cambios auditados de este barrido</h3>
          <table class="auditoria">
            <thead>
              <tr><th>Cambio</th><th>Tienda</th><th>Prenda</th><th>Antes → después</th><th /></tr>
            </thead>
            <tbody>
              <tr v-for="e in traza.auditoria" :key="e.eventId" :class="{ sospechoso: e.sospechoso }">
                <td>{{ e.tipo }}</td>
                <td>{{ e.store }}</td>
                <td>{{ e.nombre ?? e.slug ?? `#${e.productId}` }}</td>
                <td class="mono">
                  {{ e.precioAnterior ? formatearPrecio(e.precioAnterior) : '—' }} →
                  {{ e.precioNuevo ? formatearPrecio(e.precioNuevo) : '—' }}
                  <template v-if="e.descuentoPct"> (−{{ e.descuentoPct }}%)</template>
                </td>
                <td>{{ e.sospechoso ? `🚩 ${e.motivo}` : '' }}</td>
              </tr>
            </tbody>
          </table>
        </template>

        <h3>Buscarlo en los logs</h3>
        <p class="muted">Todos los servicios escriben sus logs en JSON con este número. En la EC2:</p>
        <div class="comando">
          <code>{{ comando }}</code>
          <BaseButton variante="secundario" tamano="chico" @click="copiar">
            {{ copiado ? 'Copiado' : 'Copiar' }}
          </BaseButton>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.cuerpo {
  display: grid;
  gap: var(--cep-sp-5);
  margin-top: var(--cep-sp-4);
}
@media (min-width: 900px) {
  .cuerpo {
    grid-template-columns: 260px minmax(0, 1fr);
  }
}
.lista {
  display: grid;
  gap: var(--cep-sp-2);
  align-content: start;
}
.lista__cabeza {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.barrido {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 2px var(--cep-sp-2);
  padding: var(--cep-sp-2) var(--cep-sp-3);
  border: 1px solid var(--cep-line);
  border-radius: var(--cep-r-md);
  background: none;
  font: inherit;
  text-align: left;
  cursor: pointer;
}
.barrido.elegido {
  border-color: var(--cep-accent);
}
.barrido__tienda {
  font-weight: 600;
  text-transform: capitalize;
}
.barrido .mono {
  font-size: var(--cep-fs-xs);
}
.barrido .chip {
  grid-row: 1 / span 2;
  grid-column: 2;
  align-self: center;
}
.chip {
  padding: 2px 8px;
  border-radius: 999px;
  font-size: var(--cep-fs-xs);
  border: 1px solid var(--cep-line-fuerte);
}
.chip.ok {
  border-color: var(--cep-exito);
  color: var(--cep-exito);
}
.chip.activo {
  border-color: var(--cep-accent);
  color: var(--cep-accent);
}
.chip.mal {
  border-color: var(--cep-alerta);
  color: var(--cep-alerta);
}
.id {
  margin: 0 0 var(--cep-sp-3);
  font-size: var(--cep-fs-sm);
}

.linea {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
}
.linea li {
  position: relative;
  display: grid;
  grid-template-columns: 22px minmax(0, 1fr);
  gap: var(--cep-sp-3);
  padding-bottom: var(--cep-sp-4);
}
/* la línea que une los puntos */
.linea li:not(:last-child)::before {
  content: '';
  position: absolute;
  left: 10px;
  top: 18px;
  bottom: 0;
  width: 2px;
  background: var(--cep-line);
}
.linea__punto {
  width: 14px;
  height: 14px;
  margin: 3px 0 0 4px;
  border-radius: 50%;
  border: 2px solid var(--cep-line-fuerte);
  background: var(--cep-surface);
}
.linea .ok .linea__punto {
  border-color: var(--cep-exito);
  background: var(--cep-exito);
}
.linea .activo .linea__punto {
  border-color: var(--cep-accent);
}
.linea .alerta .linea__punto,
.linea .mal .linea__punto {
  border-color: var(--cep-alerta);
  background: var(--cep-alerta);
}
.linea .no .linea__titulo {
  color: var(--cep-muted);
}
.linea p {
  margin: 0;
}
.linea__titulo {
  font-weight: 600;
}
.linea__via {
  font-size: var(--cep-fs-xs);
}
.linea__detalle {
  font-size: var(--cep-fs-sm);
  margin-top: 2px !important;
}

h3 {
  margin: var(--cep-sp-4) 0 var(--cep-sp-2);
  font-size: var(--cep-fs-md);
}
.auditoria {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--cep-fs-sm);
}
.auditoria th,
.auditoria td {
  text-align: left;
  padding: var(--cep-sp-15, 6px) var(--cep-sp-2);
  border-bottom: 1px solid var(--cep-line);
}
.auditoria .sospechoso {
  background: color-mix(in srgb, var(--cep-alerta) 8%, transparent);
}
.comando {
  display: flex;
  gap: var(--cep-sp-2);
  align-items: center;
  padding: var(--cep-sp-2) var(--cep-sp-3);
  border-radius: var(--cep-r-md);
  background: var(--cep-sunken);
  overflow-x: auto;
}
.comando code {
  flex: 1;
  white-space: nowrap;
  font-size: var(--cep-fs-sm);
}
.error {
  color: var(--cep-alerta);
}
</style>
