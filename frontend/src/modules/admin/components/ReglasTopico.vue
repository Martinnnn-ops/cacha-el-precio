<script setup>
import BaseButton from '@/shared/components/BaseButton.vue'
import ConfirmarBorrado from '@/modules/admin/components/ConfirmarBorrado.vue'
import { describirFiltro, etiqueta } from '@/modules/admin/data/flujos'

// Un tópico SNS y sus reglas de distribución (suscripciones con filtro), en una
// tabla compacta: cada regla dice en palabras qué deja pasar, y el JSON del
// filtro queda a un clic para quien lo quiera ver.
defineProps({
  topico: { type: Object, required: true },
  ocupado: { type: Boolean, default: false },
  borrando: { type: Object, default: null },
  errorBorrado: { type: String, default: '' },
})
const emit = defineEmits(['pedir-borrado', 'confirmar', 'cancelar'])

const json = (v) => JSON.stringify(v, null, 2)
const clave = (t, s) => `${t.nombre} -> ${s.cola}`
const borrandoEste = (b, tipo, nombre) => b?.tipo === tipo && b?.nombre === nombre
</script>

<template>
  <div class="topico">
    <div class="cabecera">
      <p>
        Tópico <code>{{ topico.nombre }}</code>
        <span v-if="etiqueta(topico.nombre)" class="muted">· {{ etiqueta(topico.nombre) }}</span>
        <span class="muted">· {{ topico.suscripciones.length }} regla(s)</span>
      </p>
      <BaseButton variante="texto" tamano="chico"
        @click="emit('pedir-borrado', { tipo: 'topico', nombre: topico.nombre, declarado: topico.declaradoEnTopologia })">Borrar tópico</BaseButton>
    </div>
    <template v-if="borrandoEste(borrando, 'topico', topico.nombre)">
      <ConfirmarBorrado :nombre="topico.nombre" :declarado="topico.declaradoEnTopologia" :ocupado="ocupado"
        @confirmar="(f) => emit('confirmar', f)" @cancelar="emit('cancelar')" />
      <p v-if="errorBorrado" role="alert" class="error">{{ errorBorrado }}</p>
    </template>

    <p v-if="!topico.suscripciones.length" class="muted">Sin reglas: lo que se publique acá no llega a ninguna cola.</p>
    <div v-else class="tabla-envoltura">
      <table>
        <thead>
          <tr>
            <th scope="col">Va a la cola</th>
            <th scope="col">Qué deja pasar</th>
            <th scope="col"><span class="sr-only">Acciones</span></th>
          </tr>
        </thead>
        <tbody>
          <template v-for="s in topico.suscripciones" :key="s.cola">
            <tr>
              <th scope="row">
                <span v-if="etiqueta(s.cola)" class="nombre">{{ etiqueta(s.cola) }}</span>
                <code>{{ s.cola }}</code>
              </th>
              <td>
                {{ describirFiltro(s.filtro) }}
                <details v-if="s.filtro">
                  <summary>Ver el filtro (JSON)</summary>
                  <pre>{{ json(s.filtro) }}</pre>
                </details>
              </td>
              <td class="acciones">
                <BaseButton variante="texto" tamano="chico"
                  @click="emit('pedir-borrado', { tipo: 'sub', nombre: clave(topico, s), topico: topico.nombre, cola: s.cola, declarado: s.declaradaEnTopologia })">Quitar regla</BaseButton>
              </td>
            </tr>
            <tr v-if="borrandoEste(borrando, 'sub', clave(topico, s))">
              <td colspan="3">
                <ConfirmarBorrado :nombre="clave(topico, s)" :declarado="s.declaradaEnTopologia" :ocupado="ocupado"
                  @confirmar="(f) => emit('confirmar', f)" @cancelar="emit('cancelar')" />
                <p v-if="errorBorrado" role="alert" class="error">{{ errorBorrado }}</p>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
.topico + .topico { margin-top: var(--cep-sp-4); }
.cabecera { display: flex; justify-content: space-between; align-items: baseline; gap: var(--cep-sp-2); flex-wrap: wrap; }
.cabecera p { margin: 0; }
.tabla-envoltura { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: var(--cep-fs-md); table-layout: fixed; }
th, td { text-align: left; padding: var(--cep-sp-2) var(--cep-sp-3); border-bottom: 1px solid var(--cep-line); vertical-align: top; }
thead th { white-space: nowrap; }
thead th:first-child { width: 34%; }
thead th:last-child { width: 9rem; }
th[scope='row'] { font-weight: normal; }
.nombre { display: block; font-weight: 600; }
details { margin-top: var(--cep-sp-1); }
summary { cursor: pointer; font-size: var(--cep-fs-sm); color: var(--cep-muted); }
.acciones { text-align: right; }
code, pre { font-family: var(--cep-font-mono); font-size: var(--cep-fs-sm); }
pre { overflow-x: auto; padding: var(--cep-sp-2) var(--cep-sp-3); background: var(--cep-sunken); border-radius: var(--cep-r-md); margin: var(--cep-sp-1) 0 0; }
.error { color: var(--cep-alerta); }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
</style>
