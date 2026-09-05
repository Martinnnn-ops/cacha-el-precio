<script setup>
import { computed } from 'vue'
import { storeToRefs } from 'pinia'

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import { useTiendas } from '@/modules/comparador/composables/useTiendas'
import { nombreParte } from '@/modules/outfits/data/partes'
import { useOutfitStore } from '@/modules/outfits/store/outfit.store'
import BaseButton from '@/shared/components/BaseButton.vue'
import BaseTicket from '@/shared/components/BaseTicket.vue'
import EnlaceTienda from '@/shared/components/EnlaceTienda.vue'
import { formatearPrecio } from '@/shared/utils/formato'

// Desglose y totales del outfit. Es la pantalla que responde a la pregunta que
// trae a alguien aquí: dónde compro cada cosa y cuánto me sale.
const outfit = useOutfitStore()
const { desglose, total, tiendasImplicadas, mejorTiendaUnica, ahorroRepartiendo } =
  storeToRefs(outfit)

const { nombreTienda, colorTienda } = useTiendas()

// Con una sola fuente de precio no hay dos formas de comprar: enseñar el mismo
// número dos veces —una como "en 1 tienda" y otra como "todo en una tienda"—
// no informa de nada y da a entender una comparación que no existe.
const { comparacionDisponible } = storeToRefs(useComparadorStore())

const cuantasTiendas = computed(() => tiendasImplicadas.value.size)
</script>

<template>
  <BaseTicket class="resumen">
    <h2 class="display resumen__titulo">Tu outfit</h2>

    <p v-if="desglose.length === 0" class="muted resumen__vacio">
      Ve eligiendo prendas y aquí verás cuánto cuesta y dónde comprar cada una.
    </p>

    <template v-else>
      <ul class="resumen__lista">
        <li v-for="{ parte, producto, oferta } in desglose" :key="parte" class="fila">
          <span class="fila__parte mono">{{ nombreParte(parte) }}</span>

          <span class="fila__prenda">
            <span class="fila__nombre">{{ producto.nombre }}</span>

            <span
              v-if="oferta && comparacionDisponible"
              class="fila__tienda mono"
            >
              <i :style="{ background: colorTienda(oferta.tienda) }" />
              {{ nombreTienda(oferta.tienda) }}
            </span>
          </span>

          <span v-if="oferta" class="fila__precio mono">
            {{ formatearPrecio(oferta.precio) }}
          </span>

          <span v-if="oferta" class="fila__ir">
            <EnlaceTienda
              :url="oferta.url"
              :tienda="nombreTienda(oferta.tienda)"
              tamano="chico"
            />
          </span>
          <span v-else class="fila__precio mono muted">sin stock</span>
        </li>
      </ul>

      <div class="divisor" />

      <!-- Las dos formas de comprarlo, con su letra pequeña. Un total de "lo
           más barato pieza por pieza" que no diga que son varias tiendas puede
           salir más caro de verdad, por los despachos. -->
      <div class="opcion">
        <div class="opcion__cabecera">
          <span class="opcion__etiqueta">
            {{ comparacionDisponible ? 'Cada pieza donde está más barata' : 'Total del outfit' }}
          </span>
          <strong class="opcion__monto">{{ formatearPrecio(total) }}</strong>
        </div>
        <p v-if="comparacionDisponible" class="mono muted opcion__nota">
          En {{ cuantasTiendas }}
          {{ cuantasTiendas === 1 ? 'tienda' : 'tiendas distintas' }}
        </p>
      </div>

      <div v-if="comparacionDisponible && mejorTiendaUnica" class="opcion">
        <div class="opcion__cabecera">
          <span class="opcion__etiqueta">Todo en una sola tienda</span>
          <strong class="opcion__monto">
            {{ formatearPrecio(mejorTiendaUnica.total) }}
          </strong>
        </div>
        <p class="mono muted opcion__nota">
          Todo en {{ nombreTienda(mejorTiendaUnica.tienda) }}: un solo envío
          que cobra esa tienda
        </p>
      </div>

      <p
        v-else-if="comparacionDisponible && desglose.length > 1"
        class="mono muted opcion__nota"
      >
        Ninguna tienda tiene el outfit completo con stock.
      </p>

      <p
        v-if="comparacionDisponible && ahorroRepartiendo > 0"
        class="resumen__cierre"
      >
        Comprando cada pieza donde está más barata te ahorras
        <strong>{{ formatearPrecio(ahorroRepartiendo) }}</strong>, pero harías
        {{ cuantasTiendas }} compras en tiendas distintas y cada una cobra su
        envío. Con eso decides tú.
      </p>

      <BaseButton
        variante="secundario"
        tamano="chico"
        bloque
        @click="outfit.limpiar()"
      >
        Empezar de nuevo
      </BaseButton>
    </template>
  </BaseTicket>
</template>

<style scoped>
.resumen {
  position: sticky;
  top: var(--cep-sp-4);
}
.resumen__titulo {
  margin: 0 0 var(--cep-sp-4);
  font-size: var(--cep-fs-xl);
}
.resumen__vacio {
  margin: 0;
  font-size: var(--cep-fs-sm);
}

.resumen__lista {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--cep-sp-3);
}
.fila {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: var(--cep-sp-1) var(--cep-sp-3);
  align-items: baseline;
}
.fila__parte {
  grid-column: 1 / -1;
  font-size: var(--cep-fs-2xs);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--cep-muted);
}
.fila__prenda {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}
.fila__nombre {
  font-size: var(--cep-fs-sm);
}
.fila__tienda {
  display: inline-flex;
  align-items: center;
  gap: var(--cep-sp-15);
  font-size: var(--cep-fs-xs);
  color: var(--cep-muted);
}
.fila__tienda i {
  width: 7px;
  height: 7px;
  border-radius: 50%;
}
.fila__ir {
  grid-column: 1 / -1;
  margin-top: var(--cep-sp-1);
}

.fila__precio {
  font-size: var(--cep-fs-sm);
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.opcion {
  margin-bottom: var(--cep-sp-3);
}
.opcion__cabecera {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--cep-sp-3);
}
.opcion__etiqueta {
  font-size: var(--cep-fs-sm);
}
.opcion__monto {
  font-family: var(--cep-font-display);
  font-size: var(--cep-fs-lg);
  font-variant-numeric: tabular-nums;
}
.opcion__nota {
  margin: 2px 0 0;
  font-size: var(--cep-fs-xs);
}

.resumen__cierre {
  margin: var(--cep-sp-4) 0;
  padding: var(--cep-sp-25) var(--cep-sp-3);
  background: var(--cep-bg);
  border-left: 3px solid var(--cep-exito);
  border-radius: var(--cep-r-sm);
  font-size: var(--cep-fs-sm);
  line-height: var(--cep-lh-snug);
}
.resumen__cierre strong {
  color: var(--cep-exito);
}
</style>
