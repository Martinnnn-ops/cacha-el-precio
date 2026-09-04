<script setup>
import { computed, ref } from 'vue'

import { useTiendas } from '@/modules/comparador/composables/useTiendas'
import EnlaceTienda from '@/shared/components/EnlaceTienda.vue'
import { formatearPrecio } from '@/shared/utils/formato'
import { descuento } from '@/shared/utils/precios'

// El bloque donde se decide la compra.
//
// Antes era una tabla compacta de cuatro columnas. Aquí cada tienda es una
// fila con espacio para lo que de verdad cambia la decisión: el medio de pago
// —un precio que sólo aplica con la tarjeta de la tienda no es el mismo
// precio— y la condición de la prenda.
const props = defineProps({
  ofertas: { type: Array, default: () => [] },
  // Con una sola fuente de precio no hay nada que elegir: cambia el título y
  // se ocultan los controles de orden.
  comparando: { type: Boolean, default: true },
})

const { nombreTienda, colorTienda } = useTiendas()

const orden = ref('precio')

const CONDICIONES = {
  nueva: null, // lo normal no necesita etiqueta
  'ultima-talla': { texto: 'Últimas tallas', tono: 'aviso' },
  'segunda-mano': { texto: 'Segunda mano', tono: 'aviso' },
  outlet: { texto: 'Outlet', tono: 'aviso' },
}

const listadas = computed(() => {
  const copia = [...props.ofertas]

  return copia.sort((a, b) => {
    // Lo que no se puede comprar va al final, se ordene por lo que se ordene:
    // una oferta agotada no compite por el primer puesto aunque sea la barata.
    if (a.stock !== b.stock) return a.stock ? -1 : 1

    if (orden.value === 'tienda') {
      return nombreTienda(a.tienda).localeCompare(nombreTienda(b.tienda))
    }

    return a.precio - b.precio
  })
})

const masBarata = computed(() => listadas.value.find((o) => o.stock) ?? null)

function condicionDe(oferta) {
  return CONDICIONES[oferta.condicion] ?? null
}
</script>

<template>
  <section class="tiendas" aria-labelledby="elige-tienda">
    <header class="tiendas__cabecera">
      <h2 id="elige-tienda" class="display tiendas__titulo">
        {{ comparando ? 'Elige tu tienda' : 'Dónde comprarla' }}
      </h2>

      <label v-if="comparando && listadas.length > 1" class="orden">
        <span class="orden__etiqueta mono">Ordenar por</span>
        <select v-model="orden" class="orden__control">
          <option value="precio">Precio</option>
          <option value="tienda">Tienda</option>
        </select>
      </label>
    </header>

    <ul class="tiendas__lista">
      <li
        v-for="oferta in listadas"
        :key="oferta.tienda"
        class="oferta"
        :class="{
          'oferta--mejor': comparando && oferta.tienda === masBarata?.tienda,
          'oferta--agotada': !oferta.stock,
        }"
      >
        <div class="oferta__quien">
          <p class="oferta__tienda">
            <span
              class="oferta__punto"
              :style="{ background: colorTienda(oferta.tienda) }"
            />
            {{ nombreTienda(oferta.tienda) }}
          </p>

          <p class="oferta__etiquetas">
            <span
              v-if="condicionDe(oferta)"
              class="etiqueta etiqueta--aviso"
            >
              {{ condicionDe(oferta).texto }}
            </span>

            <span v-if="oferta.medioPago" class="etiqueta mono">
              {{ oferta.medioPago }}
            </span>
          </p>
        </div>

        <div class="oferta__precio">
          <p class="oferta__monto">{{ formatearPrecio(oferta.precio) }}</p>

          <!-- El precio normal sólo se tacha si de verdad es mayor: tachar uno
               igual o menor sugiere un descuento que no existe. -->
          <p v-if="descuento(oferta)" class="oferta__normal mono">
            <span class="oferta__tachado">
              {{ formatearPrecio(oferta.precioLista) }}
            </span>
            <span class="oferta__rebaja">−{{ descuento(oferta) }}%</span>
          </p>
        </div>

        <div class="oferta__accion">
          <span v-if="!oferta.stock" class="mono oferta__sinstock">Agotado</span>

          <EnlaceTienda
            v-else
            :url="oferta.url"
            :tienda="nombreTienda(oferta.tienda)"
            tamano="chico"
          />
        </div>
      </li>
    </ul>

    <p v-if="listadas.length === 0" class="muted tiendas__vacio">
      Ninguna tienda la tiene publicada ahora mismo.
    </p>
  </section>
