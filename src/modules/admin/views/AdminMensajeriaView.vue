<script setup>
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import BaseButton from '@/shared/components/BaseButton.vue'
import ConfirmarBorrado from '@/modules/admin/components/ConfirmarBorrado.vue'
import * as api from '@/modules/admin/services/mensajeria.service'

// Panel de la mensajeria (EP3, T12): colas, topicos y reglas de distribucion,
// los mensajes apartados en una DLQ y su redrive. Toda la logica y la
// validacion viven en el gateway (T11); aca se muestra lo que responde,
// incluidos sus errores por campo.

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

const esBorrando = (tipo, nombre) => borrando.value?.tipo === tipo && borrando.value?.nombre === nombre
const json = (v) => JSON.stringify(v, null, 2)
</script>

<template>
  <section class="panel">
    <p class="eyebrow">Administración</p>
    <h1 class="display">Mensajería</h1>
    <p class="muted">
      Colas SQS, tópicos SNS y reglas de distribución de Cacha el Precio. Los mensajes que no se pudieron
      procesar quedan apartados en una DLQ con el motivo de la falla.
    </p>

    <div class="barra">
      <BaseButton tamano="chico" :disabled="cargando" @click="cargar()">Refrescar</BaseButton>
      <span v-if="hora" class="muted" role="status">Actualizado a las {{ hora }} · se refresca cada 10 s</span>
      <span v-if="topologia" class="estado" :class="topologia.sincronizada ? 'ok' : 'alerta'">
        {{ topologia.sincronizada ? 'AWS igual a topologia.json' : 'AWS distinto de topologia.json' }}
      </span>
    </div>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="aviso" role="status" class="aviso">{{ aviso }}</p>

    <!-- ═════════ Colas ═════════ -->
    <h2>Colas</h2>
    <div class="tabla-envoltura">
      <table>
        <thead>
          <tr>
            <th scope="col">Cola</th>
            <th scope="col" class="num">Visibles</th>
            <th scope="col" class="num">En proceso</th>
            <th scope="col">DLQ</th>
            <th scope="col"><span class="sr-only">Acciones</span></th>
          </tr>
        </thead>
        <tbody>
          <template v-for="c in colas" :key="c.nombre">
            <tr>
              <th scope="row">
                <code>{{ c.nombre }}</code>
                <small v-if="c.descripcion" class="muted">{{ c.descripcion }}</small>
              </th>
              <td class="num">{{ c.mensajesVisibles }}</td>
              <td class="num">{{ c.mensajesEnVuelo }}</td>
              <td>
                <template v-if="c.dlq"><code>{{ c.dlq.cola }}</code> tras {{ c.dlq.maxIntentos }} intentos</template>
                <template v-else-if="c.esDlqDe.length">es la DLQ de <code>{{ c.esDlqDe.join(', ') }}</code></template>
                <template v-else>—</template>
              </td>
              <td class="acciones">
                <BaseButton v-if="!c.dlq" variante="secundario" tamano="chico" @click="verMensajes(c.nombre)">Ver mensajes</BaseButton>
                <BaseButton v-if="c.esDlqDe.length" tamano="chico" :disabled="ocupado" @click="reprocesar(c.nombre)">Redrive</BaseButton>
                <BaseButton variante="texto" tamano="chico" @click="pedirBorrado({ tipo: 'cola', nombre: c.nombre, declarado: c.declaradaEnTopologia })">Borrar</BaseButton>
              </td>
            </tr>
            <tr v-if="esBorrando('cola', c.nombre)">
              <td colspan="5">
                <ConfirmarBorrado :nombre="c.nombre" :declarado="c.declaradaEnTopologia" :ocupado="ocupado"
                  @confirmar="borrar" @cancelar="borrando = null" />
                <p v-if="errorBorrado" role="alert" class="error">{{ errorBorrado }}</p>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>

    <!-- ═════════ Mensajes de una DLQ / auditoria ═════════ -->
    <template v-if="revisando">
      <h2>Mensajes en <code>{{ revisando }}</code></h2>
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

    <!-- ═════════ Topicos y reglas ═════════ -->
    <h2>Tópicos y reglas de distribución</h2>
    <p v-if="!topicos.length && !cargando" class="muted">No hay tópicos.</p>
    <article v-for="t in topicos" :key="t.nombre" class="topico">
      <header>
        <h3><code>{{ t.nombre }}</code></h3>
        <BaseButton variante="texto" tamano="chico"
          @click="pedirBorrado({ tipo: 'topico', nombre: t.nombre, declarado: t.declaradoEnTopologia })">Borrar tópico</BaseButton>
      </header>
      <p v-if="t.descripcion" class="muted">{{ t.descripcion }}</p>
      <ConfirmarBorrado v-if="esBorrando('topico', t.nombre)" :nombre="t.nombre" :declarado="t.declaradoEnTopologia"
        :ocupado="ocupado" @confirmar="borrar" @cancelar="borrando = null" />
      <p v-if="esBorrando('topico', t.nombre) && errorBorrado" role="alert" class="error">{{ errorBorrado }}</p>
      <ul class="reglas">
        <li v-for="s in t.suscripciones" :key="s.cola">
          <p>→ <code>{{ s.cola }}</code> <span class="muted">{{ s.entregaCruda ? 'entrega cruda' : 'con sobre de SNS' }}</span></p>
          <pre v-if="s.filtro">{{ json(s.filtro) }}</pre>
          <p v-else class="muted">Sin filtro: recibe todo.</p>
          <BaseButton variante="texto" tamano="chico"
            @click="pedirBorrado({ tipo: 'sub', nombre: `${t.nombre} -> ${s.cola}`, topico: t.nombre, cola: s.cola, declarado: s.declaradaEnTopologia })">Quitar regla</BaseButton>
          <template v-if="esBorrando('sub', `${t.nombre} -> ${s.cola}`)">
            <ConfirmarBorrado :nombre="`${t.nombre} -> ${s.cola}`" :declarado="s.declaradaEnTopologia" :ocupado="ocupado"
              @confirmar="borrar" @cancelar="borrando = null" />
            <p v-if="errorBorrado" role="alert" class="error">{{ errorBorrado }}</p>
          </template>
        </li>
      </ul>
    </article>

    <!-- ═════════ Crear ═════════ -->
    <h2>Crear</h2>
    <div class="formularios">
      <form novalidate @submit.prevent="crearCola">
        <h3>Cola</h3>
        <label>Nombre <input v-model.trim="formCola.nombre" :aria-invalid="!!errores.cola.nombre" /></label>
        <p v-for="e in errores.cola.nombre" :key="e" class="error-campo">{{ e }}</p>
        <label>Visibilidad (s) <input v-model="formCola.visibilidadSegundos" type="number" :aria-invalid="!!errores.cola.visibilidadSegundos" /></label>
        <p v-for="e in errores.cola.visibilidadSegundos" :key="e" class="error-campo">{{ e }}</p>
        <label>Retención (s) <input v-model="formCola.retencionSegundos" type="number" :aria-invalid="!!errores.cola.retencionSegundos" /></label>
        <p v-for="e in errores.cola.retencionSegundos" :key="e" class="error-campo">{{ e }}</p>
        <label>DLQ
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
        <h3>Tópico</h3>
        <label>Nombre <input v-model.trim="formTopico.nombre" :aria-invalid="!!errores.topico.nombre" /></label>
        <p v-for="e in errores.topico.nombre" :key="e" class="error-campo">{{ e }}</p>
        <BaseButton type="submit" tamano="chico" :disabled="ocupado">Crear tópico</BaseButton>
        <p v-if="mensajesForm.topico" role="status" :class="Object.keys(errores.topico).length ? 'error' : 'muted'">{{ mensajesForm.topico }}</p>
      </form>

      <form novalidate @submit.prevent="crearSuscripcion">
        <h3>Regla de distribución</h3>
        <label>Tópico
          <select v-model="formSub.topico" :aria-invalid="!!errores.sub.topico">
            <option value="" disabled>Elige un tópico</option>
            <option v-for="t in topicos" :key="t.nombre" :value="t.nombre">{{ t.nombre }}</option>
          </select>
        </label>
        <p v-for="e in errores.sub.topico" :key="e" class="error-campo">{{ e }}</p>
        <label>Cola
          <select v-model="formSub.cola" :aria-invalid="!!errores.sub.cola">
            <option value="" disabled>Elige una cola</option>
            <option v-for="n in nombresColas.filter((n) => !dlqs.includes(n))" :key="n" :value="n">{{ n }}</option>
          </select>
        </label>
        <p v-for="e in errores.sub.cola" :key="e" class="error-campo">{{ e }}</p>
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
  </section>
