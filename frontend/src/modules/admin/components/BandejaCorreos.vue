<script setup>
import { onMounted, ref, watch } from 'vue'
import BaseButton from '@/shared/components/BaseButton.vue'
import * as api from '@/modules/admin/services/mensajeria.service'

// Los correos que generó notification-service (flujo 2), tal como los recibe
// quien sigue la prenda. En el lab el servicio corre con CORREO_MODO=log y el
// correo no sale a Gmail: esta bandeja es la forma de verlo sin abrir los logs.
const props = defineProps({
  // El panel lo cambia en cada refresco: así la lista se actualiza con él.
  actualizado: { type: Date, default: null },
})

const TIPOS = {
  'precio.bajo': 'Bajó de precio',
  'oferta.agotada': 'Se agotó',
  'oferta.repuesta': 'Volvió a estar disponible',
}
const MODOS = {
  log: { texto: 'Solo registrado', clase: 'neutro' },
  smtp: { texto: 'Enviado por Gmail', clase: 'ok' },
  ejemplo: { texto: 'Ejemplo, no se envía', clase: 'neutro' },
}

const correos = ref([])
const error = ref('')
const abierto = ref(null) // el correo que se está mirando, con su HTML
const vista = ref('html')
const errorAbierto = ref('')

async function cargar() {
  try {
    correos.value = await api.listarCorreos()
    error.value = ''
  } catch (e) {
    error.value = api.leerError(e).mensaje
  }
}

async function abrir(cargarCorreo) {
  errorAbierto.value = ''
  vista.value = 'html'
  try {
    abierto.value = await cargarCorreo()
  } catch (e) {
    abierto.value = null
    errorAbierto.value = api.leerError(e).mensaje
  }
}

const verCorreo = (id) => abrir(() => api.verCorreo(id))
const verEjemplo = (tipo) => abrir(() => api.verEjemplo(tipo))

onMounted(cargar)
watch(() => props.actualizado, cargar)

// Al entrar se abre el aviso real más nuevo, si hay: es lo primero que se quiere ver.
watch(correos, (lista, antes) => {
  if (!abierto.value && !antes?.length && lista.length) verCorreo(lista[0].id)
})

