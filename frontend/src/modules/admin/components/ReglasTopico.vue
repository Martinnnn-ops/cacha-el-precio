<script setup>
import { describirFiltro, etiqueta } from '@/modules/admin/data/flujos'

// Un tópico SNS y sus reglas de distribución (suscripciones con filtro), en una
// tabla compacta: cada regla dice en palabras qué deja pasar, y el JSON del
// filtro queda a un clic para quien lo quiera ver. Solo se mira: desde el 17-11
// las reglas las maneja Terraform (topologia.json + tools/tf.sh subir).
defineProps({
  topico: { type: Object, required: true },
})

const json = (v) => JSON.stringify(v, null, 2)
</script>

<template>
  <div class="caja">
    <div class="cabecera">
      <p>
        <span class="rol topico">Tópico</span>
        <strong>{{ etiqueta(topico.nombre) ?? topico.nombre }}</strong>
        <code>{{ topico.nombre }}</code>
        <span class="muted">· reparte a {{ topico.suscripciones.length }} cola(s)</span>
      </p>
    </div>

    <p v-if="!topico.suscripciones.length" class="vacio muted">Sin reglas: lo que se publique acá no llega a ninguna cola.</p>
    <div v-else class="tabla-envoltura">
      <table>
        <thead>
          <tr>
            <th scope="col" class="c-cola">Va a la cola</th>
            <th scope="col">Qué deja pasar</th>
          </tr>
        </thead>
        <tbody>
          <template v-for="s in topico.suscripciones" :key="s.cola">
            <tr :class="s.filtro ? 'con' : 'sin'">
              <th scope="row">
                <span class="titulo">
                  <span class="rol">{{ s.filtro ? 'Con filtro' : 'Sin filtro' }}</span>
                  <span class="nombre">{{ etiqueta(s.cola) ?? s.cola }}</span>
                </span>
                <code>{{ s.cola }}</code>
              </th>
              <td>
                {{ describirFiltro(s.filtro) }}
                <details v-if="s.filtro">
                  <summary>Ver el filtro (JSON)</summary>
                  <pre>{{ json(s.filtro) }}</pre>
                </details>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
/* Mismo formato que la tabla de colas: caja, encabezado con fondo y anchos fijos. */
.caja { border: 1px solid var(--cep-line-media); border-radius: var(--cep-r-lg); background: var(--cep-surface); overflow: hidden; }
.caja + .caja { margin-top: var(--cep-sp-4); }
.cabecera { display: flex; justify-content: space-between; align-items: center; gap: var(--cep-sp-2); flex-wrap: wrap; padding: var(--cep-sp-3); border-bottom: 1px solid var(--cep-line); }
.cabecera p { margin: 0; display: flex; align-items: baseline; gap: var(--cep-sp-2); flex-wrap: wrap; }
.vacio { padding: var(--cep-sp-3); margin: 0; border-bottom: 1px solid var(--cep-line); }
.tabla-envoltura { overflow-x: auto; }
table { width: 100%; min-width: 760px; border-collapse: collapse; font-size: var(--cep-fs-md); table-layout: fixed; }
thead th { white-space: nowrap; background: var(--cep-sunken); font-size: var(--cep-fs-sm); text-transform: uppercase; letter-spacing: 0.03em; color: var(--cep-muted); }
.c-cola { width: 36%; }
th, td { text-align: left; padding: var(--cep-sp-3); border-bottom: 1px solid var(--cep-line); vertical-align: top; }
tbody tr:last-child > * { border-bottom: 0; }
th[scope='row'] { font-weight: normal; }
th[scope='row'] code { display: block; color: var(--cep-muted); }
.titulo { display: flex; align-items: baseline; gap: var(--cep-sp-2); flex-wrap: wrap; }
.nombre { font-weight: 600; }
.rol { font-size: 0.7rem; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; padding: 1px var(--cep-sp-2); border-radius: var(--cep-r-pill); border: 1px solid; }
.rol.topico { color: var(--cep-ink); }
.con .rol { color: var(--cep-accent); }
.sin .rol { color: var(--cep-muted); }
details { margin-top: var(--cep-sp-1); }
summary { cursor: pointer; font-size: var(--cep-fs-sm); color: var(--cep-muted); }
code, pre { font-family: var(--cep-font-mono); font-size: var(--cep-fs-sm); }
pre { overflow-x: auto; padding: var(--cep-sp-2) var(--cep-sp-3); background: var(--cep-sunken); border-radius: var(--cep-r-md); margin: var(--cep-sp-1) 0 0; }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
</style>
