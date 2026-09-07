// Traducción entre el Product Service y el modelo del comparador.
//
//   API   Producto { id, nombre, descripcion, precio, catalogoId, activo }
//         Catalogo { id, nombre, descripcion }          → una CATEGORÍA
//   App   Producto { id, nombre, categoria, precios[], historial[] }
//
// ┌─ LÍMITE DEL BACKEND ACTUAL ───────────────────────────────────────────┐
// │ La API guarda UN precio por producto y no tiene noción de tienda: no  │
// │ hay dos precios del mismo producto que comparar entre sí. Mientras    │
// │ sea así, la aplicación puede LISTAR y BUSCAR productos por categoría, │
// │ pero no puede comparar precios, que es su razón de ser.               │
// │                                                                        │
// │ Lo que falta en el backend es una tabla de ofertas:                   │
// │   Oferta { productoId, tiendaId, precio, precioLista, stock, fecha }  │
// │ Con eso el adaptador agrupa por productoId y todo lo demás ya está    │
// │ escrito: el cálculo del más barato, el ahorro y el historial.         │
// └───────────────────────────────────────────────────────────────────────┘

// Fuente única mientras no haya tiendas. Se nombra explícitamente para que en
// pantalla nunca aparezca un "más barato en …" que no significa nada.
export const FUENTE_UNICA = {
  id: 'catalogo',
  nombre: 'Precio publicado',
  color: '#0b5cad',
}

export function adaptarCategorias(catalogos = []) {
  return catalogos
    .filter((c) => c?.nombre)
    .map((c) => ({ id: String(c.id), nombre: c.nombre.trim() }))
}

export function adaptarProductos(filas = [], catalogos = []) {
  const nombrePorId = new Map(
    adaptarCategorias(catalogos).map((c) => [c.id, c.nombre]),
  )

  return filas
    .filter((f) => f?.nombre && Number.isFinite(Number(f.precio)))
    .map((fila) => ({
      id: String(fila.id),
      nombre: fila.nombre.trim(),
      descripcion: fila.descripcion ?? '',
      // La API puede traer marca e imagen; si no las trae se dejan vacías y
      // la interfaz las oculta en vez de inventarse una o repetir el nombre.
      marca: typeof fila.marca === 'string' ? fila.marca.trim() : '',
      categoria: nombrePorId.get(String(fila.catalogoId)) ?? '',
      imagen: typeof fila.imagen === 'string' && fila.imagen ? fila.imagen : null,
      // Campos de ficha que la API todavía no expone. Si algún día los trae,
      // los bloques de la vista aparecen solos.
      codigo: typeof fila.codigo === 'string' ? fila.codigo : '',
      specs: fila.specs ?? {},
      pros: Array.isArray(fila.pros) ? fila.pros : [],
      contras: Array.isArray(fila.contras) ? fila.contras : [],
      precios: [
        {
          tienda: FUENTE_UNICA.id,
          precio: Number(fila.precio),
          // Sin precio de lista no se puede calcular descuento: la tarjeta
          // simplemente no lo pinta.
          precioLista: null,
          // `activo` es lo más cercano a "se puede comprar" que hay hoy.
          stock: fila.activo !== false,
          // Enlace a la ficha en la tienda. La API todavía no lo expone: sin
          // él no se pinta el botón de ir a comprar, en vez de mandar a nadie
          // a una dirección inventada.
          url: typeof fila.url === 'string' ? fila.url : null,
        },
      ],
      historial: [],
    }))
}

export function adaptarProducto(fila, catalogos = []) {
  if (!fila) return null

  return adaptarProductos([fila], catalogos)[0] ?? null
}