const fecha = (iso) =>
  new Date(iso).toLocaleString('es-CL', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
</script>

<template>
  <section class="bandeja">
    <h2>Correos generados</h2>
    <p class="muted">
      Cuando una prenda que alguien sigue baja de precio, se agota o vuelve, <code>notification-service</code> le arma
      un correo. En este ambiente corre con <code>CORREO_MODO=log</code>: el correo se arma completo pero
      <strong>no sale a Gmail</strong>; queda guardado acá tal como le llegaría a la persona.
    </p>

    <div class="cuerpo" :class="{ conVista: abierto }">
      <div class="listas">
        <h3>Avisos reales <span class="muted">· recorrieron el flujo 2 completo</span></h3>
        <p class="muted chico">tópico → regla → <code>cep-notificaciones</code> → notification-service → correo</p>

        <p v-if="error" role="alert" class="error">{{ error }}</p>
        <p v-else-if="!correos.length" class="vacio muted">
          Todavía no hay avisos. Aparecen cuando un cambio de oferta pasa la regla de <code>cep-notificaciones</code>
          y alguien sigue esa prenda.
        </p>
        <ul v-else class="lista">
          <li v-for="c in correos" :key="c.id">
            <button type="button" :aria-pressed="abierto?.id === c.id" @click="verCorreo(c.id)">
              <span class="fila1">
                <span class="tipo" :class="c.tipo">{{ TIPOS[c.tipo] ?? c.tipo }}</span>
                <span class="muted">{{ fecha(c.generadoEn) }}</span>
              </span>
              <strong>{{ c.asunto }}</strong>
              <span class="fila1 muted">
                <span>para <code>{{ c.para }}</code></span>
                <span class="chip" :class="MODOS[c.modo]?.clase">{{ MODOS[c.modo]?.texto ?? c.modo }}</span>
              </span>
            </button>
          </li>
        </ul>

        <h3 class="sub">Diseño de cada aviso <span class="muted">· ejemplo, no pasa por la cola</span></h3>
        <p class="muted chico">Se arma con una prenda inventada solo para mostrar cómo se ve. No se envía a nadie.</p>
        <div class="ejemplos">
          <BaseButton v-for="(etiqueta, tipo) in TIPOS" :key="tipo" variante="secundario" tamano="chico"
            :aria-pressed="abierto?.modo === 'ejemplo' && abierto?.tipo === tipo" @click="verEjemplo(tipo)">{{ etiqueta }}</BaseButton>
        </div>
      </div>

      <p v-if="errorAbierto" role="alert" class="error">{{ errorAbierto }}</p>

      <article v-if="abierto" class="correo" aria-label="Vista previa del correo">
        <header>
          <p class="origen" :class="abierto.modo === 'ejemplo' ? 'ejemplo' : 'real'">
            {{ abierto.modo === 'ejemplo' ? 'Ejemplo de diseño · no pasó por la cola' : 'Aviso real · generado por el flujo 2' }}
          </p>
          <dl>
            <dt>Para</dt>
            <dd><code>{{ abierto.para }}</code></dd>
            <dt>Asunto</dt>
            <dd><strong>{{ abierto.asunto }}</strong></dd>
            <template v-if="abierto.eventId">
              <dt>Evento</dt>
              <dd><code>{{ abierto.eventId }}</code></dd>
            </template>
          </dl>
          <div class="pestanas" role="tablist">
            <button type="button" role="tab" :aria-selected="vista === 'html'" @click="vista = 'html'">Como se ve</button>
            <button type="button" role="tab" :aria-selected="vista === 'texto'" @click="vista = 'texto'">Versión texto</button>
            <BaseButton variante="texto" tamano="chico" @click="abierto = null">Cerrar</BaseButton>
          </div>
        </header>
        <!-- sandbox vacío: el HTML del correo no ejecuta nada ni navega el panel. -->
        <iframe v-if="vista === 'html'" :srcdoc="abierto.html" sandbox="" title="Correo" class="marco" />
        <pre v-else>{{ abierto.texto }}</pre>
      </article>
    </div>
  </section>
</template>

<style scoped>
.bandeja h2 { margin-top: 0; }
h3 { margin: var(--cep-sp-4) 0 0; font-size: var(--cep-fs-md); }
h3 .muted { font-weight: normal; }
.sub { margin-top: var(--cep-sp-6); }
.chico { font-size: var(--cep-fs-sm); margin: var(--cep-sp-1) 0 var(--cep-sp-3); }
.cuerpo { display: grid; gap: var(--cep-sp-5); }
@media (min-width: 1024px) { .cuerpo.conVista { grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.1fr); align-items: start; } }
.error { color: var(--cep-alerta); }
.vacio { padding: var(--cep-sp-4); border: 1px dashed var(--cep-line-media); border-radius: var(--cep-r-lg); }
.lista { list-style: none; padding: 0; margin: 0; display: grid; gap: var(--cep-sp-2); }
.lista button {
  all: unset; box-sizing: border-box; width: 100%; cursor: pointer; display: grid; gap: var(--cep-sp-1);
  padding: var(--cep-sp-3); border: 1px solid var(--cep-line); border-radius: var(--cep-r-lg); background: var(--cep-surface);
}
.lista button:hover { border-color: var(--cep-line-fuerte); }
.lista button:focus-visible { outline: 2px solid var(--cep-accent); outline-offset: 2px; }
.lista button[aria-pressed='true'] { border-color: var(--cep-accent); box-shadow: inset 3px 0 0 var(--cep-accent); }
.fila1 { display: flex; justify-content: space-between; align-items: center; gap: var(--cep-sp-2); flex-wrap: wrap; font-size: var(--cep-fs-sm); }
.tipo { font-weight: 600; font-size: var(--cep-fs-sm); }
.tipo.precio\.bajo { color: var(--cep-exito); }
.tipo.oferta\.agotada { color: var(--cep-alerta); }
.tipo.oferta\.repuesta { color: var(--cep-accent); }
.ejemplos { display: flex; flex-wrap: wrap; gap: var(--cep-sp-2); }
code, pre { font-family: var(--cep-font-mono); font-size: var(--cep-fs-sm); }
.chip { font-size: var(--cep-fs-sm); padding: var(--cep-sp-05) var(--cep-sp-2); border-radius: var(--cep-r-pill); border: 1px solid; white-space: nowrap; }
.chip.ok { color: var(--cep-exito); }
.chip.neutro { color: var(--cep-muted); }
.correo { padding: var(--cep-sp-4); border: 1px solid var(--cep-line); border-radius: var(--cep-r-lg); background: var(--cep-surface); display: grid; gap: var(--cep-sp-3); }
.correo header { display: grid; gap: var(--cep-sp-3); }
.origen { margin: 0; font-size: var(--cep-fs-sm); font-weight: 600; padding: var(--cep-sp-1) var(--cep-sp-3); border-radius: var(--cep-r-md); justify-self: start; }
.origen.real { color: var(--cep-exito); background: color-mix(in srgb, var(--cep-exito) 10%, transparent); }
.origen.ejemplo { color: var(--cep-muted); background: var(--cep-sunken); }
dl { display: grid; grid-template-columns: max-content minmax(0, 1fr); gap: var(--cep-sp-1) var(--cep-sp-3); margin: 0; }
dt { color: var(--cep-muted); font-size: var(--cep-fs-sm); }
dd { margin: 0; overflow-wrap: anywhere; }
.pestanas { display: flex; gap: var(--cep-sp-1); align-items: center; flex-wrap: wrap; }
.pestanas button[role='tab'] { font: inherit; font-size: var(--cep-fs-sm); padding: var(--cep-sp-1) var(--cep-sp-3); border: 1px solid var(--cep-line-media); border-radius: var(--cep-r-pill); background: transparent; color: var(--cep-ink); cursor: pointer; }
.pestanas button[aria-selected='true'] { background: var(--cep-ink); color: var(--cep-surface); }
.pestanas :deep(> :last-child) { margin-left: auto; }
/* El correo trae sus propios colores: el marco no hereda el tema del panel. */
.marco { width: 100%; height: 640px; border: 1px solid var(--cep-line); border-radius: var(--cep-r-md); background: #f4f1ea; }
pre { overflow-x: auto; padding: var(--cep-sp-3); background: var(--cep-sunken); border-radius: var(--cep-r-md); white-space: pre-wrap; margin: 0; }
</style>
