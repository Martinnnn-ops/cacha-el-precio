<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
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
// y al final lo que exista fuera de los flujos.
//
// Desde el 17-11 el panel solo mira: no crea, cambia ni borra colas, topicos ni
// reglas. Eso es de Terraform (infra/terraform/cuenta/mensajeria.tf, que lee
// infra/mensajeria/topologia.json). Si el panel lo hiciera, Terraform chocaria
// con esos recursos o los desharia en el proximo apply. El redrive se queda:
// mueve mensajes, no toca la infraestructura.

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
    titulo: 'Simular y ver correos',
    para: 'Lo que recibe por correo quien sigue una prenda cuando cambia.',
    acciones: [
      'Simular un cambio y verlo recorrer el flujo 2.',
      'Ver los avisos que salieron, tal como llegan.',
      'Ver el diseño de cada tipo de aviso.',
    ],
  },
  {
    id: 'otros',
    titulo: 'Fuera de los flujos',
    para: 'Lo que existe en AWS pero no es parte de ningún flujo, y cómo se cambia la mensajería.',
    acciones: ['Ver si quedó algo creado a mano.', 'Saber dónde se cambian colas, tópicos y reglas.'],
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
    <section v-for="(f, n) in FLUJOS" :id="f.id" :key="f.id" class="bloque flujo">
      <h2><span class="num">{{ n + 1 }}</span>{{ f.titulo.split(' · ')[1] }}</h2>
      <p>{{ f.resumen }}</p>
      <details class="pasos">
        <summary>Paso a paso</summary>
        <ol>
          <li v-for="paso in f.pasos" :key="paso">{{ paso }}</li>
        </ol>
      </details>

      <h3 class="sub">Colas</h3>
      <TablaColas :colas="colasDe(f)" :ocupado="ocupado" :revisando="revisando"
        @ver="verMensajes" @redrive="reprocesar" />

      <template v-if="topicosDe(f).length">
        <h3 class="sub">Reglas de distribución</h3>
        <p class="muted">Quien publica en el tópico no sabe quién escucha: cada regla decide qué copia llega a qué cola.</p>
        <ReglasTopico v-for="t in topicosDe(f)" :key="t.nombre" :topico="t" />
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

    <!-- ═════════ Fuera de los flujos ═════════ -->
    <section id="otros" class="bloque">
      <h2>Fuera de los flujos</h2>
      <p class="muted">
        Este panel solo mira: colas, tópicos y reglas los maneja <strong>Terraform</strong>. Para cambiar algo, se
        edita <code>infra/mensajeria/topologia.json</code> y se corre <code>./tools/tf.sh subir &lt;tu-nombre&gt;</code>.
        Si el estado de arriba dice «AWS distinto de topologia.json», se arregla de la misma forma.
      </p>
      <p v-if="!colasSueltas.length && !topicosSueltos.length" class="muted">
        No hay nada fuera de los flujos: todo lo que existe es de Terraform.
      </p>
      <TablaColas v-if="colasSueltas.length" :colas="colasSueltas" :ocupado="ocupado" :revisando="revisando"
        @ver="verMensajes" @redrive="reprocesar" />
      <ReglasTopico v-for="t in topicosSueltos" :key="t.nombre" :topico="t" />
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
.pasos summary { cursor: pointer; font-weight: 600; }
.pasos ol { display: grid; gap: var(--cep-sp-1); padding-left: var(--cep-sp-6); }
.barra { display: flex; flex-wrap: wrap; gap: var(--cep-sp-3); align-items: center; margin-top: var(--cep-sp-4); }
.estado { font-size: var(--cep-fs-sm); padding: var(--cep-sp-05) var(--cep-sp-2); border-radius: var(--cep-r-pill); border: 1px solid; }
.estado.ok { color: var(--cep-exito); }
.estado.alerta { color: var(--cep-alerta); }
.error { color: var(--cep-alerta); }
.aviso { color: var(--cep-accent); }
code, pre { font-family: var(--cep-font-mono); font-size: var(--cep-fs-sm); }
pre { overflow-x: auto; padding: var(--cep-sp-3); background: var(--cep-sunken); border-radius: var(--cep-r-md); }
.mensajes { list-style: none; padding: 0; display: grid; gap: var(--cep-sp-3); }
.mensajes li { padding: var(--cep-sp-4); border: 1px solid var(--cep-line); border-radius: var(--cep-r-lg); background: var(--cep-surface); }
dl { display: grid; grid-template-columns: minmax(0, max-content) minmax(0, 1fr); gap: var(--cep-sp-1) var(--cep-sp-3); margin: 0; }
dt { font-family: var(--cep-font-mono); font-size: var(--cep-fs-sm); color: var(--cep-muted); }
dd { margin: 0; overflow-wrap: anywhere; }
.bloque.flujo h2 { display: flex; align-items: center; gap: var(--cep-sp-2); }
.num { display: inline-grid; place-items: center; width: 1.8rem; height: 1.8rem; border-radius: 50%; background: var(--cep-ink); color: var(--cep-surface); font-size: var(--cep-fs-md); }
</style>