</template>

<style scoped>
.panel h2 { margin-top: var(--cep-sp-10); }
.barra { display: flex; flex-wrap: wrap; gap: var(--cep-sp-3); align-items: center; margin-top: var(--cep-sp-4); }
.estado { font-size: var(--cep-fs-sm); padding: var(--cep-sp-05) var(--cep-sp-2); border-radius: var(--cep-r-pill); border: 1px solid; }
.estado.ok { color: var(--cep-exito); }
.estado.alerta { color: var(--cep-alerta); }
.error, .error-campo { color: var(--cep-alerta); }
.error-campo { margin: 0 0 var(--cep-sp-2); font-size: var(--cep-fs-sm); }
.aviso { color: var(--cep-accent); }
.tabla-envoltura { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: var(--cep-fs-md); }
th, td { text-align: left; padding: var(--cep-sp-2) var(--cep-sp-3); border-bottom: 1px solid var(--cep-line); vertical-align: top; }
th[scope='row'] small { display: block; font-weight: normal; max-width: 42ch; }
.num { text-align: right; font-variant-numeric: tabular-nums; }
.acciones { white-space: nowrap; }
.acciones :deep(> *) { margin-right: var(--cep-sp-1); }
code, pre { font-family: var(--cep-font-mono); font-size: var(--cep-fs-sm); }
pre { overflow-x: auto; padding: var(--cep-sp-3); background: var(--cep-sunken); border-radius: var(--cep-r-md); }
.mensajes, .reglas { list-style: none; padding: 0; display: grid; gap: var(--cep-sp-3); }
.mensajes li, .topico { padding: var(--cep-sp-4); border: 1px solid var(--cep-line); border-radius: var(--cep-r-lg); background: var(--cep-surface); }
.topico { margin-top: var(--cep-sp-3); }
.topico header { display: flex; justify-content: space-between; align-items: center; gap: var(--cep-sp-2); }
dl { display: grid; grid-template-columns: minmax(0, max-content) minmax(0, 1fr); gap: var(--cep-sp-1) var(--cep-sp-3); margin: 0; }
dt { font-family: var(--cep-font-mono); font-size: var(--cep-fs-sm); color: var(--cep-muted); }
dd { margin: 0; overflow-wrap: anywhere; }
.formularios { display: grid; grid-template-columns: minmax(0, 1fr); gap: var(--cep-sp-6); }
@media (min-width: 900px) { .formularios { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
form { display: grid; gap: var(--cep-sp-2); align-content: start; }
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
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
</style>
