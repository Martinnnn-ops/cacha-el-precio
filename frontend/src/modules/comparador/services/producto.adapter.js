// Traducción entre el Product Service y el modelo del comparador.
//
//   API   Product { id, canonicalKey, name, brand, category, description,
//                   image, visits, createdAt, offers[] }
//         Offer   { id, externalId, store, price, sizes[], url, image,
//                   active, updatedAt }
//   App   Producto { id, nombre, categoria, precios[], historial[] }

// Fuente de respaldo para productos antiguos que no identifican la tienda.
export const FUENTE_UNICA = {
  id: 'catalogo',
  nombre: 'Precio publicado',
  color: '#0b5cad',
}

// Cuántos días pasaron desde la fecha (ISO-8601) que da la API. Si no viene o
// no se puede leer, null: el frontend oculta la antigüedad en vez de mentir.
function diasDesde(fecha) {
  if (!fecha) return null

  const instante = Date.parse(fecha)

  if (!Number.isFinite(instante)) return null

  const dias = Math.floor((Date.now() - instante) / 86_400_000)

  return dias >= 0 ? dias : null
}

const COLORES_CONOCIDOS = {
  ripley: '#6b2d8c',
  paris: '#0b5cad',
  zara: '#2b2b2b',
  hym: '#c0392b',
  mango: '#8a6a2f',
}
const COLORES_TIENDA = ['#0b5cad', '#a8325e', '#1f7a4d', '#8a5a14', '#5d4bb7']

function texto(valor) {
  return typeof valor === 'string' ? valor.trim() : ''
}

export function idTienda(nombre) {
  const limpio = texto(nombre).toLowerCase()

  if (limpio === 'h&m' || limpio === 'h & m') return 'hym'

  return limpio
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
}

function tallasDisponibles(sizes) {
  if (Array.isArray(sizes)) {
    return [...new Set(sizes.map((talla) => texto(String(talla)).toUpperCase()).filter(Boolean))]
  }

  if (!sizes || typeof sizes !== 'object') return []

  return ['XS', 'S', 'M', 'L', 'XL', 'XXL'].filter(
    (talla) => sizes[talla] === true || sizes[talla.toLowerCase()] === true,
  )
}

// Durante el despliegue puede convivir brevemente el contrato nuevo con la
// forma plana anterior. Tratar la propia fila como oferta mantiene la web
// compatible mientras se renuevan las instancias del backend.
function ofertasDe(producto) {
  return Array.isArray(producto?.offers) ? producto.offers : [producto]
}

export function adaptarCategorias(productos = []) {
  return [...new Set(productos.map((p) => texto(p?.category)).filter(Boolean))]
    .sort((a, b) => a.localeCompare(b, 'es'))
    .map((nombre) => ({ id: nombre, nombre }))
}

export function adaptarTiendas(productos = []) {
  const nombres = new Map()
  let hayProductoSinTienda = false

  productos.flatMap(ofertasDe).forEach((oferta) => {
    const nombre = texto(oferta?.store)
    const id = idTienda(nombre)

    if (!id) {
      hayProductoSinTienda = true
    } else if (!nombres.has(id)) {
      nombres.set(id, nombre)
    }
  })

  if (nombres.size === 0) return [FUENTE_UNICA]

  const tiendas = [...nombres.entries()]
    .sort(([, a], [, b]) => a.localeCompare(b, 'es'))
    .map(([id, nombre], indice) => ({
      id,
      nombre,
      color: COLORES_CONOCIDOS[id] ?? COLORES_TIENDA[indice % COLORES_TIENDA.length],
    }))

  return hayProductoSinTienda ? [FUENTE_UNICA, ...tiendas] : tiendas
}

export function adaptarProductos(filas = []) {
  return filas
    .filter((fila) => texto(fila?.name))
    .map((fila) => {
      const ofertasOriginales = ofertasDe(fila)
      const ofertas = ofertasOriginales
        .filter((oferta) => Number.isFinite(Number(oferta?.price)))
        .map((oferta) => ({
          tienda: idTienda(oferta.store) || FUENTE_UNICA.id,
          precio: Number(oferta.price),
          // El backend aún no conserva precio de lista ni historial.
          precioLista: null,
          stock: oferta.active !== false,
          url: texto(oferta.url) || null,
          tallas: tallasDisponibles(oferta.sizes),
          imagen: texto(oferta.image) || null,
        }))

      const primera = ofertasOriginales[0] ?? {}
      const ultimaActualizacion = ofertasOriginales
        .map((oferta) => oferta?.updatedAt)
        .filter(Boolean)
        .sort()
        .at(-1)

      return {
        id: String(fila.id),
        nombre: fila.name.trim(),
        descripcion: texto(fila.description),
        marca: texto(fila.brand),
        categoria: texto(fila.category),
        imagen: texto(fila.image) || ofertas.find((oferta) => oferta.imagen)?.imagen || null,
        vistas: Number(fila.visits ?? 0) || 0,
        agregadoHace: diasDesde(fila.createdAt ?? ultimaActualizacion),
        codigo: texto(fila.canonicalKey) || texto(primera.externalId),
        specs: {},
        pros: [],
        contras: [],
        precios: ofertas,
        historial: [],
      }
    })
    .filter((producto) => producto.precios.length > 0)
}

export function adaptarProducto(fila) {
  if (!fila) return null

  return adaptarProductos([fila])[0] ?? null
}
