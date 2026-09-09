import http, { USAR_MOCK } from '@/core/api/http'
import {
  FUENTE_UNICA,
  adaptarCategorias,
  adaptarProducto,
  adaptarProductos,
  adaptarTiendas,
} from '@/modules/comparador/services/producto.adapter'
import { PRODUCTOS_MOCK } from '@/modules/comparador/data/productos.mock'
import { TIENDAS } from '@/modules/comparador/data/tiendas'

// Capa de servicios del módulo: aquí y sólo aquí se sabe cómo son las URLs del
// backend y la forma que devuelve. El store llama a estas funciones y recibe
// objetos de dominio ya listos; nunca ve una respuesta HTTP.
//
// Se habla el contrato del BFF (`/productos`, en español) y no el del Product
// Service (`/api/products`), aunque el gateway publique los dos. El motivo
// está en el ADR-021: si el navegador pide el mismo path que publica el
// servicio interno, el gateway queda de intermediario transparente y el
// nombre de ese servicio viaja hasta el navegador de cada usuario. El día que
// Product Service renombre una ruta, el cambio llega a gente que tiene la
// página abierta — y el frontend se despliega aparte, así que ni siquiera se
// actualizan a la vez.

const VERSION = '1.0'

const RETARDO_MOCK = 250

function simularRed(datos) {
  return new Promise((resolve) => setTimeout(() => resolve(datos), RETARDO_MOCK))
}

// Categorías y tiendas vienen dentro de cada producto. Se comparte la primera
// petición para que cargar la portada y sus filtros no duplique GET /productos.
let cacheFilas = null

async function filasProductos() {
  if (cacheFilas) return cacheFilas

  cacheFilas = http.get('/productos', { version: VERSION }).catch((error) => {
    cacheFilas = null
    throw error
  })

  return cacheFilas
}

/**
 * Fuentes de precio disponibles.
 *
 * Con datos de ejemplo son las cinco tiendas. Contra la API real se derivan
 * del campo `store` de los productos.
 */
export async function obtenerTiendas() {
  if (USAR_MOCK) return simularRed(TIENDAS)

  const tiendas = adaptarTiendas(await filasProductos())

  return tiendas.length > 0 ? tiendas : [FUENTE_UNICA]
}

/** Las categorías se derivan del campo `category` de GET /productos. */
export async function obtenerCategorias() {
  if (USAR_MOCK) {
    return simularRed(
      [...new Set(PRODUCTOS_MOCK.map((p) => p.categoria))].map((n) => ({
        id: n,
        nombre: n,
      })),
    )
  }

  return adaptarCategorias(await filasProductos())
}

/**
 * GET /productos. Los filtros del comparador se aplican en el store para
 * poder combinar texto, categoría, tienda, talla y precio sin múltiples viajes.
 */
export async function obtenerProductos() {
  if (USAR_MOCK) return simularRed(PRODUCTOS_MOCK)

  return adaptarProductos(await filasProductos())
}

/** GET /productos/:id */
export async function obtenerProducto(id) {
  if (USAR_MOCK) {
    return simularRed(PRODUCTOS_MOCK.find((p) => p.id === id) ?? null)
  }

  const fila = await http.get(`/productos/${encodeURIComponent(id)}`, {
    version: VERSION,
  })

  return adaptarProducto(fila)
}
