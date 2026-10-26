<script setup>
import { computed } from 'vue'

// Los dos flujos como un plano: cada pieza en su columna de una grilla, para
// que la DLQ quede justo bajo su cola y las ramas del tópico se lean como
// ramas. Los contadores son los de las colas en vivo; cada cola lleva a la
// sección de su flujo.
const props = defineProps({
  colas: { type: Array, required: true },
})
const emit = defineEmits(['ir'])

const porNombre = computed(() => Object.fromEntries(props.colas.map((c) => [c.nombre.replace(/^cep-/, ''), c])))
const n = (cola) => porNombre.value[cola]?.mensajesVisibles ?? '–'
const conFallas = (cola) => (porNombre.value[cola]?.mensajesVisibles ?? 0) > 0
</script>

<template>
  <div class="diagrama">
    <ul class="leyenda" aria-label="Leyenda">
      <li><span class="muestra servicio" /> Servicio que produce o consume</li>
      <li><span class="muestra cola" /> Cola SQS (espera su turno)</li>
      <li><span class="muestra topico" /> Tópico SNS (reparte copias)</li>
      <li><span class="muestra dlq" /> DLQ (lo que falló, apartado)</li>
    </ul>

    <figure class="flujo">
      <figcaption><span class="num">1</span> Ingesta de ofertas</figcaption>
      <div class="plano">
        <div class="nodo servicio" style="grid-area: 1 / 1">Scraper<small>captura la oferta</small></div>
        <div class="flecha" style="grid-area: 1 / 2">→</div>
        <div class="nodo servicio" style="grid-area: 1 / 3">API Gateway<small>POST /ingesta</small></div>
        <div class="flecha" style="grid-area: 1 / 4">→</div>
        <button type="button" class="nodo cola" style="grid-area: 1 / 5" @click="emit('ir', 'flujo1')">
          cep-ofertas-crudas<small>{{ n('ofertas-crudas') }} en espera</small>
        </button>
        <div class="flecha" style="grid-area: 1 / 6">→</div>
        <div class="nodo servicio" style="grid-area: 1 / 7">product-service<small>valida y guarda</small></div>
        <div class="flecha" style="grid-area: 1 / 8">→</div>
        <div class="nodo fin" style="grid-area: 1 / 9">Catálogo<small>PostgreSQL</small></div>

        <div class="baja" style="grid-area: 2 / 5">↓ inválida, o 5 intentos fallidos</div>
        <button type="button" class="nodo dlq" :class="{ con: conFallas('ofertas-crudas-dlq') }" style="grid-area: 3 / 5"
          @click="emit('ir', 'flujo1')">
          cep-ofertas-crudas-dlq<small>{{ n('ofertas-crudas-dlq') }} apartados</small>
        </button>
      </div>
    </figure>

    <figure class="flujo">
      <figcaption><span class="num">2</span> Avisos de cambios</figcaption>
      <div class="plano">
        <div class="nodo servicio" style="grid-area: 1 / 1">product-service<small>detecta un cambio</small></div>
        <div class="flecha" style="grid-area: 1 / 2">→</div>
        <button type="button" class="nodo topico" style="grid-area: 1 / 3" @click="emit('ir', 'flujo2')">
          cep-cambios-oferta<small>publica una vez</small>
        </button>
        <div class="flecha regla" style="grid-area: 1 / 4">→<small>≥ 10%, agotado o repuesto</small></div>
        <button type="button" class="nodo cola" style="grid-area: 1 / 5" @click="emit('ir', 'flujo2')">
          cep-notificaciones<small>{{ n('notificaciones') }} en espera</small>
        </button>
        <div class="flecha" style="grid-area: 1 / 6">→</div>
        <div class="nodo servicio" style="grid-area: 1 / 7">notification-service<small>busca quién la sigue</small></div>
        <div class="flecha" style="grid-area: 1 / 8">→</div>
        <button type="button" class="nodo fin" style="grid-area: 1 / 9" @click="emit('ir', 'correos')">
          Correo<small>ver en la bandeja</small>
        </button>

        <div class="baja" style="grid-area: 2 / 5">↓ si no se puede enviar</div>
        <button type="button" class="nodo dlq" :class="{ con: conFallas('notificaciones-dlq') }" style="grid-area: 3 / 5"
          @click="emit('ir', 'flujo2')">
          cep-notificaciones-dlq<small>{{ n('notificaciones-dlq') }} apartados</small>
        </button>

        <!-- La segunda rama del tópico: una L que baja desde el tópico hasta la auditoría. -->
        <div class="codo" style="grid-area: 2 / 3 / 5 / 4" aria-hidden="true" />
        <div class="flecha regla" style="grid-area: 4 / 4">→<small>todo, sin filtro</small></div>
        <button type="button" class="nodo cola" style="grid-area: 4 / 5" @click="emit('ir', 'flujo2')">
          cep-auditoria-precios<small>{{ n('auditoria-precios') }} guardados</small>
        </button>
        <div class="nota" style="grid-area: 4 / 6 / 4 / 10">nadie la consume: es el historial de cambios para revisar</div>
      </div>
    </figure>
  </div>
