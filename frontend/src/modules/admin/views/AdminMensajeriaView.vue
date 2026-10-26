<script setup>
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import BaseButton from '@/shared/components/BaseButton.vue'
import BandejaCorreos from '@/modules/admin/components/BandejaCorreos.vue'
import DiagramaFlujos from '@/modules/admin/components/DiagramaFlujos.vue'
import ReglasTopico from '@/modules/admin/components/ReglasTopico.vue'
import TablaColas from '@/modules/admin/components/TablaColas.vue'
import { FLUJOS, esDeUnFlujo, sinPrefijo } from '@/modules/admin/data/flujos'
import * as api from '@/modules/admin/services/mensajeria.service'

// Panel de la mensajeria (EP3, T12): colas, topicos y reglas de distribucion,
// los mensajes apartados en una DLQ y su redrive. Toda la logica y la
// validacion viven en el gateway (T11); aca se muestra lo que responde,
// incluidos sus errores por campo.
//
// Una sola pagina que se lee de arriba abajo, ordenada por flujo y no por tipo
// de recurso, porque asi se explica: primero que hay en la pagina, despues cada
// flujo con sus reglas, colas y fallas, luego los correos que genera el flujo 2
// y al final la administracion (crear/borrar).

// Las secciones del indice de arriba, con lo que le sirven al admin.
const SECCIONES = [
  ...FLUJOS.map((f) => ({ id: f.id, titulo: f.titulo, para: f.paraQue, acciones: f.acciones })),
  {
    id: 'plano',
    titulo: 'El plano completo',
    para: 'Los dos flujos dibujados de punta a punta, con los contadores en vivo.',
    acciones: ['Explicar el recorrido de un mensaje.', 'Ver de un vistazo si alguna DLQ tiene fallas.'],
  },
  {
    id: 'correos',
    titulo: 'Correos generados',
    para: 'Lo que recibe por correo quien sigue una prenda cuando cambia.',
    acciones: ['Ver los avisos que salieron del flujo 2.', 'Ver el diseño de cada tipo de aviso.'],
  },
  {
    id: 'administrar',
    titulo: 'Administrar',
    para: 'Cambiar la mensajería sin entrar a la consola de AWS.',
    acciones: ['Crear colas, tópicos y reglas.', 'Probar que el servidor rechaza datos inválidos.'],
  },
]
function irA(id) {
  document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

const colas = ref([])
const topicos = ref([])
const topologia = ref(null)
const cargando = ref(false)
const error = ref('')
const aviso = ref('')
const actualizado = ref(null)

async function cargar() {
  cargando.value = true
  try {
    const [c, t, topo] = await Promise.all([api.listarColas(), api.listarTopicos(), api.compararTopologia()])
    colas.value = c
    topicos.value = t
    topologia.value = topo
    error.value = ''
    actualizado.value = new Date()
  } catch (e) {
    error.value = api.leerError(e).mensaje
  } finally {
    cargando.value = false
  }
}

// La tabla se refresca sola cada 10 s, pero no mientras la pestaña no se ve.
let reloj = null
onMounted(() => {
  cargar()
  reloj = setInterval(() => document.visibilityState === 'visible' && cargar(), 10000)
})
onBeforeUnmount(() => clearInterval(reloj))

const nombresColas = computed(() => colas.value.map((c) => c.nombre))

// Lo de cada flujo, en el orden en que se explica; lo demas va a «Otros».
const colasDe = (f) => f.colas.map((n) => colas.value.find((c) => sinPrefijo(c.nombre) === n)).filter(Boolean)
const topicosDe = (f) => f.topicos.map((n) => topicos.value.find((t) => sinPrefijo(t.nombre) === n)).filter(Boolean)
// El visor de mensajes se abre bajo la tabla donde se pidio.
const flujoDeRevisando = computed(() =>
  FLUJOS.find((f) => revisando.value && f.colas.includes(sinPrefijo(revisando.value)))?.id ?? 'otros')
const colasSueltas = computed(() => colas.value.filter((c) => !esDeUnFlujo(c.nombre)))
const topicosSueltos = computed(() => topicos.value.filter((t) => !esDeUnFlujo(t.nombre)))
const totalApartados = computed(() =>
  colas.value.filter((c) => c.esDlqDe.length).reduce((n, c) => n + c.mensajesVisibles, 0))

// Filtros de ejemplo para no escribir el JSON de memoria.
const FILTROS_EJEMPLO = [
  { etiqueta: 'Sin filtro (recibe todo)', valor: '' },
  { etiqueta: 'Solo agotados', valor: '{"type": ["oferta.agotada"]}' },
  { etiqueta: 'Bajas de 30% o más', valor: '{"type": ["precio.bajo"], "discountPct": [{"numeric": [">=", 30]}]}' },
  { etiqueta: 'Agotados y reposiciones', valor: '{"type": ["oferta.agotada", "oferta.repuesta"]}' },
]
const dlqs = computed(() => colas.value.filter((c) => c.esDlqDe.length).map((c) => c.nombre))
const hora = computed(() => actualizado.value?.toLocaleTimeString('es-CL') ?? '')

// ── Mensajes apartados (DLQ) y auditoria ──────────────────────────────────
const revisando = ref(null)
const mensajes = ref([])
const errorMensajes = ref('')
const ocupado = ref(false)

async function verMensajes(cola) {
  revisando.value = cola
  mensajes.value = []
  errorMensajes.value = ''
  try {
    mensajes.value = await api.verMensajes(cola)
  } catch (e) {
    errorMensajes.value = api.leerError(e).mensaje
  }
}

async function reprocesar(dlq) {
  ocupado.value = true
  aviso.value = ''
  try {
    const r = await api.reprocesar(dlq)
    aviso.value =
      r.mensajesMovidos === null
        ? `Redrive en curso: los mensajes de ${dlq} vuelven a ${r.destino}.`
        : `${r.mensajesMovidos} mensaje(s) de ${dlq} devuelto(s) a ${r.destino}.`
    await cargar()
    if (revisando.value === dlq) await verMensajes(dlq)
  } catch (e) {
    aviso.value = api.leerError(e).mensaje
  } finally {
    ocupado.value = false
  }
}

// ── Crear (con los errores por campo que devuelve el servidor) ─────────────
const formCola = reactive({ nombre: 'cep-', visibilidadSegundos: 30, retencionSegundos: 345600, dlq: '', maxIntentos: 5 })
const formTopico = reactive({ nombre: 'cep-' })
const formSub = reactive({ topico: '', cola: '', filtro: '', entregaCruda: true })
const errores = reactive({ cola: {}, topico: {}, sub: {} })
const mensajesForm = reactive({ cola: '', topico: '', sub: '' })

async function enviar(clave, accion, datos, alTerminar) {
  errores[clave] = {}
  mensajesForm[clave] = ''
  ocupado.value = true
  try {
    await accion(datos)
    mensajesForm[clave] = 'Creado.'
    alTerminar?.()
    await cargar()
  } catch (e) {
    const { mensaje, campos } = api.leerError(e)
    errores[clave] = campos
    mensajesForm[clave] = mensaje
  } finally {
    ocupado.value = false
  }
}

function crearCola() {
  const conDlq = formCola.dlq !== ''
  enviar('cola', api.crearCola, {
    nombre: formCola.nombre,
    visibilidadSegundos: Number(formCola.visibilidadSegundos),
    retencionSegundos: Number(formCola.retencionSegundos),
    dlq: conDlq ? formCola.dlq : null,
    maxIntentos: conDlq ? Number(formCola.maxIntentos) : null,
  }, () => { formCola.nombre = 'cep-' })
}

function crearTopico() {
  enviar('topico', api.crearTopico, { nombre: formTopico.nombre }, () => { formTopico.nombre = 'cep-' })
}

function crearSuscripcion() {
  let filtro = null
  if (formSub.filtro.trim()) {
    try {
      filtro = JSON.parse(formSub.filtro)
    } catch {
      errores.sub = { filtro: ['No es un JSON válido. Ejemplo: {"type": ["oferta.agotada"]}'] }
      mensajesForm.sub = ''
      return
    }
  }
  enviar('sub', api.crearSuscripcion,
    { topico: formSub.topico, cola: formSub.cola, filtro, entregaCruda: formSub.entregaCruda },
    () => { formSub.filtro = '' })
}

// ── Borrar (confirmando con el nombre) ─────────────────────────────────────
const borrando = ref(null) // { tipo, nombre, declarado, topico?, cola? }
const errorBorrado = ref('')

function pedirBorrado(item) {
  borrando.value = item
  errorBorrado.value = ''
}

async function borrar(forzar) {
  const b = borrando.value
  ocupado.value = true
  try {
    if (b.tipo === 'cola') await api.borrarCola(b.nombre, forzar)
    if (b.tipo === 'topico') await api.borrarTopico(b.nombre, forzar)
    if (b.tipo === 'sub') await api.borrarSuscripcion(b.topico, b.cola, forzar)
    borrando.value = null
    if (revisando.value === b.nombre) revisando.value = null
    await cargar()
  } catch (e) {
    errorBorrado.value = api.leerError(e).mensaje
  } finally {
    ocupado.value = false
  }
}

</script>

<template>
  <section class="panel">
    <p class="eyebrow">Administración</p>
    <h1 class="display">Mensajería</h1>
    <p class="muted">
      Cómo se comunican los servicios de Cacha el Precio sin llamarse directo: colas SQS, un tópico SNS y reglas
      que reparten cada mensaje a quien le corresponde. Lo que no se puede procesar queda apartado en una DLQ con
      el motivo de la falla.
    </p>

    <div class="barra">
      <BaseButton tamano="chico" :disabled="cargando" @click="cargar()">Refrescar</BaseButton>
      <span v-if="hora" class="muted" role="status">Actualizado a las {{ hora }} · se refresca cada 10 s</span>
      <span v-if="topologia" class="estado" :class="topologia.sincronizada ? 'ok' : 'alerta'"
        title="Compara lo que existe en AWS con lo declarado en infra/mensajeria/topologia.json">
        {{ topologia.sincronizada ? 'AWS igual a topologia.json' : 'AWS distinto de topologia.json' }}
      </span>
      <span v-if="totalApartados" class="estado alerta">{{ totalApartados }} mensaje(s) apartado(s) en DLQ</span>
    </div>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="aviso" role="status" class="aviso">{{ aviso }}</p>

    <!-- ═════════ Que hay en esta pagina ═════════ -->
    <section class="bloque">
      <h2>Qué hay en esta página</h2>
      <p class="muted">
        Los servicios no se llaman directo: se dejan mensajes. La idea en una línea:
        <strong>el catálogo se guarda primero y los avisos viajan después.</strong>
      </p>
      <ol class="indice">
        <li v-for="(x, i) in SECCIONES" :key="x.id">
          <a :href="`#${x.id}`" class="tarjeta" @click.prevent="irA(x.id)">
            <span class="paso">{{ i + 1 }}</span>
            <strong>{{ x.titulo }}</strong>
            <span class="para">{{ x.para }}</span>
            <ul>
              <li v-for="a in x.acciones" :key="a">{{ a }}</li>
            </ul>
            <span class="ir">Ir a la sección ↓</span>
          </a>
        </li>
      </ol>
    </section>

    <!-- ═════════ Cada flujo: que hace, sus reglas y sus colas ═════════ -->
    <section v-for="f in FLUJOS" :id="f.id" :key="f.id" class="bloque">
      <h2>{{ f.titulo }}</h2>
      <p>{{ f.resumen }}</p>
      <details class="pasos">
        <summary>Paso a paso</summary>
        <ol>
          <li v-for="paso in f.pasos" :key="paso">{{ paso }}</li>
        </ol>
      </details>

      <h3 class="sub">Colas</h3>
      <TablaColas :colas="colasDe(f)" :ocupado="ocupado" :borrando="borrando" :error-borrado="errorBorrado"
        :revisando="revisando" @ver="verMensajes" @redrive="reprocesar" @pedir-borrado="pedirBorrado"
        @confirmar="borrar" @cancelar="borrando = null" />

      <template v-if="topicosDe(f).length">
        <h3 class="sub">Reglas de distribución</h3>
        <p class="muted">Quien publica en el tópico no sabe quién escucha: cada regla decide qué copia llega a qué cola.</p>
        <ReglasTopico v-for="t in topicosDe(f)" :key="t.nombre" :topico="t" :ocupado="ocupado"
          :borrando="borrando" :error-borrado="errorBorrado"
          @pedir-borrado="pedirBorrado" @confirmar="borrar" @cancelar="borrando = null" />
      </template>

      <template v-if="revisando && flujoDeRevisando === f.id">
        <h3 class="sub">Mensajes en <code>{{ revisando }}</code></h3>
        <p class="muted">Se leen sin borrarlos. SQS entrega una muestra de hasta 10 por lectura.</p>
        <p v-if="errorMensajes" role="alert" class="error">{{ errorMensajes }}</p>
        <p v-else-if="!mensajes.length" class="muted">No hay mensajes.</p>
        <ul class="mensajes">
          <li v-for="m in mensajes" :key="m.messageId">
            <dl v-if="Object.keys(m.diagnostico).length">
              <template v-for="clave in ['error-type', 'error-reason', 'failed-by', 'failed-at']" :key="clave">
                <dt>{{ clave }}</dt>
                <dd>{{ m.diagnostico[clave] ?? '—' }}</dd>
              </template>
            </dl>
            <p class="muted">Recibido {{ m.vecesRecibido }} vez/veces · id {{ m.messageId }}</p>
            <details>
              <summary>Ver el mensaje</summary>
              <pre>{{ m.cuerpo }}</pre>
            </details>
          </li>
        </ul>
        <BaseButton variante="texto" tamano="chico" @click="revisando = null">Cerrar</BaseButton>
      </template>
    </section>

    <!-- ═════════ El plano completo ═════════ -->
    <section id="plano" class="bloque">
      <h2>El plano completo</h2>
      <p class="muted">Los dos flujos de punta a punta. Haz clic en una cola para ir a su tabla.</p>
      <DiagramaFlujos :colas="colas" @ir="irA" />
    </section>

    <!-- ═════════ Correos que genero el flujo 2 ═════════ -->
    <BandejaCorreos id="correos" class="bloque" :actualizado="actualizado" />

    <!-- ═════════ Administrar ═════════ -->
    <section id="administrar" class="bloque">
      <h2>Administrar</h2>
      <p class="muted">
        Crear y borrar colas, tópicos y reglas. El servidor valida cada campo: si un nombre o un valor no sirve, el
        error aparece bajo ese campo. Lo declarado en <code>topologia.json</code> pide confirmación extra para
        borrarse, porque lo usa un flujo.
      </p>

      <div class="formularios">
        <form novalidate @submit.prevent="crearCola">
          <h3>Nueva cola</h3>
          <p class="muted ayuda">Un buzón donde un servicio deja mensajes y otro los retira a su ritmo.</p>
          <label>Nombre <input v-model.trim="formCola.nombre" :aria-invalid="!!errores.cola.nombre" /></label>
          <p v-for="e in errores.cola.nombre" :key="e" class="error-campo">{{ e }}</p>
          <label>Visibilidad (s) <small class="muted">cuánto se esconde un mensaje mientras alguien lo procesa</small>
            <input v-model="formCola.visibilidadSegundos" type="number" :aria-invalid="!!errores.cola.visibilidadSegundos" /></label>
          <p v-for="e in errores.cola.visibilidadSegundos" :key="e" class="error-campo">{{ e }}</p>
          <label>Retención (s) <small class="muted">cuánto vive un mensaje sin leer</small>
            <input v-model="formCola.retencionSegundos" type="number" :aria-invalid="!!errores.cola.retencionSegundos" /></label>
          <p v-for="e in errores.cola.retencionSegundos" :key="e" class="error-campo">{{ e }}</p>
          <label>DLQ <small class="muted">a dónde va lo que falla demasiadas veces</small>
            <select v-model="formCola.dlq" :aria-invalid="!!errores.cola.dlq">
              <option value="">Sin DLQ</option>
              <option v-for="n in nombresColas" :key="n" :value="n">{{ n }}</option>
            </select>
          </label>
          <p v-for="e in errores.cola.dlq" :key="e" class="error-campo">{{ e }}</p>
          <label v-if="formCola.dlq">Intentos antes de apartar (1–10) <input v-model="formCola.maxIntentos" type="number" :aria-invalid="!!errores.cola.maxIntentos" /></label>
          <p v-for="e in errores.cola.maxIntentos" :key="e" class="error-campo">{{ e }}</p>
          <BaseButton type="submit" tamano="chico" :disabled="ocupado">Crear cola</BaseButton>
          <p v-if="mensajesForm.cola" role="status" :class="Object.keys(errores.cola).length ? 'error' : 'muted'">{{ mensajesForm.cola }}</p>
        </form>

        <form novalidate @submit.prevent="crearTopico">
          <h3>Nuevo tópico</h3>
          <p class="muted ayuda">Un altavoz: lo que se publica acá se copia a cada cola que tenga una regla.</p>
          <label>Nombre <input v-model.trim="formTopico.nombre" :aria-invalid="!!errores.topico.nombre" /></label>
          <p v-for="e in errores.topico.nombre" :key="e" class="error-campo">{{ e }}</p>
          <BaseButton type="submit" tamano="chico" :disabled="ocupado">Crear tópico</BaseButton>
          <p v-if="mensajesForm.topico" role="status" :class="Object.keys(errores.topico).length ? 'error' : 'muted'">{{ mensajesForm.topico }}</p>
        </form>

        <form novalidate @submit.prevent="crearSuscripcion">
          <h3>Nueva regla de distribución</h3>
          <p class="muted ayuda">Conecta un tópico con una cola, y opcionalmente filtra qué mensajes pasan.</p>
          <label>Tópico <small class="muted">de dónde salen los mensajes</small>
            <select v-model="formSub.topico" :aria-invalid="!!errores.sub.topico">
              <option value="" disabled>Elige un tópico</option>
              <option v-for="t in topicos" :key="t.nombre" :value="t.nombre">{{ t.nombre }}</option>
            </select>
          </label>
          <p v-for="e in errores.sub.topico" :key="e" class="error-campo">{{ e }}</p>
          <label>Cola <small class="muted">a dónde llegan</small>
            <select v-model="formSub.cola" :aria-invalid="!!errores.sub.cola">
              <option value="" disabled>Elige una cola</option>
              <option v-for="n in nombresColas.filter((n) => !dlqs.includes(n))" :key="n" :value="n">{{ n }}</option>
            </select>
          </label>
          <p v-for="e in errores.sub.cola" :key="e" class="error-campo">{{ e }}</p>
          <label>Filtro de ejemplo
            <select @change="formSub.filtro = $event.target.value">
              <option v-for="f in FILTROS_EJEMPLO" :key="f.etiqueta" :value="f.valor">{{ f.etiqueta }}</option>
            </select>
          </label>
          <label>Filtro (JSON, vacío = recibe todo)
            <textarea v-model="formSub.filtro" rows="4" spellcheck="false" placeholder='{"type": ["oferta.agotada"]}'
              :aria-invalid="!!errores.sub.filtro" />
          </label>
          <p v-for="e in errores.sub.filtro" :key="e" class="error-campo">{{ e }}</p>
          <label class="casilla"><input v-model="formSub.entregaCruda" type="checkbox" /> Entrega cruda (sin el sobre de SNS)</label>
          <BaseButton type="submit" tamano="chico" :disabled="ocupado">Crear regla</BaseButton>
          <p v-if="mensajesForm.sub" role="status" :class="Object.keys(errores.sub).length ? 'error' : 'muted'">{{ mensajesForm.sub }}</p>
        </form>
      </div>

      <h3 class="sub">Recursos fuera de los flujos</h3>
      <p v-if="!colasSueltas.length && !topicosSueltos.length" class="muted">
        Ninguno: todo lo que existe pertenece a un flujo. Lo que crees arriba aparece en esta lista.
      </p>
      <TablaColas v-if="colasSueltas.length" :colas="colasSueltas" :ocupado="ocupado" :borrando="borrando"
        :error-borrado="errorBorrado" :revisando="revisando" @ver="verMensajes" @redrive="reprocesar"
        @pedir-borrado="pedirBorrado" @confirmar="borrar" @cancelar="borrando = null" />
      <ReglasTopico v-for="t in topicosSueltos" :key="t.nombre" :topico="t" :ocupado="ocupado" :borrando="borrando"
        :error-borrado="errorBorrado" @pedir-borrado="pedirBorrado" @confirmar="borrar" @cancelar="borrando = null" />
      <template v-if="revisando && flujoDeRevisando === 'otros'">
        <h3 class="sub">Mensajes en <code>{{ revisando }}</code></h3>
        <p class="muted">Se leen sin borrarlos. SQS entrega una muestra de hasta 10 por lectura.</p>
        <p v-if="errorMensajes" role="alert" class="error">{{ errorMensajes }}</p>
        <p v-else-if="!mensajes.length" class="muted">No hay mensajes.</p>
        <ul class="mensajes">
          <li v-for="m in mensajes" :key="m.messageId">
            <dl v-if="Object.keys(m.diagnostico).length">
              <template v-for="clave in ['error-type', 'error-reason', 'failed-by', 'failed-at']" :key="clave">
                <dt>{{ clave }}</dt>
                <dd>{{ m.diagnostico[clave] ?? '—' }}</dd>
              </template>
            </dl>
            <p class="muted">Recibido {{ m.vecesRecibido }} vez/veces · id {{ m.messageId }}</p>
            <details>
              <summary>Ver el mensaje</summary>
              <pre>{{ m.cuerpo }}</pre>
            </details>
          </li>
        </ul>
        <BaseButton variante="texto" tamano="chico" @click="revisando = null">Cerrar</BaseButton>
      </template>
    </section>
  </section>
</template>

<style scoped>
.panel h2 { margin-top: var(--cep-sp-6); }
.sub { margin-top: var(--cep-sp-8); }
.bloque > h2 { margin-top: 0; }
.bloque { scroll-margin-top: var(--cep-sp-8); padding-top: var(--cep-sp-4); border-top: 1px solid var(--cep-line); margin-top: var(--cep-sp-10); }
.indice { list-style: none; padding: 0; margin: var(--cep-sp-4) 0 0; display: grid; gap: var(--cep-sp-3); grid-template-columns: minmax(0, 1fr); }
@media (min-width: 720px) { .indice { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (min-width: 1024px) { .indice { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
.indice > li { display: flex; }
.tarjeta {
  flex: 1; display: grid; align-content: start; gap: var(--cep-sp-1);
  padding: var(--cep-sp-4); border: 1px solid var(--cep-line); border-radius: var(--cep-r-lg);
  background: var(--cep-surface); color: var(--cep-ink); text-decoration: none;
}
.tarjeta:hover, .tarjeta:focus-visible { border-color: var(--cep-accent); outline: none; }
.tarjeta .paso { display: inline-grid; place-items: center; width: 1.5rem; height: 1.5rem; border-radius: 50%; background: var(--cep-sunken); font-size: var(--cep-fs-sm); }
.tarjeta .para { color: var(--cep-muted); font-size: var(--cep-fs-sm); }
.tarjeta ul { margin: var(--cep-sp-1) 0 0; padding-left: var(--cep-sp-4); font-size: var(--cep-fs-sm); display: grid; gap: 2px; }
.tarjeta .ir { margin-top: var(--cep-sp-2); font-size: var(--cep-fs-sm); color: var(--cep-accent); font-weight: 600; }
.ayuda { font-size: var(--cep-fs-sm); margin: 0 0 var(--cep-sp-2); }
.pasos summary { cursor: pointer; font-weight: 600; }
.pasos ol { display: grid; gap: var(--cep-sp-1); padding-left: var(--cep-sp-6); }
label small { font-weight: normal; }
.barra { display: flex; flex-wrap: wrap; gap: var(--cep-sp-3); align-items: center; margin-top: var(--cep-sp-4); }
.estado { font-size: var(--cep-fs-sm); padding: var(--cep-sp-05) var(--cep-sp-2); border-radius: var(--cep-r-pill); border: 1px solid; }
.estado.ok { color: var(--cep-exito); }
.estado.alerta { color: var(--cep-alerta); }
.error, .error-campo { color: var(--cep-alerta); }
.error-campo { margin: 0 0 var(--cep-sp-2); font-size: var(--cep-fs-sm); }
.aviso { color: var(--cep-accent); }
code, pre { font-family: var(--cep-font-mono); font-size: var(--cep-fs-sm); }
pre { overflow-x: auto; padding: var(--cep-sp-3); background: var(--cep-sunken); border-radius: var(--cep-r-md); }
.mensajes { list-style: none; padding: 0; display: grid; gap: var(--cep-sp-3); }
.mensajes li { padding: var(--cep-sp-4); border: 1px solid var(--cep-line); border-radius: var(--cep-r-lg); background: var(--cep-surface); }
dl { display: grid; grid-template-columns: minmax(0, max-content) minmax(0, 1fr); gap: var(--cep-sp-1) var(--cep-sp-3); margin: 0; }
dt { font-family: var(--cep-font-mono); font-size: var(--cep-fs-sm); color: var(--cep-muted); }
dd { margin: 0; overflow-wrap: anywhere; }
.formularios { display: grid; grid-template-columns: minmax(0, 1fr); gap: var(--cep-sp-4); margin-top: var(--cep-sp-4); }
@media (min-width: 900px) { .formularios { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
form {
  display: grid; gap: var(--cep-sp-2); align-content: start;
  padding: var(--cep-sp-4); border: 1px solid var(--cep-line); border-radius: var(--cep-r-lg); background: var(--cep-surface);
}
form h3 { margin: 0; }
form :deep(button[type='submit']) { margin-top: var(--cep-sp-2); justify-self: start; }
label { display: grid; gap: var(--cep-sp-1); font-size: var(--cep-fs-sm); }
.casilla { display: flex; gap: var(--cep-sp-2); align-items: center; }
input:not([type='checkbox']), select, textarea {
  min-height: var(--cep-control-h-sm);
  padding: var(--cep-sp-1) var(--cep-sp-2);
  font: inherit;
  color: var(--cep-ink);
  background: var(--cep-surface);
  border: 1px solid var(--cep-line-media);
  border-radius: var(--cep-r-md);
}
textarea { font-family: var(--cep-font-mono); }
[aria-invalid='true'] { border-color: var(--cep-alerta); }
</style>
