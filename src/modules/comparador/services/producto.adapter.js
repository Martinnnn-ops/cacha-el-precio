// Traducción entre el Product Service y el modelo del comparador.
//
//   API   Product { id, externalId, store, name, brand, category, price,
//                   sizes, description, url, image, active }
//   App   Producto { id, nombre, categoria, precios[], historial[] }
//
// ┌─ LÍMITE DEL BACKEND ACTUAL ───────────────────────────────────────────┐
// │ Cada fila representa una oferta publicada por una tienda. El backend  │
// │ todavía no entrega precio de lista ni historial, por lo que esos      │
// │ campos quedan vacíos y la interfaz oculta lo que no puede calcular.   │
// │                                                                        │
// │ Lo que falta en el backend es una tabla de ofertas:                   │
// │   Oferta { productoId, tiendaId, precio, precioLista, stock, fecha }  │
// │ Con eso el adaptador agrupa por productoId y todo lo demás ya está    │
// │ escrito: el cálculo del más barato, el ahorro y el historial.         │
// └───────────────────────────────────────────────────────────────────────┘

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
  if (!sizes || typeof sizes !== 'object') return []

  return ['XS', 'S', 'M', 'L', 'XL', 'XXL'].filter(
    (talla) => sizes[talla] === true || sizes[talla.toLowerCase()] === true,
  )
}

export function adaptarCategorias(productos = []) {
  return [...new Set(productos.map((p) => texto(p?.category)).filter(Boolean))]
    .sort((a, b) => a.localeCompare(b, 'es'))
    .map((nombre) => ({ id: nombre, nombre }))
}

export function adaptarTiendas(productos = []) {
  const nombres = new Map()
  let hayProductoSinTienda = false

  productos.forEach((producto) => {
    const nombre = texto(producto?.store)
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
    .filter((f) => texto(f?.name) && Number.isFinite(Number(f.price)))
    .map((fila) => ({
      id: String(fila.id),
      nombre: fila.name.trim(),
      descripcion: texto(fila.description),
      marca: texto(fila.brand),
      categoria: texto(fila.category),
      imagen: texto(fila.image) || null,
      visitas: 0,
      agregadoHace: diasDesde(fila.createdAt),
      // Campos de ficha que la API todavía no expone. Si algún día los trae,
      // los bloques de la vista aparecen solos.
      codigo: texto(fila.externalId),
      specs: {},
      pros: [],
      contras: [],
      precios: [
        {
          tienda: idTienda(fila.store) || FUENTE_UNICA.id,
          precio: Number(fila.price),
          // Sin precio de lista no se puede calcular descuento: la tarjeta
          // simplemente no lo pinta.
          precioLista: null,
          stock: fila.active !== false,
          url: texto(fila.url) || null,
          tallas: tallasDisponibles(fila.sizes),
        },
      ],
      historial: [],
    }))
}

export function adaptarProducto(fila) {
  if (!fila) return null

  return adaptarProductos([fila])[0] ?? null
}
