<script setup>
import BaseButton from '@/shared/components/BaseButton.vue'
import ConfirmarBorrado from '@/modules/admin/components/ConfirmarBorrado.vue'
import { etiqueta } from '@/modules/admin/data/flujos'

// Las colas de un flujo (o las sueltas) con sus contadores y acciones. El
// estado de borrado y de carga lo lleva el panel: acá solo se muestra.
defineProps({
  colas: { type: Array, required: true },
  ocupado: { type: Boolean, default: false },
  borrando: { type: Object, default: null },
  errorBorrado: { type: String, default: '' },
  revisando: { type: String, default: null },
})
const emit = defineEmits(['ver', 'redrive', 'pedir-borrado', 'confirmar', 'cancelar'])
</script>

<template>
  <div class="tabla-envoltura">
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
        <template v-for="c in colas" :key="c.nombre">
          <tr :class="{ alerta: c.esDlqDe.length && c.mensajesVisibles > 0, elegida: revisando === c.nombre }">
            <th scope="row">
              <span v-if="etiqueta(c.nombre)" class="nombre">{{ etiqueta(c.nombre) }}</span>
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
                :title="`Devuelve los mensajes a ${c.esDlqDe[0]} para reintentarlos`" @click="emit('redrive', c.nombre)">Reintentar (redrive)</BaseButton>
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
.tabla-envoltura { overflow-x: auto; }
table { width: 100%; min-width: 760px; border-collapse: collapse; font-size: var(--cep-fs-md); table-layout: fixed; }
thead th { white-space: nowrap; }
/* Anchos fijos: las tablas de los dos flujos quedan alineadas una bajo otra. */
.c-cola { width: 32%; }
.c-num { width: 6.5rem; }
.c-acciones { width: 18.5rem; }
th, td { text-align: left; padding: var(--cep-sp-2) var(--cep-sp-3); border-bottom: 1px solid var(--cep-line); vertical-align: top; }
th[scope='row'] { font-weight: normal; }
th[scope='row'] .nombre { display: block; font-weight: 600; }
th[scope='row'] small { display: block; max-width: 42ch; }
tr.alerta { background: color-mix(in srgb, var(--cep-alerta) 9%, transparent); }
tr.alerta td.num:first-of-type { color: var(--cep-alerta); font-weight: 700; }
tr.elegida { outline: 2px solid var(--cep-accent); outline-offset: -2px; }
.num { text-align: right; font-variant-numeric: tabular-nums; }
.acciones { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: var(--cep-sp-1); }
code { font-family: var(--cep-font-mono); font-size: var(--cep-fs-sm); }
.error { color: var(--cep-alerta); }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
</style>
