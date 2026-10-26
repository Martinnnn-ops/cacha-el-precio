<script setup>
import { computed } from 'vue'
import BaseButton from '@/shared/components/BaseButton.vue'
import ConfirmarBorrado from '@/modules/admin/components/ConfirmarBorrado.vue'
import { etiqueta } from '@/modules/admin/data/flujos'

// Las colas de un flujo (o las sueltas) en una tabla encerrada en su caja, con
// sus contadores y acciones. Cada DLQ va justo debajo de su cola (↳), así el
// par se lee junto. El estado de borrado y de carga lo lleva el panel.
const props = defineProps({
  colas: { type: Array, required: true },
  ocupado: { type: Boolean, default: false },
  borrando: { type: Object, default: null },
  errorBorrado: { type: String, default: '' },
  revisando: { type: String, default: null },
})
const emit = defineEmits(['ver', 'redrive', 'pedir-borrado', 'confirmar', 'cancelar'])

// Orden de la tabla: cada cola seguida de su DLQ; las DLQ sin su cola, al final.
const ordenadas = computed(() => {
  const porNombre = Object.fromEntries(props.colas.map((c) => [c.nombre, c]))
  const lista = []
  for (const c of props.colas) {
    if (c.esDlqDe.length) continue
    lista.push(c)
    const dlq = c.dlq && porNombre[c.dlq.cola]
    if (dlq) lista.push(dlq)
  }
  for (const c of props.colas) if (!lista.includes(c)) lista.push(c)
  return lista
})

function rol(c) {
  if (c.esDlqDe.length) return { texto: 'DLQ', clase: 'dlq' }
  if (c.dlq) return { texto: 'Principal', clase: 'principal' }
  return { texto: 'Registro', clase: 'registro' }
}
</script>

<template>
  <div class="caja">
    <table>
      <thead>
        <tr>
          <th scope="col" class="c-cola">Cola</th>
          <th scope="col" class="num c-num" title="Esperando a que alguien los lea">En espera</th>
          <th scope="col" class="num c-num" title="Leídos y todavía sin confirmar (ACK)">En proceso</th>
          <th scope="col">Si falla</th>
          <th scope="col" class="c-acciones"><span class="sr-only">Acciones</span></th>
        </tr>
      </thead>
      <tbody>
        <template v-for="c in ordenadas" :key="c.nombre">
          <tr :class="[rol(c).clase, { alerta: c.esDlqDe.length && c.mensajesVisibles > 0, elegida: revisando === c.nombre }]">
            <th scope="row">
              <span class="titulo">
                <span v-if="c.esDlqDe.length" class="gancho" aria-hidden="true">↳</span>
                <span class="rol">{{ rol(c).texto }}</span>
                <span class="nombre">{{ etiqueta(c.nombre) ?? c.nombre }}</span>
              </span>
              <code>{{ c.nombre }}</code>
              <small v-if="c.descripcion" class="muted">{{ c.descripcion }}</small>
            </th>
            <td class="num">{{ c.mensajesVisibles }}</td>
            <td class="num">{{ c.mensajesEnVuelo }}</td>
            <td>
              <template v-if="c.dlq">va a <code>{{ c.dlq.cola }}</code> tras {{ c.dlq.maxIntentos }} intentos</template>
              <template v-else-if="c.esDlqDe.length"><strong>Es la DLQ</strong> de <code>{{ c.esDlqDe.join(', ') }}</code></template>
              <template v-else>—</template>
            </td>
            <td><div class="acciones">
              <!-- Leer una cola con DLQ sumaría intentos a sus mensajes: el gateway responde 409. -->
              <BaseButton v-if="!c.dlq" variante="secundario" tamano="chico" @click="emit('ver', c.nombre)">Ver mensajes</BaseButton>
              <BaseButton v-if="c.esDlqDe.length" tamano="chico" :disabled="ocupado || !c.mensajesVisibles"
                :title="`Devuelve los mensajes a ${c.esDlqDe[0]} para reintentarlos`" @click="emit('redrive', c.nombre)">Reintentar</BaseButton>
              <BaseButton variante="texto" tamano="chico"
                @click="emit('pedir-borrado', { tipo: 'cola', nombre: c.nombre, declarado: c.declaradaEnTopologia })">Borrar</BaseButton>
            </div></td>
          </tr>
          <tr v-if="borrando?.tipo === 'cola' && borrando?.nombre === c.nombre">
            <td colspan="5">
              <ConfirmarBorrado :nombre="c.nombre" :declarado="c.declaradaEnTopologia" :ocupado="ocupado"
                @confirmar="(f) => emit('confirmar', f)" @cancelar="emit('cancelar')" />
              <p v-if="errorBorrado" role="alert" class="error">{{ errorBorrado }}</p>
            </td>
          </tr>
        </template>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
/* La tabla va dentro de una caja: borde, esquinas y encabezado con fondo. */
.caja { overflow-x: auto; border: 1px solid var(--cep-line-media); border-radius: var(--cep-r-lg); background: var(--cep-surface); }
table { width: 100%; min-width: 760px; border-collapse: collapse; font-size: var(--cep-fs-md); table-layout: fixed; }
thead th { white-space: nowrap; background: var(--cep-sunken); font-size: var(--cep-fs-sm); text-transform: uppercase; letter-spacing: 0.03em; color: var(--cep-muted); }
/* Anchos fijos: las tablas de los dos flujos quedan alineadas una bajo otra. */
.c-cola { width: 36%; }
.c-num { width: 6.5rem; }
.c-acciones { width: 15rem; }
th, td { text-align: left; padding: var(--cep-sp-3); border-bottom: 1px solid var(--cep-line); vertical-align: top; }
tbody tr:last-child > * { border-bottom: 0; }
th[scope='row'] { font-weight: normal; }
th[scope='row'] small { display: block; max-width: 46ch; margin-top: 2px; }
th[scope='row'] code { display: block; color: var(--cep-muted); }
.titulo { display: flex; align-items: baseline; gap: var(--cep-sp-2); flex-wrap: wrap; }
.nombre { font-weight: 600; }
.gancho { color: var(--cep-alerta); font-weight: 700; }
.rol { font-size: 0.7rem; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; padding: 1px var(--cep-sp-2); border-radius: var(--cep-r-pill); border: 1px solid; }
.principal .rol { color: var(--cep-accent); }
.dlq .rol { color: var(--cep-alerta); }
.registro .rol { color: var(--cep-muted); }
/* La DLQ se mete hacia adentro, colgando de la cola de arriba. */
tr.dlq th[scope='row'] { padding-left: var(--cep-sp-6); }
tr.principal > * { border-bottom-style: dashed; }
tr.alerta { background: color-mix(in srgb, var(--cep-alerta) 7%, transparent); }
tr.alerta td.num:nth-of-type(1) { color: var(--cep-alerta); font-weight: 700; }
tr.elegida { outline: 2px solid var(--cep-accent); outline-offset: -2px; }
.num { text-align: right; font-variant-numeric: tabular-nums; font-size: var(--cep-fs-lg, 1.15rem); }
thead .num { font-size: var(--cep-fs-sm); }
.acciones { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: var(--cep-sp-1); }
code { font-family: var(--cep-font-mono); font-size: var(--cep-fs-sm); }
.error { color: var(--cep-alerta); }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
</style>
