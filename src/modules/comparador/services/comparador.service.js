import http, { USAR_MOCK } from '@/core/api/http'
import {
  FUENTE_UNICA,
  adaptarCategorias,
  adaptarProducto,
  adaptarProductos,
} from '@/modules/comparador/services/producto.adapter'
import { PRODUCTOS_MOCK } from '@/modules/comparador/data/productos.mock'
import { TIENDAS } from '@/modules/comparador/data/tiendas'

// Capa de servicios del módulo: aquí y sólo aquí se sabe cómo son las URLs del
// Product Service y la forma que devuelve. El store llama a estas funciones y
// recibe objetos de dominio ya listos; nunca ve una respuesta HTTP.

// Versión de la API por endpoint. Sale de leer los tres OpenAPI del servicio:
//   0.1.0  CRUD completo — es la ÚNICA que declara GET /productos/{id}
//   0.2.0  sólo listados ordenados
//   0.3.0  listado con filtros (catalogoId, soloActivos)
const V_LISTADO = '0.3.0'
const V_DETALLE = '0.1.0'

const RETARDO_MOCK = 250

function simularRed(datos) {
  return new Promise((resolve) => setTimeout(() => resolve(datos), RETARDO_MOCK))
}

// Los catálogos se piden una vez y se reutilizan: hacen falta para poner el
// nombre de la categoría en cada producto.
let cacheCatalogos = null

async function catalogos() {
  if (cacheCatalogos) return cacheCatalogos

  cacheCatalogos = await http.get('/catalogos', { version: V_LISTADO })

  return cacheCatalogos
}

/**
 * Fuentes de precio disponibles.
 *
 * Con datos de ejemplo son las cinco tiendas. Contra la API real hay UNA sola,
 * porque el backend todavía no guarda precios por tienda (ver el adaptador).
 */
export async function obtenerTiendas() {
  if (USAR_MOCK) return simularRed(TIENDAS)

  return [FUENTE_UNICA]
}

/** GET /catalogos → categorías del catálogo. */
export async function obtenerCategorias() {
  if (USAR_MOCK) {
    return simularRed(
      [...new Set(PRODUCTOS_MOCK.map((p) => p.categoria))].map((n) => ({
        id: n,
        nombre: n,
      })),
    )
  }

  return adaptarCategorias(await catalogos())
}

/**
 * GET /productos
 * @param {{ catalogoId?: number, soloActivos?: boolean }} filtros
 */
export async function obtenerProductos(filtros = {}) {
  if (USAR_MOCK) return simularRed(PRODUCTOS_MOCK)

  const params = {}

  // Parámetros de la versión 0.3.0. En 0.1.0 y 0.2.0 se ignoran sin error.
  if (filtros.catalogoId != null) params.catalogoId = filtros.catalogoId
  if (filtros.soloActivos != null) params.soloActivos = filtros.soloActivos

  const [filas, cats] = await Promise.all([
    http.get('/productos', { params, version: V_LISTADO }),
    catalogos(),
  ])

  return adaptarProductos(filas, cats)
}

/** GET /productos/:id */
export async function obtenerProducto(id) {
  if (USAR_MOCK) {
    return simularRed(PRODUCTOS_MOCK.find((p) => p.id === id) ?? null)
  }

  const [fila, cats] = await Promise.all([
    // El detalle SÓLO existe en 0.1.0. Con la versión del listado responde 404.
    http.get(`/productos/${encodeURIComponent(id)}`, { version: V_DETALLE }),
    catalogos(),
  ])

  return adaptarProducto(fila, cats)
}
