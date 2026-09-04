import { computed, ref, watch } from 'vue'
import { defineStore } from 'pinia'

import {
  obtenerProducto,
  obtenerProductos,
  obtenerTiendas,
} from '@/modules/comparador/services/comparador.service'
import { TIENDAS } from '@/modules/comparador/data/tiendas'
import { ahorroMaximo, precioMasBajo } from '@/shared/utils/precios'

// Estado del módulo comparador. El store orquesta y guarda; no sabe de HTTP ni
// de URLs — para eso llama al servicio.
const CLAVE_TIENDAS = 'cep:tiendas'

function leerPreferencia() {
  try {
    const guardado = JSON.parse(localStorage.getItem(CLAVE_TIENDAS) ?? 'null')

    if (Array.isArray(guardado) && guardado.length > 0) return guardado
  } catch {
    // Almacenamiento bloqueado o valor corrupto: se cae al valor por defecto.
  }

  return TIENDAS.map((t) => t.id)
}

function guardarPreferencia(ids) {
  try {
    localStorage.setItem(CLAVE_TIENDAS, JSON.stringify(ids))
  } catch {
    // Sin persistencia la preferencia dura la sesión.
  }
}

export const useComparadorStore = defineStore('comparador', () => {
  // ——— estado ———
  const productos = ref([])
  // La lista de tiendas ya no es una constante: con backend viene de
  // /catalogos, y con datos de ejemplo son las cinco de siempre.
  const tiendas = ref(TIENDAS)
  const producto = ref(null)
  const busqueda = ref('')
  const categoria = ref('')
  // Cómo se ordenan los resultados. 'barato' es el que tiene sentido por
  // defecto en un comparador: si alguien viene a ver precios, lo primero que
  // quiere arriba es el más bajo.
  const orden = ref('barato')
  // Tope de precio. null = sin tope.
  const precioMaximo = ref(null)
  // Marcas elegidas. Vacío = todas.
  const marcas = ref([])
  // Talla concreta. La disponibilidad es POR TIENDA, así que una prenda entra
  // si ALGUNA tienda con stock la tiene en esa talla.
  const talla = ref('')
  // Atributos de ficha: { Material: 'Lino', Corte: 'Slim' }. Cuáles hay
  // depende del tipo de prenda, así que se derivan del catálogo, no de una
  // lista escrita a mano.
  const atributos = ref({})
  // Preferencia del usuario, persistida: qué tiendas quiere ver en toda la web.
  const tiendasActivas = ref(leerPreferencia())
  const cargando = ref(false)
  const error = ref(null)
  // Marca de tiempo de la última carga con éxito. Sólo se mueve cuando la
  // petición fue bien: es lo que permite decir "precios de hace 10 minutos".
  const actualizadoEn = ref(null)

  // ——— derivados ———
  const hayFiltros = computed(
    () =>
      busqueda.value.trim() !== '' ||
      categoria.value !== '' ||
      marcas.value.length > 0 ||
      talla.value !== '' ||
      Object.keys(atributos.value).length > 0 ||
      precioMaximo.value !== null ||
      tiendasActivas.value.length !== tiendas.value.length,
  )

  // Lo que hay puesto ahora mismo, para poder enseñarlo y quitarlo de uno en
  // uno. Sin esto sólo queda "limpiar todo", que obliga a rehacer el resto.
  const filtrosActivos = computed(() => {
    const puestos = []

    if (busqueda.value.trim() !== '') {
      puestos.push({ tipo: 'busqueda', etiqueta: `«${busqueda.value.trim()}»` })
    }

    if (categoria.value !== '') {
      puestos.push({ tipo: 'categoria', etiqueta: categoria.value })
    }

    marcas.value.forEach((m) =>
      puestos.push({ tipo: `marca:${m}`, etiqueta: m }),
    )

    if (talla.value !== '') {
      puestos.push({ tipo: 'talla', etiqueta: `Talla ${talla.value}` })
    }

    Object.entries(atributos.value).forEach(([clave, valor]) =>
      puestos.push({ tipo: `atributo:${clave}`, etiqueta: `${clave}: ${valor}` }),
    )

    if (precioMaximo.value !== null) {
      puestos.push({ tipo: 'precio', etiqueta: `hasta ${precioMaximo.value}` })
    }

    if (tiendasActivas.value.length !== tiendas.value.length) {
      puestos.push({
        tipo: 'tiendas',
        etiqueta: `${tiendasActivas.value.length} de ${tiendas.value.length} tiendas`,
      })
    }

    return puestos
  })

  // Rango de precios del catálogo, para que el control de presupuesto tenga
  // límites reales en vez de un máximo inventado.
  const rangoPrecios = computed(() => {
    const precios = productos.value
      .map((p) => precioMasBajo(p)?.precio)
      .filter((v) => typeof v === 'number')

    if (precios.length === 0) return null

    return { min: Math.min(...precios), max: Math.max(...precios) }
  })

  // Cuántos productos hay por categoría, contando el resto de filtros. Así el
  // panel puede avisar de que una categoría se quedaría vacía.
  const conteoPorCategoria = computed(() => {
    const cuenta = new Map()

    productos.value.forEach((p) => {
      if (!p.categoria) return

      cuenta.set(p.categoria, (cuenta.get(p.categoria) ?? 0) + 1)
    })

    return [...cuenta.entries()]
      .map(([nombre, total]) => ({ nombre, total }))
      .sort((a, b) => b.total - a.total || a.nombre.localeCompare(b.nombre))
  })

  // ——— facetas: qué se puede filtrar, según lo que hay ———
  //
  // Se calculan sobre los productos que pasan TODOS los demás filtros menos el
  // propio. Así los conteos son los de verdad y no se ofrece una opción que
  // dejaría la pantalla vacía.
  function sinFiltroDe(omitir) {
    const texto = busqueda.value.trim().toLowerCase()

    return productos.value.filter((p) => {
      if (
        texto !== '' &&
        !`${p.marca} ${p.nombre} ${p.categoria}`.toLowerCase().includes(texto)
      ) {
        return false
      }

      if (categoria.value !== '' && p.categoria !== categoria.value) return false

      if (
        omitir !== 'marca' &&
        marcas.value.length > 0 &&
        !marcas.value.includes(p.marca)
      ) {
        return false
      }

      if (
        omitir !== 'talla' &&
        talla.value !== '' &&
        !p.precios.some((o) => o.stock && (o.tallas ?? []).includes(talla.value))
      ) {
        return false
      }

      return Object.entries(atributos.value).every(
        ([clave, valor]) => omitir === clave || valorDeSpec(p, clave) === valor,
      )
    })
  }

  const marcasDisponibles = computed(() => {
    const cuenta = new Map()

    sinFiltroDe('marca').forEach((p) => {
      if (p.marca) cuenta.set(p.marca, (cuenta.get(p.marca) ?? 0) + 1)
    })

    return [...cuenta.entries()]
      .map(([nombre, total]) => ({ nombre, total }))
      .sort((a, b) => b.total - a.total || a.nombre.localeCompare(b.nombre))
  })

  const tallasDisponibles = computed(() => {
    const cuenta = new Map()

    sinFiltroDe('talla').forEach((p) => {
      const suyas = new Set(
        p.precios.filter((o) => o.stock).flatMap((o) => o.tallas ?? []),
      )

      suyas.forEach((t) => cuenta.set(t, (cuenta.get(t) ?? 0) + 1))
    })

    // Numéricas por número, de letra por el orden de siempre.
    const ORDEN = ['XS', 'S', 'M', 'L', 'XL', 'XXL']

    return [...cuenta.entries()]
      .map(([nombre, total]) => ({ nombre, total }))
      .sort((a, b) => {
        const na = Number(a.nombre)
        const nb = Number(b.nombre)

        if (Number.isFinite(na) && Number.isFinite(nb)) return na - nb

        return ORDEN.indexOf(a.nombre) - ORDEN.indexOf(b.nombre)
      })
  })

  // Atributos que de verdad sirven para filtrar: los que aparecen en más de un
  // producto Y con más de un valor distinto. Un atributo con un solo valor no
  // discrimina nada, y ofrecerlo sólo alarga el panel.
  const atributosDisponibles = computed(() => {
    const visibles = sinFiltroDe(null)
    const porClave = new Map()

    visibles.forEach((p) => {
      const specs = Array.isArray(p.specs) ? p.specs : [{ filas: p.specs ?? {} }]

      specs.forEach((grupo) => {
        Object.entries(grupo?.filas ?? {}).forEach(([clave, valor]) => {
          if (!porClave.has(clave)) porClave.set(clave, new Map())

          const valores = porClave.get(clave)

          valores.set(valor, (valores.get(valor) ?? 0) + 1)
        })
      })
    })

    return [...porClave.entries()]
      .filter(([, valores]) => valores.size > 1)
      .map(([clave, valores]) => ({
        clave,
        valores: [...valores.entries()]
          .map(([valor, total]) => ({ valor, total }))
          .sort((a, b) => b.total - a.total || a.valor.localeCompare(b.valor)),
      }))
      .sort((a, b) => a.clave.localeCompare(b.clave))
  })

  function alternarMarca(nombre) {
    marcas.value = marcas.value.includes(nombre)
      ? marcas.value.filter((m) => m !== nombre)
      : [...marcas.value, nombre]
  }

  function ponerAtributo(clave, valor) {
    const copia = { ...atributos.value }

    if (copia[clave] === valor) delete copia[clave]
    else copia[clave] = valor

    atributos.value = copia
  }

  function quitarFiltro(tipo) {
    if (tipo === 'busqueda') busqueda.value = ''
    if (tipo === 'categoria') categoria.value = ''
    if (tipo === 'talla') talla.value = ''
    if (tipo === 'precio') precioMaximo.value = null
    if (tipo === 'tiendas') marcarTodasLasTiendas()
    if (tipo.startsWith('marca:')) alternarMarca(tipo.slice(6))

    if (tipo.startsWith('atributo:')) {
      const copia = { ...atributos.value }

      delete copia[tipo.slice(9)]
      atributos.value = copia
    }
  }

  // Valor de un atributo de ficha, sea cual sea el formato de specs.
  function valorDeSpec(producto, clave) {
    const specs = producto?.specs

    if (Array.isArray(specs)) {
      for (const grupo of specs) {
        const valor = grupo?.filas?.[clave]

        if (valor !== undefined) return valor
      }

      return undefined
    }

    return specs?.[clave]
  }

  const productosFiltrados = computed(() => {
    const texto = busqueda.value.trim().toLowerCase()

    return productos.value
      .filter((p) => {
        const coincideTexto =
          texto === '' ||
          `${p.marca} ${p.nombre} ${p.categoria}`.toLowerCase().includes(texto)

        const coincideCategoria =
          categoria.value === '' || p.categoria === categoria.value

        const coincideMarca =
          marcas.value.length === 0 || marcas.value.includes(p.marca)

        const coincideTalla =
          talla.value === '' ||
          p.precios.some((o) => o.stock && (o.tallas ?? []).includes(talla.value))

        const coincideAtributos = Object.entries(atributos.value).every(
          ([clave, valor]) => valorDeSpec(p, clave) === valor,
        )

        // El tope se mide contra el precio más bajo del producto: si en alguna
        // tienda cabe en el presupuesto, el producto entra.
        const masBajo = precioMasBajo(p)
        const coincidePrecio =
          precioMaximo.value === null ||
          (masBajo !== null && masBajo.precio <= precioMaximo.value)

        // Un producto entra si alguna de las tiendas marcadas lo vende.
        const coincideTienda = p.precios.some((oferta) =>
          tiendasActivas.value.includes(oferta.tienda),
        )

        return (
          coincideTexto &&
          coincideCategoria &&
          coincideMarca &&
          coincideTalla &&
          coincideAtributos &&
          coincidePrecio &&
          coincideTienda
        )
      })
      .map((p) => ({
        ...p,
        // Sólo las ofertas de las tiendas marcadas: si el usuario descartó
        // H&M, el "más barato" no puede seguir siendo el de H&M.
        precios: p.precios.filter((oferta) =>
          tiendasActivas.value.includes(oferta.tienda),
        ),
      }))
      .sort((a, b) => {
        if (orden.value === 'nuevo') {
          return (a.agregadoHace ?? 999) - (b.agregadoHace ?? 999)
        }

        if (orden.value === 'visto') {
          return (b.vistas ?? 0) - (a.vistas ?? 0)
        }

        const ma = precioMasBajo(a)
        const mb = precioMasBajo(b)

        if (orden.value === 'caro') {
          // Los que no tienen precio siguen al final, también al ordenar de
          // mayor a menor: no tenerlo no es ser el más caro.
          if (!ma && !mb) return 0
          if (!ma) return 1
          if (!mb) return -1

          return mb.precio - ma.precio
        }

        // Los que no tienen stock en ninguna tienda marcada van al final.
        if (!ma && !mb) return 0
        if (!ma) return 1
        if (!mb) return -1

        return ma.precio - mb.precio
      })
  })

  const totalResultados = computed(() => productosFiltrados.value.length)

  // Índice por id. Como Map y no como find(): el armador lo consulta una vez
  // por ranura en cada recálculo, y recorrer el catálogo entero cada vez se
  // nota cuando hay cientos de prendas.
  const porId = computed(() => new Map(productos.value.map((p) => [p.id, p])))

  function productoById(id) {
    return porId.value.get(String(id)) ?? null
  }

  // ¿Hay más de una fuente de precio? Con el backend actual no la hay, y la
  // interfaz tiene que dejar de hablar de "la tienda más barata" en vez de
  // enseñar una comparación de un solo elemento, que no compara nada.
  const comparacionDisponible = computed(() => tiendas.value.length > 1)

  // Hay datos en pantalla pero la última petición falló: lo que se ve es
  // cierto, sólo que viejo. La interfaz lo dice en vez de fingir que está al día.
  const datosDesactualizados = computed(
    () => error.value !== null && productos.value.length > 0,
  )

  // Se persiste desde un watch y no dentro de cada acción: así también se
  // guarda cuando la cabecera escribe la lista entera con v-model.
  watch(tiendasActivas, guardarPreferencia, { deep: true })

  // ——— derivados de la portada ———
  // La portada respeta la PREFERENCIA de tiendas (es una elección deliberada
  // del usuario sobre qué quiere ver en toda la web) pero ignora la búsqueda y
  // la categoría, que son filtros pasajeros del comparador: un escaparate no
  // debe encogerse porque quedara un texto escrito antes de volver al inicio.
  const LIMITE_PORTADA = 6

  const visibles = computed(() =>
    productos.value
      .filter((p) =>
        p.precios.some((o) => tiendasActivas.value.includes(o.tienda)),
      )
      .map((p) => ({
        ...p,
        precios: p.precios.filter((o) =>
          tiendasActivas.value.includes(o.tienda),
        ),
      })),
  )

  const masRecientes = computed(() =>
    [...visibles.value]
      .sort((a, b) => (a.agregadoHace ?? 999) - (b.agregadoHace ?? 999))
      .slice(0, LIMITE_PORTADA),
  )

  const masVistos = computed(() =>
    [...visibles.value]
      .sort((a, b) => (b.vistas ?? 0) - (a.vistas ?? 0))
      .slice(0, LIMITE_PORTADA),
  )

  // "Ofertas del día": los que más se ahorran comparando entre tiendas. Es la
  // métrica que justifica la app, no el descuento que declara cada tienda.
  const ofertasDelDia = computed(() =>
    visibles.value
      .map((producto) => ({ producto, ahorro: ahorroMaximo(producto) }))
      .filter((o) => o.ahorro > 0)
      .sort((a, b) => b.ahorro - a.ahorro)
      .slice(0, LIMITE_PORTADA),
  )

  const categoriasPopulares = computed(() => {
    const cuenta = new Map()

    visibles.value.forEach((p) => {
      cuenta.set(p.categoria, (cuenta.get(p.categoria) ?? 0) + 1)
    })

    return [...cuenta.entries()]
      .map(([categoria, cantidad]) => ({ categoria, cantidad }))
      .sort((a, b) => b.cantidad - a.cantidad || a.categoria.localeCompare(b.categoria))
  })

  // El producto que abre la portada: el de mayor ahorro entre tiendas.
  const destacado = computed(() => ofertasDelDia.value[0]?.producto ?? null)

  // ——— acciones ———
  async function cargarProductos({ forzar = false } = {}) {
    if (productos.value.length > 0 && !forzar) return

    cargando.value = true
    error.value = null

    try {
      productos.value = await obtenerProductos()
      actualizadoEn.value = Date.now()
    } catch (e) {
      error.value = e.message

      // Lo que ya estaba cargado NO se borra. Antes había aquí un
      // `productos.value = []` y el efecto era el peor posible: estabas
      // navegando con precios a la vista, fallaba una recarga y se te vaciaba
      // la pantalla. Es mejor enseñar lo de antes diciendo desde cuándo es.
    } finally {
      cargando.value = false
    }
  }

  async function cargarTiendas() {
    try {
      const lista = await obtenerTiendas()

      if (lista.length === 0) return

      tiendas.value = lista

      // Si la preferencia guardada apunta a tiendas que ya no existen, se
      // recalcula: si no, el usuario se quedaría sin ver nada y sin entender
      // por qué.
      const validas = tiendasActivas.value.filter((id) =>
        lista.some((t) => t.id === id),
      )

      tiendasActivas.value =
        validas.length > 0 ? validas : lista.map((t) => t.id)
    } catch {
      // Si /catalogos falla se sigue con la lista por defecto: peor sería
      // dejar la cabecera sin ninguna tienda que ofrecer.
    }
  }

  // Contador de petición: si el usuario abre un producto y salta a otro antes
  // de que llegue la respuesta, la primera no debe pisar a la segunda.
  let peticion = 0

  async function cargarProducto(id) {
    const actual = ++peticion

    cargando.value = true
    error.value = null
    producto.value = null

    try {
      const resultado = await obtenerProducto(id)

      if (actual !== peticion) return

      producto.value = resultado
    } catch (e) {
      if (actual !== peticion) return

      error.value = e.message
    } finally {
      if (actual === peticion) cargando.value = false
    }
  }

  function alternarTienda(id) {
    const i = tiendasActivas.value.indexOf(id)

    if (i === -1) {
      tiendasActivas.value.push(id)
    } else {
      tiendasActivas.value.splice(i, 1)
    }
  }

  // Deja marcada sólo esa tienda. Lo usan los enlaces del pie: "ver lo de
  // Zara" es acotar, no sumar a lo que ya hubiera puesto.
  function soloTienda(id) {
    tiendasActivas.value = tiendas.value.some((t) => t.id === id)
      ? [id]
      : tiendas.value.map((t) => t.id)
  }

  function marcarTodasLasTiendas() {
    tiendasActivas.value = tiendas.value.map((t) => t.id)
  }

  function limpiarFiltros() {
    busqueda.value = ''
    categoria.value = ''
    marcas.value = []
    talla.value = ''
    atributos.value = {}
    precioMaximo.value = null
    marcarTodasLasTiendas()
  }

  return {
    productos,
    tiendas,
    producto,
    busqueda,
    categoria,
    orden,
    precioMaximo,
    marcas,
    talla,
    atributos,
    tiendasActivas,
    cargando,
    error,
    actualizadoEn,

    hayFiltros,
    filtrosActivos,
    rangoPrecios,
    conteoPorCategoria,
    marcasDisponibles,
    tallasDisponibles,
    atributosDisponibles,
    productosFiltrados,
    totalResultados,
    comparacionDisponible,
    datosDesactualizados,
    masRecientes,
    masVistos,
    ofertasDelDia,
    categoriasPopulares,
    destacado,

    productoById,

    cargarTiendas,
    cargarProductos,
    cargarProducto,
    alternarTienda,
    soloTienda,
    marcarTodasLasTiendas,
    alternarMarca,
    ponerAtributo,
    quitarFiltro,
    limpiarFiltros,
  }
})