</template>

<style scoped>
.tiendas__cabecera {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--cep-sp-4);
  flex-wrap: wrap;
  margin-bottom: var(--cep-sp-3);
}
.tiendas__titulo {
  margin: 0;
  font-size: var(--cep-fs-xl);
}

.orden {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-2);
}
.orden__etiqueta {
  font-size: var(--cep-fs-2xs);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--cep-muted);
}
.orden__control {
  min-height: var(--cep-control-h-sm);
  padding: 0 var(--cep-sp-2);
  background: var(--cep-surface);
  color: var(--cep-ink);
  border: 1px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-sm);
  font-size: var(--cep-fs-sm);
  cursor: pointer;
}

.tiendas__lista {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-2);
}

.oferta {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: var(--cep-sp-2) var(--cep-sp-4);
  align-items: center;
  padding: var(--cep-sp-3) var(--cep-sp-4);
  background: var(--cep-surface);
  border-radius: var(--cep-r-lg);
  box-shadow: var(--cep-shadow-1);
}
@media (min-width: 560px) {
  .oferta {
    grid-template-columns: 1fr auto auto;
  }
}
/* La más barata se marca con un borde de acento a la izquierda, no con un
   fondo de color: se distingue sin gritar. */
.oferta--mejor {
  border-left: 3px solid var(--cep-exito);
}
.oferta--agotada {
  opacity: 0.6;
}

.oferta__quien {
  min-width: 0;
}
.oferta__tienda {
  display: flex;
  align-items: center;
  gap: var(--cep-sp-2);
  margin: 0;
  font-size: var(--cep-fs-base);
  font-weight: 600;
}
.oferta__punto {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  flex: none;
}
.oferta__etiquetas {
  display: flex;
  flex-wrap: wrap;
  gap: var(--cep-sp-15);
  margin: var(--cep-sp-15) 0 0;
}
.etiqueta {
  padding: 2px var(--cep-sp-2);
  border: 1px solid var(--cep-line-fuerte);
  border-radius: var(--cep-r-pill);
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}
.etiqueta--aviso {
  border-color: var(--cep-rate);
  color: var(--cep-rate);
}

.oferta__precio {
  text-align: right;
}
.oferta__monto {
  margin: 0;
  font-family: var(--cep-font-display);
  font-size: var(--cep-fs-xl);
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}
.oferta--mejor .oferta__monto {
  color: var(--cep-exito);
}
.oferta__normal {
  display: flex;
  align-items: baseline;
  justify-content: flex-end;
  gap: var(--cep-sp-15);
  margin: 2px 0 0;
  font-size: var(--cep-fs-xs);
}
.oferta__tachado {
  color: var(--cep-muted);
  text-decoration: line-through;
  text-decoration-color: var(--cep-alerta);
}
.oferta__rebaja {
  color: var(--cep-alerta);
  font-weight: 600;
}

.oferta__accion {
  grid-column: 1 / -1;
  justify-self: stretch;
}
@media (min-width: 560px) {
  .oferta__accion {
    grid-column: auto;
    justify-self: end;
  }
}
.oferta__sinstock {
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}

.tiendas__vacio {
  margin: 0;
  padding: var(--cep-sp-6) 0;
  font-size: var(--cep-fs-sm);
}
</style>