</template>

<style scoped>
.diagrama { display: grid; gap: var(--cep-sp-4); }
.leyenda { list-style: none; padding: 0; margin: 0; display: flex; flex-wrap: wrap; gap: var(--cep-sp-2) var(--cep-sp-5); font-size: var(--cep-fs-sm); color: var(--cep-muted); }
.leyenda li { display: flex; align-items: center; gap: var(--cep-sp-2); }
.muestra { display: inline-block; width: 1.1rem; height: 0.8rem; border-radius: 3px; }

.flujo { margin: 0; padding: var(--cep-sp-4); border: 1px solid var(--cep-line); border-radius: var(--cep-r-lg); background: var(--cep-surface); overflow-x: auto; }
figcaption { font-weight: 600; margin-bottom: var(--cep-sp-3); display: flex; align-items: center; gap: var(--cep-sp-2); }
.num { display: inline-grid; place-items: center; width: 1.5rem; height: 1.5rem; border-radius: 50%; background: var(--cep-ink); color: var(--cep-surface); font-size: var(--cep-fs-sm); }

/* 5 piezas y 4 flechas por fila; las columnas de piezas miden lo mismo en los dos flujos. */
.plano {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 2.2rem minmax(0, 1fr) 6.5rem minmax(0, 1.3fr) 2.2rem minmax(0, 1.1fr) 2.2rem minmax(0, 0.8fr);
  align-items: center;
  row-gap: var(--cep-sp-1);
  /* Bajo este ancho el plano se desplaza de lado en vez de apretarse. */
  min-width: 980px;
}
.nodo {
  display: flex; flex-direction: column; gap: 2px; justify-content: center;
  min-height: 3.4rem; padding: var(--cep-sp-2) var(--cep-sp-3);
  border-radius: var(--cep-r-md); border: 1.5px solid; font: inherit; font-size: var(--cep-fs-sm);
  text-align: left; color: var(--cep-ink); background: transparent; overflow-wrap: anywhere;
}
.nodo small { color: var(--cep-muted); font-size: 0.75rem; font-family: var(--cep-font-body, inherit); }
button.nodo { cursor: pointer; }
button.nodo:hover, button.nodo:focus-visible { outline: 2px solid var(--cep-accent); outline-offset: 2px; }
.servicio, .muestra.servicio { border-color: var(--cep-line-fuerte); border-style: dashed; }
.muestra.servicio { border: 1.5px dashed var(--cep-line-fuerte); }
.cola, .muestra.cola { border-color: var(--cep-accent); background: color-mix(in srgb, var(--cep-accent) 9%, transparent); font-family: var(--cep-font-mono); }
.topico, .muestra.topico { border-color: var(--cep-ink); background: var(--cep-sunken); font-family: var(--cep-font-mono); }
.dlq, .muestra.dlq { border-color: var(--cep-alerta); font-family: var(--cep-font-mono); }
.dlq.con, .muestra.dlq { background: color-mix(in srgb, var(--cep-alerta) 10%, transparent); }
.dlq.con small { color: var(--cep-alerta); font-weight: 700; }
.fin { border-color: var(--cep-exito); background: color-mix(in srgb, var(--cep-exito) 9%, transparent); }

.flecha { text-align: center; color: var(--cep-muted); font-size: 1.1rem; line-height: 1; }
.flecha.regla { display: grid; gap: 2px; }
.flecha small { font-size: 0.7rem; font-style: italic; line-height: 1.2; }
.baja { font-size: 0.75rem; color: var(--cep-muted); padding-left: var(--cep-sp-3); }
.codo {
  align-self: stretch; margin-left: 50%; margin-bottom: 1.7rem;
  border-left: 1.5px solid var(--cep-line-fuerte); border-bottom: 1.5px solid var(--cep-line-fuerte);
  border-bottom-left-radius: 6px;
}
.nota { font-size: var(--cep-fs-sm); color: var(--cep-muted); font-style: italic; padding-left: var(--cep-sp-3); }
</style>
