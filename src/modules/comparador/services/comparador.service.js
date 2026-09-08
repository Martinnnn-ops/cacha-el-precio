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
// Product Service y la forma que devuelve. El store llama a estas funciones y
// recibe objetos de dominio ya listos; nunca ve una respuesta HTTP.

const VERSION = '1.0'

const RETARDO_MOCK = 250

function simularRed(datos) {
  return new Promise((resolve) => setTimeout(() => resolve(datos), RETARDO_MOCK))
}

// Categorías y tiendas vienen dentro de cada producto. Se comparte la primera
// petición para que cargar la portada y sus filtros no duplique GET /products.
let cacheFilas = null

async function filasProductos() {
  if (cacheFilas) return cacheFilas

  cacheFilas = http.get('/products', { version: VERSION }).catch((error) => {
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

/** Las categorías se derivan del campo `category` de GET /api/products. */
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
 * GET /api/products. Los filtros del comparador se aplican en el store para
 * poder combinar texto, categoría, tienda, talla y precio sin múltiples viajes.
 */
export async function obtenerProductos() {
  if (USAR_MOCK) return simularRed(PRODUCTOS_MOCK)

  return adaptarProductos(await filasProductos())
}

/** GET /api/products/:id */
export async function obtenerProducto(id) {
  if (USAR_MOCK) {
    return simularRed(PRODUCTOS_MOCK.find((p) => p.id === id) ?? null)
  }

  const fila = await http.get(`/products/${encodeURIComponent(id)}`, {
    version: VERSION,
  })

  return adaptarProducto(fila)
}
