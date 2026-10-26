<script setup>
import { onMounted, ref } from 'vue'
import * as api from '@/modules/admin/services/mensajeria.service'

// «Probar el flujo 2» en un clic: publica en el tópico un cambio inventado de
// una prenda real, igual que lo haría product-service, y desde ahí recorre todo
// de verdad (regla, cola, notification-service, correo). No cambia el precio
// del catálogo. Tres casos fijos, elegidos para la demo: uno que pasa la regla,
// uno que la regla filtra y otro tipo de aviso.
const PRODUCTO = 1

const CASOS = [
  { id: 'baja30', titulo: 'Bajó 30%', espera: 'Pasa la regla: llega el correo', tipo: 'precio.bajo', descuento: 30, llega: true },
  { id: 'baja5', titulo: 'Bajó 5%', espera: 'La regla lo filtra (pide 10% o más): no hay correo', tipo: 'precio.bajo', descuento: 5, llega: false },
  { id: 'agotada', titulo: 'Se agotó', espera: 'Pasa la regla: llega el correo de agotado', tipo: 'oferta.agotada', descuento: null, llega: true },
]

const emit = defineEmits(['publicado'])
const producto = ref(null)
const enviando = ref(null)
const error = ref('')

onMounted(async () => {
  try {
    producto.value = await api.verProducto(PRODUCTO)
  } catch {
    error.value = `No se encontró el producto ${PRODUCTO} en el catálogo.`
  }
})

const precio = () => {
  const o = producto.value?.offers?.find((x) => x.active) ?? producto.value?.offers?.[0]
  return o ? `${o.store} · $${o.price.toLocaleString('es-CL')}` : ''
}

async function probar(caso) {
  enviando.value = caso.id
  error.value = ''
  try {
    // Sin alguien que siga la prenda no hay a quién mandarle el correo: el
    // admin la sigue (si ya la seguía, no cambia nada).
    await api.seguirProducto(PRODUCTO)
    const r = await api.simularAviso({ productId: PRODUCTO, tipo: caso.tipo, descuento: caso.descuento })
    emit('publicado', { eventId: r.eventId, caso })
  } catch (e) {
    error.value = api.leerError(e).mensaje
  } finally {
    enviando.value = null
  }
}
</script>

<template>
  <div class="probar">
    <div class="encabezado">
      <h3>Probar el flujo 2</h3>
      <p v-if="producto" class="muted">
        Con <strong>{{ producto.name }}</strong> <span class="nowrap">({{ precio() }})</span>.
        Se publica un cambio inventado en el tópico; el precio real no se toca.
      </p>
    </div>
    <div class="casos">
      <button v-for="c in CASOS" :key="c.id" type="button" class="caso" :class="c.llega ? 'llega' : 'filtra'"
        :disabled="!!enviando || !producto" @click="probar(c)">
        <strong>{{ enviando === c.id ? 'Publicando…' : c.titulo }}</strong>
        <span>{{ c.espera }}</span>
      </button>
    </div>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
  </div>
</template>

<style scoped>
.probar { display: grid; gap: var(--cep-sp-3); padding: var(--cep-sp-4); border: 1px solid var(--cep-line); border-radius: var(--cep-r-lg); background: var(--cep-surface); }
h3 { margin: 0; font-size: var(--cep-fs-md); }
.encabezado p { margin: var(--cep-sp-1) 0 0; font-size: var(--cep-fs-sm); }
.nowrap { white-space: nowrap; }
.casos { display: grid; gap: var(--cep-sp-2); grid-template-columns: minmax(0, 1fr); }
@media (min-width: 720px) { .casos { grid-template-columns: repeat(3, minmax(0, 1fr)); } }
.caso {
  display: grid; gap: 2px; text-align: left; cursor: pointer; font: inherit; color: var(--cep-ink);
  padding: var(--cep-sp-3); border-radius: var(--cep-r-md); border: 1.5px solid; background: transparent;
}
.caso strong { font-size: var(--cep-fs-md); }
.caso span { font-size: var(--cep-fs-sm); color: var(--cep-muted); }
.caso.llega { border-color: var(--cep-exito); }
.caso.filtra { border-color: var(--cep-line-fuerte); border-style: dashed; }
.caso:hover:not(:disabled) { background: var(--cep-sunken); }
.caso:disabled { opacity: 0.55; cursor: default; }
.caso:focus-visible { outline: 2px solid var(--cep-accent); outline-offset: 2px; }
.error { color: var(--cep-alerta); font-size: var(--cep-fs-sm); margin: 0; }
</style>
