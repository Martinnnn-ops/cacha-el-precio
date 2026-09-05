import { readdirSync, readFileSync } from 'node:fs'

import { createServer } from 'vite'
import { createSSRApp } from 'vue'
import { renderToString } from 'vue/server-renderer'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'

// mode 'test' carga .env.test, que deja VITE_API_BASE_URL vacía: la lógica se
// prueba contra los datos de ejemplo, sin depender de que el backend esté
// levantado. La conexión real se comprueba aparte, más abajo.
// Node no trae localStorage ni document. Se falsean AQUÍ y no en el código de
// la aplicación: los stores ya se defienden con try/catch, y ensuciar el
// código de producción para que un script de pruebas funcione sería al revés.
const almacen = new Map()

globalThis.localStorage = {
  getItem: (k) => (almacen.has(k) ? almacen.get(k) : null),
  setItem: (k, v) => almacen.set(k, String(v)),
  removeItem: (k) => almacen.delete(k),
  clear: () => almacen.clear(),
  key: (i) => [...almacen.keys()][i] ?? null,
  get length() {
    return almacen.size
  },
}

const vite = await createServer({
  mode: 'test',
  server: { middlewareMode: true },
  appType: 'custom',
  logLevel: 'error',
})

const load = (p) => vite.ssrLoadModule(p)
let fallos = 0
const check = (nombre, cond, extra = '') => {
  console.log(`  ${cond ? 'OK  ' : 'FALLA'} ${nombre}${extra ? ' — ' + extra : ''}`)
  if (!cond) fallos++
}
// Para lo que es información del entorno y no un defecto del frontend.
const nota = (texto) => console.log(`  NOTA  ${texto}`)

try {
  console.log('=== contraste de la paleta (WCAG) ===')
  // Los colores del texto no se eligen a ojo: este bloque falla si alguien
  // aclara un token por debajo del mínimo legible.
  {
    const css = readFileSync('src/assets/base.css', 'utf8')
    // El tema oscuro redefine los tokens dentro de [data-tema='oscuro'].
    const bloqueOscuro = /\[data-tema='oscuro'\] \{([\s\S]*?)\n\}/.exec(css)?.[1] ?? ''

    const token = (n, oscuro = false) => {
      const donde = oscuro ? bloqueOscuro : css
      const m = new RegExp(`--cep-${n}: *(#[0-9a-f]{6})`, 'i').exec(donde)
      if (!m) throw new Error(`token --cep-${n} no encontrado`)
      return m[1]
    }
    const rgb = (h) => [1, 3, 5].map((i) => parseInt(h.slice(i, i + 2), 16))
    const lum = (c) =>
      c
        .map((v) => (v / 255 <= 0.03928 ? v / 255 / 12.92 : ((v / 255 + 0.055) / 1.055) ** 2.4))
        .reduce((a, v, i) => a + v * [0.2126, 0.7152, 0.0722][i], 0)
    const ratio = (a, b, oscuro) => {
      const x = lum(rgb(token(a, oscuro)))
      const y = lum(rgb(token(b, oscuro)))
      return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05)
    }

    const PARES = [
      ['ink', 'bg', 4.5, 'texto sobre el fondo'],
      ['ink', 'surface', 4.5, 'texto sobre tarjeta'],
      ['ink', 'sunken', 4.5, 'texto sobre el pie'],
      ['muted', 'bg', 4.5, 'texto atenuado sobre el fondo'],
      ['muted', 'surface', 4.5, 'texto atenuado sobre tarjeta'],
      ['muted', 'sunken', 4.5, 'texto atenuado sobre el pie'],
      ['accent', 'surface', 4.5, 'acento sobre tarjeta'],
      ['accent', 'sunken', 4.5, 'acento sobre el pie'],
      ['exito', 'surface', 4.5, 'éxito sobre tarjeta'],
      ['alerta', 'surface', 4.5, 'error sobre tarjeta'],
      ['on-ink', 'ink', 4.5, 'texto del botón primario'],
      ['on-accent', 'accent', 4.5, 'texto sobre el acento'],
    ]

    // Las tres superficies tienen que formar una escalera. Esto NO es contraste
    // de texto: es lo que hace que una tarjeta se lea como tarjeta y no como
    // una mancha del fondo. El tema oscuro llegó a tener 1.02 entre la tarjeta
    // y el pie —o sea, el mismo color— y la página entera se veía plana.
    const SEPARACION = [
      ['bg', 'surface', 1.15, 'la tarjeta se despega del fondo'],
      ['bg', 'sunken', 1.12, 'lo hundido se despega del fondo'],
      ['surface', 'sunken', 1.3, 'la tarjeta se despega de lo hundido'],
    ]

    // El orden importa tanto como la distancia: lo hundido va POR DEBAJO del
    // fondo y la tarjeta POR ENCIMA, en los dos temas. Si se invierte, el pie
    // sobresale y la tarjeta se hunde, que es justo al revés de lo que dicen.
    const lumDe = (n, oscuro) => lum(rgb(token(n, oscuro)))

    for (const [tema, oscuro] of [['claro', false], ['oscuro', true]]) {
      console.log(`  · tema ${tema}`)

      for (const [fg, bg, min, etiqueta] of PARES) {
        const v = ratio(fg, bg, oscuro)
        check(`  ${etiqueta} (${v.toFixed(2)}:1)`, v >= min, `min ${min}`)
      }

      for (const [a, b, min, etiqueta] of SEPARACION) {
        const v = ratio(a, b, oscuro)
        check(`  ${etiqueta} (${v.toFixed(2)}:1)`, v >= min, `min ${min}`)
      }

      const hundido = lumDe('sunken', oscuro)
      const fondo = lumDe('bg', oscuro)
      const tarjeta = lumDe('surface', oscuro)

      check('  lo hundido queda por debajo del fondo', hundido < fondo)
      check('  y la tarjeta por encima', tarjeta > fondo)
    }
  }

  console.log('\n=== adaptador del Product Service ===')
  {
    const { adaptarProductos, adaptarCategorias, FUENTE_UNICA } = await load(
      '/src/modules/comparador/services/producto.adapter.js',
    )

    // Forma exacta que devuelve la API. Un catálogo es una CATEGORÍA, no una
    // tienda: cada fila es un producto con un solo precio.
    const CATALOGOS = [
      { id: 2, nombre: 'Poleras', descripcion: '' },
      { id: 3, nombre: ' Pantalones ', descripcion: '' },
      { id: 9, nombre: null },
    ]
    const FILAS = [
      { id: 2, nombre: 'Polera básica', descripcion: 'Algodón', precio: 9990, catalogoId: 2, activo: true },
      { id: 4, nombre: 'Polera manga larga', descripcion: '', precio: 15990, catalogoId: 2, activo: false },
      { id: 5, nombre: 'Jeans slim', descripcion: '', precio: 29990, catalogoId: 3, activo: true },
      { id: 8, nombre: 'Huérfano', descripcion: '', precio: 100, catalogoId: 77, activo: true },
      { id: 9, nombre: '', precio: 100, catalogoId: 2, activo: true },
      { id: 10, nombre: 'Rota', precio: 'x', catalogoId: 2, activo: true },
    ]

    const adaptados = adaptarProductos(FILAS, CATALOGOS)

    check('una fila de la API es un producto', adaptados.length === 4, `${adaptados.length} productos`)
    check('  el catálogo se traduce a categoría',
      adaptados.find((p) => p.id === '2').categoria === 'Poleras')
    check('  recorta los espacios del nombre de la categoría',
      adaptados.find((p) => p.id === '5').categoria === 'Pantalones')
    check('  un catálogo desconocido deja la categoría vacía, no "undefined"',
      adaptados.find((p) => p.id === '8').categoria === '')
    check('  activo=false se traduce a sin stock',
      adaptados.find((p) => p.id === '4').precios[0].stock === false)
    check('  no inventa marca', adaptados.every((p) => p.marca === ''))
    check('  sin precio de lista no inventa descuento',
      adaptados.every((p) => p.precios[0].precioLista === null))
    check('  historial vacío, no undefined',
      adaptados.every((p) => Array.isArray(p.historial) && p.historial.length === 0))

    check('descarta filas sin nombre', !adaptados.some((p) => p.nombre === ''))
    check('descarta precios que no son números', !adaptados.some((p) => p.nombre === 'Rota'))

    // Mientras el backend no guarde precios por tienda hay UNA sola fuente.
    // Ponerle un nombre neutro evita que la interfaz diga "más barato en X".
    check('todos los precios vienen de la misma fuente',
      new Set(adaptados.map((p) => p.precios[0].tienda)).size === 1)
    check('  y esa fuente no se llama como una tienda',
      FUENTE_UNICA.nombre === 'Precio publicado', FUENTE_UNICA.nombre)

    const cats = adaptarCategorias(CATALOGOS)
    check('las categorías descartan las que no tienen nombre', cats.length === 2)

    const { precioMasBajo: pmb, ahorroMaximo: am } = await load('/src/shared/utils/precios.js')
    const polera = adaptados.find((p) => p.id === '2')
    check('el cálculo de precio sigue funcionando con una sola oferta',
      pmb(polera).precio === 9990)
    check('  y el ahorro es cero: no hay nada que comparar', am(polera) === 0)
    check('  un producto sin stock no tiene precio más bajo',
      pmb(adaptados.find((p) => p.id === '4')) === null)
  }

  console.log('\n=== capa de servicios ===')
  const svc = await load('/src/modules/comparador/services/comparador.service.js')
  const lista = await svc.obtenerProductos()
  const TOTAL_MOCK = (
    await load('/src/modules/comparador/data/productos.mock.js')
  ).PRODUCTOS_MOCK.length

  check('obtenerProductos() devuelve el catálogo',
    Array.isArray(lista) && lista.length === TOTAL_MOCK, `${lista.length} productos`)

  const uno = await svc.obtenerProducto('2')
  check('obtenerProducto(id) devuelve el producto', uno?.nombre?.includes('Jeans'), uno?.nombre)
  check('obtenerProducto(id inexistente) devuelve null', (await svc.obtenerProducto('nope')) === null)

  console.log('\n=== utilidades de precio ===')
  const { precioMasBajo, ahorroMaximo, descuento } = await load('/src/shared/utils/precios.js')
  const p1 = lista.find((p) => p.id === '1')
  check('el más barato entre las disponibles', precioMasBajo(p1).tienda === 'hym', precioMasBajo(p1).tienda)
  check('ahorro = más caro − más barato, con stock', ahorroMaximo(p1) === 12990 - 8990, String(ahorroMaximo(p1)))

  // La oferta de menor precio está agotada: si la regla del stock se cae, esto
  // devolvería 'zara'. El caso anterior no lo detectaba porque su agotada era
  // además la más cara.
  const conAgotado = {
    precios: [
      { tienda: 'zara', precio: 1000, stock: false },
      { tienda: 'paris', precio: 5000, stock: true },
      { tienda: 'mango', precio: 9000, stock: true },
    ],
  }
  check('la más barata agotada no gana', precioMasBajo(conAgotado).tienda === 'paris', precioMasBajo(conAgotado).tienda)
  check('y tampoco cuenta para el ahorro', ahorroMaximo(conAgotado) === 4000, String(ahorroMaximo(conAgotado)))
  check('todo agotado devuelve null', precioMasBajo({ precios: [{ tienda: 'zara', precio: 1, stock: false }] }) === null)
  check('descuento sobre el precio de lista', descuento({ precio: 9990, precioLista: 14990 }) === 33)
  check('sin producto no revienta', precioMasBajo(null) === null && ahorroMaximo(undefined) === 0)

  console.log('\n=== store ===')
  const pinia = createPinia()
  setActivePinia(pinia)
  const { useComparadorStore } = await load('/src/modules/comparador/store/comparador.store.js')
  const store = useComparadorStore()

  await store.cargarProductos()
  check('carga y deja de cargar',
    store.productos.length === TOTAL_MOCK && store.cargando === false)
  // El primero es el más barato del catálogo, sea cual sea: la prueba mira el
  // orden, no una prenda concreta.
  check('ordena por precio más bajo primero',
    store.productosFiltrados
      .map((p) => precioMasBajo(p)?.precio ?? Infinity)
      .every((precio, i, todos) => i === 0 || todos[i - 1] <= precio),
    store.productosFiltrados[0].nombre)

  store.busqueda = 'jeans'
  check('filtra por texto', store.totalResultados === 1 && store.productosFiltrados[0].id === '2', `${store.totalResultados} resultado(s)`)
  store.busqueda = ''

  store.alternarTienda('hym')
  const sinHym = store.productosFiltrados.find((p) => p.id === '1')
  check('quitar una tienda la saca de las ofertas', !sinHym.precios.some((o) => o.tienda === 'hym'))
  check('y recalcula el más barato', precioMasBajo(sinHym).tienda === 'ripley', precioMasBajo(sinHym).tienda)
  check('hayFiltros se activa', store.hayFiltros === true)

  store.limpiarFiltros()
  check('limpiar deja todo como al principio',
    store.hayFiltros === false && store.totalResultados === TOTAL_MOCK)

  await store.cargarProducto('3')
  check('carga el detalle', store.producto?.id === '3')

  // Las rutas salen del router real, no del módulo suelto: así el catch-all
  // del 404 entra en la prueba.
  const { useOutfitStore } = await load(
    '/src/modules/outfits/store/outfit.store.js',
  )
  const { routes } = await load('/src/core/router/routes.js')
  const App = (await load('/src/App.vue')).default

  // En SSR no se ejecuta onMounted, así que la vista nunca pediría los datos:
  // se precargan sobre la MISMA instancia de pinia que recibe la app.
  async function render(ruta, precargar) {
    const p = createPinia()
    setActivePinia(p)
    await precargar(useComparadorStore(), useOutfitStore())

    const router = createRouter({ history: createMemoryHistory(), routes })
    const app = createSSRApp(App)
    app.use(p).use(router)
    await router.push(ruta)
    await router.isReady()

    return renderToString(app)
  }

  console.log('\n=== derivados de la portada ===')
  const st = (() => { setActivePinia(createPinia()); return useComparadorStore() })()
  await st.cargarProductos()

  check('lo más reciente ordena por antigüedad', st.masRecientes[0].id === '3', `${st.masRecientes[0].nombre} (${st.masRecientes[0].agregadoHace}d)`)
  check('lo más visto ordena por visitas', st.masVistos[0].id === '5', `${st.masVistos[0].vistas} visitas`)
  check('ofertas del día ordena por ahorro', st.ofertasDelDia[0].ahorro >= st.ofertasDelDia[1].ahorro, `top ${st.ofertasDelDia[0].ahorro}`)
  check('ofertas del día excluye los de ahorro cero', st.ofertasDelDia.every((o) => o.ahorro > 0))
  check('categorías populares cuenta y ordena',
    st.categoriasPopulares.length > 1 &&
      st.categoriasPopulares.every((c) => c.cantidad >= 1) &&
      st.categoriasPopulares.every(
        (c, i, todas) => i === 0 || todas[i - 1].cantidad >= c.cantidad,
      ))
  check('el destacado es la mayor oferta', st.destacado.id === st.ofertasDelDia[0].producto.id)

  // La portada es un escaparate: un filtro puesto antes no debe encogerla.
  st.busqueda = 'jeans'
  check('la portada ignora los filtros activos', st.masRecientes.length === 6 && st.totalResultados === 1)
  st.limpiarFiltros()

  // La preferencia de tiendas SÍ afecta a la portada: es una elección
  // deliberada sobre qué se quiere ver en toda la web.
  st.tiendasActivas = ['zara']
  check('la portada respeta la preferencia de tiendas',
    st.masVistos.every((p) => p.precios.every((o) => o.tienda === 'zara')),
    `${st.masVistos.length} productos`)
  check('  y descarta los que esa tienda no vende',
    st.masVistos.length < st.productos.length,
    `${st.masVistos.length} de ${st.productos.length}`)
  st.marcarTodasLasTiendas()

  check('el filtro por categoría acota', (() => { st.categoria = 'Camisas'; const n = st.totalResultados; st.limpiarFiltros(); return n === 1 })())

  console.log('\n=== preferencias de interfaz ===')
  {
    setActivePinia(createPinia())
    const { useUiStore } = await load('/src/shared/stores/ui.store.js')
    const ui = useUiStore()

    check('arranca siguiendo al sistema', ui.tema === 'sistema')
    ui.elegirTema('oscuro')
    check('se puede forzar oscuro', ui.tema === 'oscuro' && ui.esOscuro === true)
    ui.elegirTema('claro')
    check('y claro', ui.esOscuro === false)
    ui.elegirTema('inventado')
    check('un valor inválido no rompe el tema', ui.tema === 'claro', ui.tema)

    // El store escribe data-tema en <html>. Se le pone un documento de mentira
    // sólo para esta comprobación y se retira enseguida: dejarlo puesto haría
    // que el render en servidor se creyera un navegador.
    globalThis.document = { documentElement: { dataset: {} } }
    ui.elegirTema('oscuro')
    await new Promise((r) => setTimeout(r, 0))
    const escrito = globalThis.document.documentElement.dataset.tema
    delete globalThis.document
    check('escribe data-tema en <html>', escrito === 'oscuro', String(escrito))

    // ——— el fogonazo de tema ———
    //
    // Vue tarda en arrancar. Si data-tema sólo se pone desde el store, hasta
    // entonces la página se pinta con los tokens claros y luego cambia a la
    // vista del usuario. Con la transición de `body` encima, quien tiene el
    // sistema en oscuro veía un desvanecido crema en CADA carga.
    const indice = readFileSync('index.html', 'utf8')
    const cabeza = indice.slice(0, indice.indexOf('</head>'))
    const guion = /<script>([\s\S]*?)<\/script>/.exec(cabeza)?.[1] ?? ''

    check('el tema se pinta antes del primer render',
      guion.includes('documentElement.dataset.tema'))
    check('  y el guion va en el <head>, no al final del body',
      cabeza.includes('documentElement.dataset.tema'))

    // El guion duplica la lógica del store por necesidad: tiene que correr
    // antes que cualquier módulo. Lo que no puede es divergir.
    const fuenteStore = readFileSync('src/shared/stores/ui.store.js', 'utf8')
    const claveStore = /const CLAVE = '([^']+)'/.exec(fuenteStore)?.[1]
    check('  usa la MISMA clave de almacenamiento que el store',
      claveStore !== undefined && guion.includes(`'${claveStore}'`), claveStore)
    check('  y respeta la preferencia del sistema, no asume claro',
      guion.includes('prefers-color-scheme: dark'))
    check('  con el localStorage capado sigue funcionando',
      /try\s*\{[\s\S]*?localStorage[\s\S]*?\}\s*catch/.test(guion))

    // La barra del navegador en móvil se pinta con este meta. Si se queda con
    // el color viejo, el móvil enmarca la página en un tono que ya no existe.
    const metaOscuro = /theme-color" content="(#[0-9a-f]{6})" media="\(prefers-color-scheme: dark\)/
      .exec(indice)?.[1]
    const bgOscuro = /\[data-tema='oscuro'\][\s\S]*?--cep-bg: *(#[0-9a-f]{6})/
      .exec(readFileSync('src/assets/base.css', 'utf8'))?.[1]
    check('  el theme-color oscuro es el fondo oscuro de verdad',
      metaOscuro !== undefined && metaOscuro === bgOscuro,
      `meta ${metaOscuro} vs token ${bgOscuro}`)
  }

  console.log('\n=== cuenta (Google OAuth 2.0 + PKCE) ===')
  {
    setActivePinia(createPinia())
    const { useCuentaStore } = await load('/src/modules/cuenta/store/cuenta.store.js')
    const cuenta = useCuentaStore()

    check('empieza sin sesión', cuenta.autenticado === false)

    // Sin VITE_GOOGLE_CLIENT_ID configurado no se puede entrar, y hay que
    // decirlo sin nombrar la variable de entorno.
    await cuenta.entrarConGoogle()
    check('sin cliente configurado avisa y no rompe',
      cuenta.autenticado === false && typeof cuenta.error === 'string',
      cuenta.error)
    check('  el aviso no menciona nada técnico',
      !/VITE_|client_id|env/i.test(cuenta.error ?? ''), cuenta.error)

    // ——— el `state` es la defensa contra CSRF ———
    const oauth = await load('/src/modules/cuenta/services/google.oauth.js')

    globalThis.sessionStorage = (() => {
      const m = new Map()
      return {
        getItem: (k) => (m.has(k) ? m.get(k) : null),
        setItem: (k, v) => m.set(k, String(v)),
        removeItem: (k) => m.delete(k),
      }
    })()

    sessionStorage.setItem('cep:estado', 'ESTADO-BUENO')
    sessionStorage.setItem('cep:pkce', 'VERIFICADOR')

    check('un state distinto se rechaza', oauth.verificarEstado('OTRO') === null)

    sessionStorage.setItem('cep:estado', 'ESTADO-BUENO')
    sessionStorage.setItem('cep:pkce', 'VERIFICADOR')
    check('  el state correcto devuelve el verificador',
      oauth.verificarEstado('ESTADO-BUENO') === 'VERIFICADOR')
    check('  y se consume: no vale dos veces',
      oauth.verificarEstado('ESTADO-BUENO') === null)

    check('sin state guardado no se acepta nada',
      oauth.verificarEstado(null) === null && oauth.verificarEstado('x') === null)

    // ——— la vuelta de Google ———
    const cancelado = await cuenta.procesarRetorno({ error: 'access_denied' })
    check('cancelar en Google no se presenta como un fallo',
      cancelado === null && /cancelaste/i.test(cuenta.error), cuenta.error)

    const sinState = await cuenta.procesarRetorno({ code: 'abc', state: 'falso' })
    check('un código sin state válido se rechaza',
      sinState === null && cuenta.autenticado === false)

    check('cerrar sesión limpia usuario y token',
      (() => {
        cuenta.cerrarSesion()
        return cuenta.autenticado === false && sessionStorage.getItem('cep:token') === null
      })())

    delete globalThis.sessionStorage
  }

  console.log('\n=== render de las vistas ===')
  const home = await render('/comparador', (s) => s.cargarProductos())
  check('ComparadorView renderiza', home.length > 400, `${home.length} bytes`)
  check('  lista los productos', home.includes('Polera b') && home.includes('Blazer'))
  check('  pinta el filtro de las 5 tiendas', ['Ripley','Paris','Zara','H&amp;M','Mango'].every((t) => home.includes(t)))
  // La primera tarjeta es la más barata del catálogo: polera a $8.990 en H&M.
  check('  la tarjeta anuncia dónde está más barato', /tarjeta__en[^>]*>\s*en H&amp;M/.test(home))
  check('  y marca esa fila como la mejor', /tarjeta__oferta--mejor[\s\S]{0,220}?tarjeta__tienda[^>]*>H&amp;M/.test(home))
  check('  con el precio correcto', home.includes('$8.990'))

  const detalle = await render('/producto/1', async (s) => {
    await s.cargarProducto('1')
    // El watch inmediato de la vista volvería a pedir el producto y a dejarlo
    // en null; SSR no puede esperar esa promesa. En el navegador sí ocurre y
    // por eso la vista tiene su estado de carga.
    s.cargarProducto = async () => {}
  })
  check('ProductoDetailView renderiza', detalle.length > 400, `${detalle.length} bytes`)
  check('  muestra dónde comprarla',
    detalle.includes('Elige tu tienda') || detalle.includes('Dónde comprarla'))
  check('  muestra el historial', detalle.includes('Historial de precios'))
  check('  dibuja una polilínea por tienda', (detalle.match(/<polyline/g) ?? []).length === 3, `${(detalle.match(/<polyline/g) ?? []).length} series`)

  const portada = await render('/', (s) => s.cargarProductos())
  check('InicioView renderiza', portada.length > 4000, `${portada.length} bytes`)
  for (const [titulo, marca] of [
    ['Lo más reciente', 'Recién agregado'],
    ['Lo más visto', 'visitas'],
    ['Categorías populares', 'Ver ofertas'],
    ['Ofertas del día', 'Ahorra '],
  ]) {
    check(`  sección "${titulo}"`, portada.includes(titulo) && portada.includes(marca))
  }
  // El de cierre lo pone ahora el layout, para todas las vistas a la vez; la
  // portada conserva el suyo propio arriba. Que aparezcan de verdad se
  // comprueba en "anuncios en todas las vistas".
  check('  la portada conserva su espacio de anuncio propio',
    (readFileSync('src/modules/comparador/views/InicioView.vue', 'utf8')
      .match(/<AdSlot/g) ?? []).length === 1)
  check('  y el de cierre vive en el layout, no repetido en cada vista',
    readFileSync('src/layouts/DefaultLayout.vue', 'utf8').includes('<AdSlot'))
  check('  el ticket de portada muestra el ahorro', portada.includes('AHORRAS'))
  check('  las muescas del ticket se dibujan', portada.includes('ticket__muescas'))

  console.log('\n=== pie de página ===')
  check('sale en todas las vistas', portada.includes('pie__interior') && home.includes('pie__interior'))
  check('  lista las 5 tiendas que seguimos', ['Ripley','Paris','Zara','H&amp;M','Mango'].every((t) => portada.includes(`pie__punto`) && portada.includes(t)))
  check('  cada tienda enlaza al comparador filtrado', /href="\/comparador\?tienda=zara"/.test(portada))
  // El aviso de datos de ejemplo es para quien desarrolla. Lo que importa
  // comprobar no es que salga, sino que NO llegue a producción: eso se
  // verifica sobre el bundle, más abajo.
  const hayPatrocinados = readFileSync(
    'src/shared/components/EnlaceTienda.vue',
    'utf8',
  ).includes('sponsored')

  check('  si hay enlaces con comisión, el pie lo declara',
    !hayPatrocinados || /comisi[óo]n/i.test(portada),
    hayPatrocinados ? 'hay enlaces patrocinados' : 'no hay enlaces con comisión')

  check('  y no se contradice diciendo que no hay afiliación',
    !/no est[áa] afiliad/i.test(portada))
  // La ciudad se lee de la configuración, NO escrita a mano aquí: estaba
  // fijada a «Santiago de Chile» y la prueba se rompió sola en cuanto el
  // proyecto la cambió, sin que hubiera ningún defecto.
  const { CIUDAD } = await load('/src/shared/config/sitio.js')

  check('  lleva el año en curso y la ciudad',
    portada.includes(String(new Date().getFullYear())) && portada.includes(CIUDAD),
    CIUDAD)
  check('  dice cada cuánto se miden los precios y cuál manda',
    portada.includes('una vez al') && portada.includes('al pagar'))

  // Las cinco columnas de la referencia, más las nuestras.
  for (const columna of ['Navega', 'Tiendas', 'Legal', 'Contacto']) {
    check(`  columna "${columna}"`, portada.includes(`>${columna}<`))
  }

  // Cinco sitios prometían «escríbenos» sin decir a dónde. El correo tiene que
  // estar en el pie Y en las tres páginas legales, o la promesa sigue rota.
  const correo = /CONTACTO = '([^']+)'/.exec(
    readFileSync('src/shared/config/sitio.js', 'utf8'),
  )?.[1]

  check('  el pie da una dirección de contacto',
    Boolean(correo) && portada.includes(`mailto:${correo}`), correo)

  for (const ruta of ['/terminos', '/privacidad', '/preguntas']) {
    const html = await render(ruta, () => {})
    check(`  ${ruta} dice a dónde escribir`, html.includes(correo))
  }

  check('  la misión no es la de cualquier comparador',
    portada.includes('todos los días') && portada.includes('vitrina'))

  // El enlace del pie tiene que dejar el comparador con esa única tienda: si
  // fuera decorativo, la vista se abriría con las cinco marcadas igual.
  {
    const p = createPinia()
    setActivePinia(p)
    const st = useComparadorStore()
    await st.cargarProductos()

    const router = createRouter({ history: createMemoryHistory(), routes })
    const app = createSSRApp(App)
    app.use(p).use(router)
    await router.push('/comparador?tienda=zara')
    await router.isReady()
    await renderToString(app)

    check('  y el filtro se aplica al llegar', st.tiendasActivas.join() === 'zara', st.tiendasActivas.join())
    st.soloTienda('inventada')
    check('  una tienda inexistente no deja la lista vacía', st.tiendasActivas.length === 5)
  }

  console.log('\n=== cabecera ===')
  check('el buscador está en la cabecera', portada.includes('buscar-cabecera'))
  check('  hay menú de tiendas', portada.includes('aria-label="Elegir tiendas"'))
  check('  hay menú de configuración', portada.includes('aria-label="Configuración"'))
  check('  hay menú de cuenta', portada.includes('aria-label="Mi cuenta"'))
  // El contenido del menú sólo existe cuando está abierto, así que aquí se
  // comprueba la pantalla de sesión, que es donde vive el botón de verdad.
  const entrar = await render('/login', () => {})
  check('  /login ofrece el botón de Google',
    entrar.includes('Continuar con Google') && entrar.includes('google__logo'))
  check('  /registro es la misma pantalla, con otra copia',
    (await render('/registro', () => {})).includes('Registrarme con Google'))
  check('  enlaza a términos y privacidad antes de entrar',
    entrar.includes('href="/terminos"') && entrar.includes('href="/privacidad"'))

  // ——— /login lleva el marco completo ———
  check('  /login lleva la cabecera completa, con buscador y filtro de tiendas',
    entrar.includes('buscar-cabecera') && entrar.includes('Elegir tiendas'))
  check('  y el pie completo, no el reducido',
    entrar.includes('acceso__barra') === false &&
      entrar.includes('pie__') && entrar.includes('href="/preguntas"'))
  check('  la tarjeta va centrada, no pegada a la izquierda',
    /\.acceso__tarjeta \{[^}]*margin:[^;]*auto/
      .test(readFileSync('src/assets/acceso.css', 'utf8')))
  // Con la cabecera y el pie puestos, una tarjeta de 400px pegada arriba deja
  // un vacío de varios cientos de píxeles hasta el pie en pantallas altas.
  check('  y también en vertical, que si no queda un hueco enorme',
    entrar.includes('marco__contenido--centrado') &&
      /\.marco__contenido--centrado \{[^}]*justify-content: center/
        .test(readFileSync('src/layouts/DefaultLayout.vue', 'utf8')))

  // El enlace vive dentro del menú de cuenta, que en SSR está cerrado y no
  // renderiza. Así que se comprueba en el origen y en el router, no en el HTML.
  {
    const r = createRouter({ history: createMemoryHistory(), routes })
    check('  el nombre de ruta «login» resuelve a /login',
      r.resolve({ name: 'login' }).path === '/login')

    const conEntrar = ['src/shared/components/AppHeader.vue',
      'src/core/router/index.js',
      'src/modules/cuenta/views/RetornoGoogleView.vue']
      .filter((f) => readFileSync(f, 'utf8').includes("name: 'entrar'"))
    check('  no queda ningún enlace apuntando al nombre viejo',
      conEntrar.length === 0, conEntrar.join(' '))
  }

  // La URL vieja estuvo publicada: si deja de redirigir, se pierden los
  // enlaces guardados y lo que Google tenga indexado.
  {
    const r = createRouter({ history: createMemoryHistory(), routes })
    await r.push('/entrar')
    check('  /entrar sigue redirigiendo a /login', r.currentRoute.value.path === '/login',
      r.currentRoute.value.path)
  }

  // /auth/google se queda desnuda a propósito: es una pantalla de paso de dos
  // segundos y cualquier enlace ahí interrumpe el proceso a medias.
  const retorno = await render('/auth/google', () => {})
  check('  la vuelta de Google sí se queda sin cabecera',
    retorno.includes('acceso__barra') && !retorno.includes('buscar-cabecera'))

  check('  el logo de la cabecera es el mismo que el favicon',
    portada.includes('M5 16 15 5h10v10L14 27Z') &&
      readFileSync('public/favicon.svg', 'utf8').includes('M5 16 15 5h10v10L14 27Z'))

  // ——— el nombre de la marca ———
  const fuenteCabecera = readFileSync('src/shared/components/AppHeader.vue', 'utf8')
  const marcaHtml = portada.slice(
    portada.indexOf('barra__marca'), portada.indexOf('barra__buscador'))

  const posA = marcaHtml.indexOf('barra__nombre-a')
  const posB = marcaHtml.indexOf('barra__nombre-b')
  check('el nombre sale entero y partido en dos piezas',
    posA !== -1 && posB > posA &&
      marcaHtml.includes('cacha') && marcaHtml.includes('el precio'))

  check('  el ° va en color de acento, que es lo que distingue la marca',
    /barra__nombre-a[^>]*>[^<]*<em[^>]*>°<\/em>/.test(marcaHtml) &&
      /\.barra__marca em \{[^}]*color: var\(--cep-accent\)/.test(fuenteCabecera))

  // El ojo tiene que agarrar «cacha°» antes que «el precio». Si las dos mitades
  // acaban con el mismo peso y el mismo color, deja de ser un logotipo.
  const pesoA = /\.barra__nombre-a \{[^}]*font-weight: (\d+)/.exec(fuenteCabecera)?.[1]
  const pesoB = /\.barra__nombre-b \{[^}]*font-weight: (\d+)/.exec(fuenteCabecera)?.[1]
  check('  la primera palabra pesa más que la segunda',
    pesoA !== undefined && pesoB !== undefined && Number(pesoA) > Number(pesoB),
    `${pesoA} vs ${pesoB}`)
  check('  y la segunda va en otro tono',
    /\.barra__nombre-b \{[^}]*color: var\(--cep-muted\)/.test(fuenteCabecera))

  // Vue se come el salto de línea entre los dos <span>, así que en el HTML no
  // queda espacio entre las palabras. Si alguien quita el gap del CSS, el
  // nombre se lee «cacha°el precio».
  const entre = /barra__nombre-a[\s\S]*?<\/span>([\s\S]*?)<span class="barra__nombre-b/
    .exec(marcaHtml)?.[1]
  const espacioEnMarcado = entre !== undefined && /\s/.test(entre)
  check('  las dos palabras no se pegan',
    espacioEnMarcado || /\.barra__nombre \{[^}]*gap:/.test(fuenteCabecera),
    espacioEnMarcado ? 'espacio en el marcado' : 'hueco por gap en el CSS')

  check('  va a tamaño de titular, no de etiqueta de menú',
    /\.barra__nombre \{[^}]*font-size: var\(--cep-fs-2xl\)/.test(fuenteCabecera))
  check('  con el interletraje apretado, como un logotipo',
    /\.barra__nombre \{[^}]*letter-spacing: -/.test(fuenteCabecera))
  check('  baja un escalón cuando comparte fila con el buscador',
    /min-width: 900px\) and \(max-width: 1099px\)[\s\S]{0,140}?--cep-fs-xl/
      .test(fuenteCabecera))
  check('  y bajo 480px desaparece: ahí identifica el símbolo',
    /max-width: 479px\)[\s\S]{0,200}?\.barra__nombre[\s\S]{0,120}?display: none/
      .test(fuenteCabecera))

  console.log('\n=== páginas legales ===')
  for (const [ruta, titulo, marca] of [
    ['/terminos', 'Términos y condiciones', 'Última actualización'],
    ['/privacidad', 'Política de privacidad', 'Última actualización'],
    ['/preguntas', 'Preguntas frecuentes', '¿De dónde salen los precios?'],
  ]) {
    const html = await render(ruta, () => {})
    check(`${ruta} renderiza`, html.includes(titulo) && html.includes(marca))
  }

  const terminos = await render('/terminos', () => {})
  check('las páginas legales van a ancho de lectura',
    terminos.includes('marco__contenido--lectura'))
  check('  y no llevan ninguna marca de trabajo interno',
    // Sin la bandera `i` para TODO/FIXME: con ella, "todo" y "todos" —palabras
    // normales en español— daban falso positivo.
    !/borrador|pendiente de revisión/i.test(terminos) &&
      !/\b(TODO|FIXME|XXX)\b/.test(terminos))

  check('el pie enlaza a las tres páginas legales',
    ['/terminos', '/privacidad', '/preguntas'].every((r) => portada.includes(`href="${r}"`)))

  console.log('\n=== ficha de producto con datos de la API real ===')
  {
    const { adaptarProductos } = await load(
      '/src/modules/comparador/services/producto.adapter.js',
    )
    const real = adaptarProductos(
      [{ id: 2, nombre: 'Polera básica', descripcion: 'Cuello redondo', precio: 9990, catalogoId: 2, activo: true }],
      [{ id: 2, nombre: 'Poleras' }],
    )[0]

    const p = createPinia()
    setActivePinia(p)
    const st = useComparadorStore()
    st.producto = real
    st.tiendas = [{ id: 'catalogo', nombre: 'Precio publicado', color: '#0b5cad' }]
    st.cargarProducto = async () => {}

    const router = createRouter({ history: createMemoryHistory(), routes })
    const app = createSSRApp(App)
    app.use(p).use(router)
    await router.push('/producto/2')
    await router.isReady()
    const html = await renderToString(app)

    check('resuelve el nombre de la fuente, no el id crudo',
      html.includes('Precio publicado') && !/>\s*catalogo\s*</.test(html))
    check('  sin marca no deja el separador colgando',
      !html.includes('> · ') && html.includes('Poleras'))
    check('  muestra la descripción del producto', html.includes('Cuello redondo'))
    check('  con una sola fuente no dice "Más barato en"',
      !html.includes('Más barato en'))
    check('  y el bloque se titula "Dónde comprarla", sin prometer elección',
      html.includes('Dónde comprarla') && !html.includes('Elige tu tienda'))
    check('  el historial se anuncia aunque no haya datos todavía',
      html.includes('Historial de precios') &&
        html.includes('Todavía no tenemos historial'))
    check('  la ficha tiene ilustración', html.includes('detalle__marco'))
  }

  console.log('\n=== la estructura aguanta el fallo ===')
  {
    // Portada con el backend caído y SIN datos: lo que no depende de datos
    // tiene que seguir en pie. Antes un error sustituía la vista entera.
    const rota = await render('/', async (st) => {
      st.productos = []
      st.error = 'Sin conexión con el servicio.'
    })

    check('el titular sigue en pie sin datos', rota.includes('hero__titulo'))
    for (const seccion of [
      'Lo más reciente',
      'Lo más visto',
      'Categorías populares',
    ]) {
      check(`  la sección "${seccion}" sigue ahí`, rota.includes(seccion))
    }
    check('  el pie sigue ahí', rota.includes('pie__interior'))
    check('  y se avisa con salida para reintentar',
      rota.includes('role="status"') && rota.includes('Reintentar'))
    check('  sin tapar la página con un error a pantalla completa',
      !rota.includes('aviso__titulo'))

    // Con datos cargados y un fallo posterior, los precios NO desaparecen.
    const vieja = await render('/', async (st) => {
      await st.cargarProductos()
      st.error = 'No pudimos actualizar.'
      st.actualizadoEn = Date.now() - 10 * 60 * 1000
    })

    check('con datos viejos los productos siguen visibles',
      vieja.includes('Polera b'))
    check('  y el aviso dice de cuándo son',
      /hace 10 minutos/.test(vieja), 'hace 10 minutos')

    // Cargando: esqueletos con la forma real.
    const cargando = await render('/', (st) => {
      st.productos = []
      st.cargando = true
    })

    check('mientras carga se ven siluetas de tarjeta',
      cargando.includes('silueta'), 'silueta')
    check('  marcadas como decorativas para el lector de pantalla',
      cargando.includes('aria-hidden="true"'))
  }

  console.log('\n=== el store no borra lo que ya tenías ===')
  {
    // Con un backend que no existe, la petición falla DE VERDAD y se ejecuta
    // el catch del store. Ponerle `error` a mano no serviría: la comprobación
    // pasaría igual aunque el catch siguiera vaciando la lista.
    const muerto = await createServer({
      mode: 'fallo',
      // hmr:false porque ya hay otra instancia de Vite arriba y las dos
      // pelearían por el puerto del WebSocket.
      server: { middlewareMode: true, hmr: false },
      appType: 'custom',
      logLevel: 'error',
    })

    try {
      setActivePinia(createPinia())
      const { useComparadorStore: usarStore } = await muerto.ssrLoadModule(
        '/src/modules/comparador/store/comparador.store.js',
      )
      const st = usarStore()

      // Se simula que ya había datos en pantalla de una carga anterior.
      st.productos = [
        { id: '1', nombre: 'Polera', categoria: '', marca: '', precios: [], historial: [] },
        { id: '2', nombre: 'Jeans', categoria: '', marca: '', precios: [], historial: [] },
      ]
      const marcaPrevia = Date.now() - 60_000
      st.actualizadoEn = marcaPrevia

      await st.cargarProductos({ forzar: true })

      check('tras un fallo real de red hay error', st.error !== null, st.error)
      check('  y los productos NO se borraron',
        st.productos.length === 2, `${st.productos.length} productos`)
      check('  se marcan como desactualizados', st.datosDesactualizados === true)
      check('  la marca de actualización no se movió',
        st.actualizadoEn === marcaPrevia)
      check('  y deja de estar cargando', st.cargando === false)
    } finally {
      await muerto.close()
    }
  }

  console.log('\n=== las siluetas encajan con el contenido ===')
  {
    // Si los anchos no coinciden, el carrusel se recoloca al llegar los datos.
    const anchoDe = (archivo, clase) =>
      new RegExp(`\\.${clase}\\s*\\{[^}]*width:\\s*(\\d+)px`).exec(
        readFileSync(archivo, 'utf8'),
      )?.[1]

    const tarjeta = anchoDe(
      'src/modules/comparador/components/ProductoCard.vue',
      'tarjeta--compacta',
    )
    const silueta = anchoDe(
      'src/modules/comparador/components/ProductoCardSkeleton.vue',
      'silueta--compacta',
    )

    check('la silueta compacta mide lo mismo que la tarjeta compacta',
      Boolean(tarjeta) && tarjeta === silueta, `tarjeta=${tarjeta} silueta=${silueta}`)
  }

  console.log('\n=== bloques de anuncio ===')
  {
    const { renderToString: pintar } = await import('vue/server-renderer')
    const { createSSRApp: crear } = await import('vue')
    const AdSlot = (await load('/src/shared/components/AdSlot.vue')).default

    const render = (props) => pintar(crear(AdSlot, props))

    // Sin id de bloque no se pinta NADA. Es lo que evita la caja vacía en
    // producción mientras los bloques no estén creados en AdSense.
    const vacio = await render({ bloque: '' })
    check('sin id de bloque no renderiza nada', vacio.trim() === '<!---->',
      JSON.stringify(vacio.slice(0, 60)))
    check('  ni la etiqueta "Publicidad"', !vacio.includes('Publicidad'))
    check('  ni el <ins> de AdSense', !vacio.includes('adsbygoogle'))

    // La portada no debe llevar huecos mientras no haya bloques configurados.
    check('la portada no muestra huecos sin configurar',
      !portada.includes('adsbygoogle') && !portada.includes('>Publicidad<'))

    // Con id de bloque sí se pinta, con los atributos que espera AdSense.
    const lleno = await render({ bloque: '1234567890', alto: 96 })

    check('con id de bloque renderiza el <ins> de AdSense',
      lleno.includes('class="adsbygoogle'), 'ins presente')
    check('  con el data-ad-slot correcto',
      lleno.includes('data-ad-slot="1234567890"'))
    check('  con el data-ad-client del editor',
      /data-ad-client="ca-pub-\d+"/.test(lleno))
    check('  y etiquetado como publicidad', lleno.includes('Publicidad'))
    check('  reservando el alto para que no salte la página',
      lleno.includes('min-height:96px'), 'min-height')

    // El script de AdSense no puede colarse cuando no hay nada que mostrar.
    const adsense = await load('/src/shared/services/adsense.js')
    check('el script de AdSense no se inyecta en el <head> de index.html',
      !readFileSync('index.html', 'utf8').includes('adsbygoogle.js'))
    check('  y cargarAdsense rechaza si no hay editor configurado',
      adsense.ADSENSE_ACTIVO === false
        ? await adsense.cargarAdsense().then(() => false, () => true)
        : true)
  }

  console.log('\n=== armador de outfits ===')
  {
    const partes = await load('/src/modules/outfits/data/partes.js')

    // Cada categoría del catálogo cae en una parte, o en ninguna a propósito.
    setActivePinia(createPinia())
    const cat = useComparadorStore()
    await cat.cargarProductos()

    const sinParte = [...new Set(cat.productos.map((p) => p.categoria))].filter(
      (c) => partes.parteDeCategoria(c) === null,
    )
    check('todas las categorías del catálogo tienen su parte',
      sinParte.length === 0, sinParte.join(', ') || 'ninguna suelta')

    check('una categoría desconocida no rompe nada',
      partes.parteDeCategoria('Paraguas') === null &&
        partes.partesQueOcupa({ categoria: 'Paraguas' }).length === 0)

    check('un vestido ocupa torso y piernas',
      partes.partesQueOcupa({ categoria: 'Vestidos' }).join() === 'torso,piernas')

    const { useOutfitStore } = await load('/src/modules/outfits/store/outfit.store.js')
    const outfit = useOutfitStore()

    // ——— las cuatro ranuras tienen de dónde elegir ———
    for (const parte of partes.IDS_PARTES) {
      check(`  hay prendas para ${parte}`, outfit.opcionesPara(parte).length > 0,
        `${outfit.opcionesPara(parte).length} opciones`)
    }

    // ——— el dinero ———
    outfit.limpiar()
    outfit.ponerPrenda('cabeza', '7')
    outfit.ponerPrenda('torso', '1')
    outfit.ponerPrenda('piernas', '2')
    outfit.ponerPrenda('pies', '9')

    check('el outfit se completa', outfit.completo === true)

    // El total se compara contra un cálculo INDEPENDIENTE —el mínimo real de
    // cada prenda— y no contra una suma del propio desglose: eso último era
    // tautológico y pasaba igual aunque el desglose eligiera la oferta más cara.
    const minimoReal = outfit.piezas.reduce(
      (t, { producto }) =>
        t + Math.min(...producto.precios.filter((o) => o.stock).map((o) => o.precio)),
      0,
    )
    check('  el total es la suma de los precios más bajos',
      outfit.total === minimoReal, `${outfit.total} vs ${minimoReal}`)

    // Ninguna pieza puede venir de una oferta agotada.
    check('  ninguna pieza sale de una tienda sin stock',
      outfit.desglose.every(({ producto, oferta }) =>
        producto.precios.find((o) => o.tienda === oferta.tienda)?.stock === true))

    // "Todo en una tienda" sólo vale si esa tienda tiene TODO con stock.
    const unica = outfit.mejorTiendaUnica
    check('  la tienda única tiene todas las piezas con stock',
      unica !== null &&
        outfit.piezas.every(({ producto }) =>
          producto.precios.some((o) => o.tienda === unica.tienda && o.stock)),
      unica ? `${unica.tienda} · ${unica.total}` : 'ninguna')

    check('  y el ahorro es la diferencia entre las dos formas',
      outfit.ahorroRepartiendo === Math.max(0, unica.total - outfit.total),
      String(outfit.ahorroRepartiendo))

    // ——— vestido: no se puede llevar con pantalón ———
    outfit.limpiar()
    outfit.ponerPrenda('piernas', '2')
    outfit.ponerPrenda('torso', '4')

    check('un vestido cubre las piernas y bloquea esa ranura',
      outfit.partesBloqueadas.includes('piernas') &&
        outfit.prendas.piernas?.id === outfit.prendas.torso?.id)
    check('  y no se cuenta dos veces en el total',
      outfit.piezas.length === 1 && outfit.total === outfit.desglose[0].oferta.precio,
      String(outfit.total))

    // ——— outfit a medias ———
    outfit.limpiar()
    outfit.ponerPrenda('pies', '9')
    check('con el outfit a medias sólo cuenta lo que hay',
      outfit.completo === false && outfit.piezas.length === 1)

    // ——— plantillas ———
    const { AUTOMATICAS, FIJAS } = await load('/src/modules/outfits/data/plantillas.js')
    const { useOutfitArmado } = await load(
      '/src/modules/outfits/composables/useOutfitArmado.js',
    )
    const { armar, resumir } = useOutfitArmado()

    for (const plantilla of AUTOMATICAS) {
      const armado = armar(plantilla)
      const resumen = resumir(armado)

      check(`la plantilla "${plantilla.nombre}" trae prendas reales`,
        resumen.piezas.length > 0, `${resumen.piezas.length} piezas · ${resumen.total}`)
    }

    check('las plantillas fijas descartan lo que ya no existe',
      resumir(armar({ ...FIJAS[0], prendas: { ...FIJAS[0].prendas, pies: 'no-existe' } }))
        .piezas.length === Object.keys(FIJAS[0].prendas).length - 1)
  }

  console.log('\n=== vistas del armador ===')
  {
    // El outfit se guarda en localStorage y las pruebas anteriores dejaron
    // prendas puestas: se limpia para ver el estado inicial de verdad.
    almacen.delete('cep:outfit')

    const armar = await render('/armar', (st) => st.cargarProductos())

    check('/armar renderiza las cuatro ranuras',
      ['Cabeza', 'Torso', 'Piernas', 'Pies'].every((p) => armar.includes(p)))
    check('  con el resumen al lado', armar.includes('Tu outfit'))
    check('  y dice qué hacer cuando está vacío',
      armar.includes('Ve eligiendo prendas'))

    // Sin catálogo la estructura aguanta, igual que el resto del sitio.
    const vacia = await render('/armar', (st) => {
      st.productos = []
      st.error = 'Sin conexión.'
    })
    check('sin datos las ranuras siguen en pie',
      vacia.includes('Cabeza') && vacia.includes('Torso'))
    check('  y cada una dice que no hay prendas',
      vacia.includes('Todavía no tenemos prendas para esta parte'))

    const galeria = await render('/outfits', (st) => st.cargarProductos())

    check('/outfits renderiza las plantillas',
      galeria.includes('Lo más barato') && galeria.includes('Fin de semana'))
    check('  con precio y número de tiendas',
      /Desde/.test(galeria) && /\d+ tiendas?/.test(galeria))
    check('  distingue las automáticas de las elegidas a mano',
      galeria.includes('Se arma solo') && galeria.includes('Selección propia'))

    // Con una sola fuente de precio no se puede fingir una comparación: el
    // resumen enseñaría el mismo total dos veces, una de ellas como "todo en
    // una sola tienda", que con un único origen no significa nada.
    const unaFuente = await render('/armar', async (st, o) => {
      await st.cargarProductos()
      st.tiendas = [{ id: 'catalogo', nombre: 'Precio publicado', color: '#0b5cad' }]
      o?.ponerPrenda?.('torso', st.productos[0].id)
    })

    check('con una sola fuente no se inventa la comparación',
      !unaFuente.includes('Todo en una sola tienda') &&
        !unaFuente.includes('Cada pieza donde está más barata'))
    check('  y el total se llama por su nombre',
      unaFuente.includes('Total del outfit'))

    check('la portada ofrece vestirse completo',
      portada.includes('Vístete completo por menos'))
    check('la barra lleva al armador', portada.includes('Armar outfit'))
  }

  console.log('\n=== ficha de producto ===')
  {
    const ver = (id) =>
      render(`/producto/${id}`, async (st) => {
        await st.cargarProductos()
        st.producto = st.productoById(id)
        st.cargarProducto = async () => {}
      })

    const completa = await ver('1')

    // ——— migas de pan ———
    check('hay migas de pan', completa.includes('Dónde estás'))
    check('  que llevan a la categoría',
      /href="\/comparador\?categoria=Poleras"/.test(completa))
    check('  y la última marca dónde estás',
      /aria-current="page"/.test(completa))

    // ——— orden de las secciones ———
    const pos = (t) => completa.indexOf(t)

    check('la ficha va en una sola columna',
      !completa.includes('detalle__columnas'))
    check('  y "elige tu tienda" va antes que las características',
      pos('Elige tu tienda') > 0 &&
        pos('Elige tu tienda') < pos('Características'))
    check('  la descripción y lo destacado van después',
      pos('Descripción') > pos('Elige tu tienda') &&
        pos('Lo que hay que saber') > pos('Elige tu tienda'))

    // ——— el sello sigue arriba, junto a la prenda ———
    check('el sello del descuento se estampa',
      /aria-label="Precio de hoy: [^"]+"/.test(completa),
      /aria-label="(Precio de hoy: [^"]+)"/.exec(completa)?.[1] ?? 'no está')
    check('  y el mínimo registrado se dice en la cabecera',
      completa.includes('Mínimo que hemos visto'))

    // ——— identidad del prototipo ———
    check('los tickets conservan sus muescas',
      (completa.match(/ticket__muescas/g) ?? []).length >= 6,
      `${(completa.match(/ticket__muescas/g) ?? []).length} muescas`)

    // ——— características agrupadas ———
    check('las características van agrupadas por bloque',
      completa.includes('Materiales y confección') && completa.includes('Cuidado'))
    check('  con su nota dentro del grupo al que se refiere',
      completa.includes('encoge algo en el primer lavado'))
    check('  y lo destacado en viñetas', completa.includes('Algodón peinado'))

    // ——— formato plano de specs: lo que daría la API ———
    const { default: Ficha } = await load(
      '/src/modules/comparador/components/FichaCaracteristicas.vue',
    )
    const { renderToString: pintar } = await import('vue/server-renderer')
    const { createSSRApp: crear } = await import('vue')

    const plano = await pintar(
      crear(Ficha, { specs: { Material: 'Algodón', Corte: 'Regular' } }),
    )
    check('un formato plano de specs también se pinta',
      plano.includes('Algodón') && plano.includes('Corte'))

    const sinSpecs = await pintar(crear(Ficha, { specs: [] }))
    check('  y sin specs no deja un bloque vacío',
      sinSpecs.trim() === '<!---->')

    // Un producto sin ficha no deja huecos. Se construye el caso en vez de
    // buscar un producto pelado en el catálogo: ahora todos tienen ficha, y una
    // prueba que depende de eso deja de comprobar nada en cuanto cambian los
    // datos.
    const pelada = await render('/producto/1', async (st) => {
      await st.cargarProductos()
      const base = st.productoById('1')

      st.producto = { ...base, specs: [], pros: [], contras: [], destacadas: [] }
      st.cargarProducto = async () => {}
    })

    check('un producto sin características no deja huecos',
      !pelada.includes('>Características<') &&
        !pelada.includes('A favor y en contra'),
      'las secciones vacías no se pintan')
    check('  pero lo que sí tiene se sigue viendo',
      pelada.includes('Elige tu tienda') && pelada.includes('Historial de precios'))
  }

  console.log('\n=== elige tu tienda ===')
  {
    const { default: Elige } = await load(
      '/src/modules/comparador/components/EligeTuTienda.vue',
    )
    const { renderToString: pintar } = await import('vue/server-renderer')
    const { createSSRApp: crear } = await import('vue')

    setActivePinia(createPinia())
    const cat = useComparadorStore()
    cat.tiendas = [
      { id: 'a', nombre: 'Ripley', color: '#111' },
      { id: 'b', nombre: 'Paris', color: '#222' },
      { id: 'c', nombre: 'Zara', color: '#333' },
    ]

    const html = await pintar(
      crear(Elige, {
        ofertas: [
          { tienda: 'a', precio: 30000, precioLista: 40000, stock: true, url: 'x', medioPago: 'Tarjeta Ripley', condicion: 'nueva' },
          // La más barata pero AGOTADA: no puede encabezar la lista.
          { tienda: 'b', precio: 10000, precioLista: 10000, stock: false, url: 'y', medioPago: 'Con todo medio de pago', condicion: 'ultima-talla' },
          { tienda: 'c', precio: 20000, precioLista: 18000, stock: true, url: 'z' },
        ],
      }),
    )

    const orden = ['Ripley', 'Paris', 'Zara']
      .map((n) => ({ n, i: html.indexOf(`>\n            ${n}` ) >= 0 ? html.indexOf(n) : html.indexOf(n) }))
      .sort((x, y) => x.i - y.i)
      .map((x) => x.n)

    check('ordena de más barata a más cara', orden[0] === 'Zara', orden.join(' → '))
    check('  y la agotada va al final aunque sea la más barata',
      orden[orden.length - 1] === 'Paris', orden.join(' → '))
    check('  la agotada no ofrece ir a comprar',
      /Paris[\s\S]{0,600}?Agotado/.test(html))

    check('cada tienda muestra su medio de pago',
      html.includes('Tarjeta Ripley') && html.includes('Con todo medio de pago'))
    check('  y su condición cuando no es la normal',
      html.includes('Últimas tallas'))
    check('  sin dejar hueco cuando la oferta no los trae',
      (html.match(/class="etiqueta/g) ?? []).length === 3,
      `${(html.match(/class="etiqueta/g) ?? []).length} etiquetas para 4 posibles`)

    // El precio normal sólo se tacha si de verdad es mayor.
    check('el precio normal se tacha sólo si es mayor',
      html.includes('−25%') && !html.includes('−-11%') && !/18\.000/.test(html),
      'Zara tiene precioLista menor: no se tacha')

    check('hay control de orden', html.includes('Ordenar por'))
  }

  console.log('\n=== gráfico de precios ===')
  {
    const conHistorial = await render('/producto/1', async (st) => {
      await st.cargarProductos()
      st.producto = st.productoById('1')
      st.cargarProducto = async () => {}
    })

    const trozo = conHistorial.slice(conHistorial.indexOf('Historial de precios'))

    check('dibuja una línea por tienda',
      (trozo.match(/<polyline/g) ?? []).length >= 2,
      `${(trozo.match(/<polyline/g) ?? []).length} series`)

    // Sin escala de precios sólo se ve la forma de la curva, no cuánto cuesta.
    // Es la parte que faltaba en la versión anterior.
    check('  con escala de precios legible', /&gt;?\$\d+k?</.test(trozo) || />\$\d+k?</.test(trozo),
      (trozo.match(/>\$\d+k?</g) ?? []).join(' '))
    check('  y líneas guía a esa altura',
      (trozo.match(/<line /g) ?? []).length >= 4)
    check('  un punto en cada dato',
      (trozo.match(/<circle/g) ?? []).length >= 6)

    check('  se puede leer el precio de un día concreto',
      trozo.includes('lectura__fecha'))
    check('  con la variación respecto del anterior',
      /▼|▲/.test(trozo))
    check('  y se navega con el teclado',
      trozo.includes('tabindex="0"') && trozo.includes('role="img"'))

    // Las fechas del eje no pueden salir en crudo.
    check('  las fechas van legibles, no en ISO',
      !/>\s*20\d\d-\d\d-\d\d\s*</.test(trozo),
      (trozo.match(/>\s*\d\d-[a-z]{3}\s*</g) ?? []).slice(0, 3).join(' '))

    // Y con el día correcto. "2026-08-15" se lee como medianoche UTC; al
    // formatearlo en hora de Chile caía en el 14, y un precio del 15 se
    // mostraba como del día anterior. La comprobación de formato no lo veía:
    // "14-ago" tampoco es ISO.
    const { formatearFecha } = await load('/src/shared/utils/formato.js')

    check('  y con el día correcto, sin desfase de huso',
      formatearFecha('2026-08-15').startsWith('15') &&
        formatearFecha('2026-01-01').startsWith('01'),
      `${formatearFecha('2026-08-15')} · ${formatearFecha('2026-01-01')}`)

    // El día sin dato de una tienda se salta, no vale 0.
    const { default: Grafico } = await load(
      '/src/modules/comparador/components/GraficoPrecios.vue',
    )
    const { renderToString: pintar } = await import('vue/server-renderer')
    const { createSSRApp: crear } = await import('vue')

    setActivePinia(createPinia())
    const cat = useComparadorStore()
    cat.tiendas = [
      { id: 'a', nombre: 'A', color: '#111' },
      { id: 'b', nombre: 'B', color: '#222' },
    ]

    const hueco = await pintar(
      crear(Grafico, {
        historial: [
          { fecha: '2026-07-01', precios: { a: 100 } },
          { fecha: '2026-08-01', precios: { a: 90, b: 80 } },
        ],
      }),
    )
    const lineas = [...hueco.matchAll(/<polyline points="([^"]*)"/g)].map((m) => m[1])

    check('un día sin dato de una tienda se salta, no vale 0',
      lineas.some((l) => l.split(' ').length === 1),
      lineas.map((l) => l.split(' ').length).join(' y ') + ' puntos')

    // Sin historial se dice, en vez de esconder la sección.
    const vacio = await pintar(crear(Grafico, { historial: [] }))
    check('sin historial lo dice en vez de desaparecer',
      vacio.includes('Todavía no tenemos historial'))
    const unSolo = await pintar(
      crear(Grafico, { historial: [{ fecha: '2026-07-01', precios: { a: 100 } }] }),
    )
    check('  con un solo día tampoco dibuja una línea falsa',
      !unSolo.includes('<polyline'))
  }

  console.log('\n=== enlaces a la tienda ===')
  {
    const detalle = await render('/producto/1', async (st) => {
      await st.cargarProductos()
      st.producto = st.productoById('1')
      st.cargarProducto = async () => {}
    })

    check('la ficha ofrece ir a la tienda', detalle.includes('Ver en'))

    // rel="sponsored" lo exige Google para enlaces que pueden dar comisión.
    // Sin él, un enlace de afiliado se lee como recomendación editorial y
    // penaliza el posicionamiento de TODO el sitio.
    const enlaces = [...detalle.matchAll(/<a[^>]*href="(https?:\/\/[^"]+)"[^>]*>/g)]
      .map((m) => m[0])
      .filter((a) => !a.includes('google'))

    check('  los enlaces salientes van marcados como patrocinados',
      enlaces.length > 0 && enlaces.every((a) => a.includes('sponsored')),
      `${enlaces.length} enlaces`)
    check('  y con noopener, para que el destino no toque nuestra página',
      enlaces.every((a) => a.includes('noopener')))
    check('  se abren en pestaña nueva, sin perder la comparación',
      enlaces.every((a) => a.includes('_blank')))
    check('  y se avisa de que abren fuera',
      detalle.includes('se abre en la tienda'))

    // Una oferta agotada no lleva a ninguna parte: no se puede comprar.
    //
    // Se parte por filas en vez de buscar con una ventana de caracteres: la
    // primera versión usaba /Agotado[\s\S]{0,220}?<\/tr>/ y al aparecer el
    // enlace la fila pasaba de 220 caracteres, la expresión dejaba de
    // encontrarla y every() sobre un array vacío daba `true`. La prueba se
    // apagaba sola justo cuando tenía que saltar.
    // La lista pasó de <tr> a <li class="oferta">: se parte por ahí.
    const filas = detalle.split('<li').filter((f) => f.includes('Agotado'))

    check('  la ficha tiene alguna oferta agotada que comprobar',
      filas.length > 0, `${filas.length} filas`)
    check('  y ninguna ofrece ir a comprar',
      filas.length > 0 && filas.every((f) => !f.includes('Ver en')))

    // Sin URL no se pinta el botón: es preferible que falte a mandar a alguien
    // a una dirección adivinada. Es el caso de la API real hoy.
    const { adaptarProductos } = await load(
      '/src/modules/comparador/services/producto.adapter.js',
    )
    const sinUrl = adaptarProductos(
      [{ id: 1, nombre: 'X', precio: 100, catalogoId: 2, activo: true }],
      [{ id: 2, nombre: 'Poleras' }],
    )[0]

    check('  la API sin url deja la oferta sin enlace',
      sinUrl.precios[0].url === null)

    const EnlaceTienda = (await load('/src/shared/components/EnlaceTienda.vue')).default
    const { renderToString: pintar } = await import('vue/server-renderer')
    const { createSSRApp: crear } = await import('vue')

    const vacio = await pintar(crear(EnlaceTienda, { url: null, tienda: 'Zara' }))
    check('  y sin enlace no se pinta ningún botón', vacio.trim() === '<!---->')
  }

  console.log('\n=== filtros del comparador ===')
  {
    const vista = await render('/comparador', (st) => st.cargarProductos())

    check('la barra lateral ofrece filtrar por categoría',
      vista.includes('Categoría') && vista.includes('Todas'))
    check('  con el número de prendas de cada una', /pildora__cuenta/.test(vista))
    check('  y filtrar por presupuesto', vista.includes('Presupuesto'))
    check('hay control de orden', vista.includes('Ordenar por'))

    // Con una sola fuente de precio el filtro de tiendas se oculta, y antes eso
    // dejaba la barra lateral COMPLETAMENTE vacía: no se podía filtrar por nada.
    const unaFuente = await render('/comparador', async (st) => {
      await st.cargarProductos()
      st.tiendas = [{ id: 'catalogo', nombre: 'Precio publicado', color: '#0b5cad' }]
    })

    check('con una sola tienda la barra lateral NO se queda vacía',
      unaFuente.includes('Categoría') && unaFuente.includes('Presupuesto'))
    check('  y no ofrece elegir entre una sola tienda',
      !unaFuente.includes('Tiendas que quiero ver'))

    setActivePinia(createPinia())
    const st = useComparadorStore()
    await st.cargarProductos()

    // ——— el orden ———
    const precios = () =>
      st.productosFiltrados.map((p) => precioMasBajo(p)?.precio ?? Infinity)

    st.orden = 'barato'
    check('ordena de menor a mayor',
      precios().every((v, i, t) => i === 0 || t[i - 1] <= v))

    st.orden = 'caro'
    const caro = precios().filter((v) => v !== Infinity)
    check('  y de mayor a menor',
      caro.every((v, i, t) => i === 0 || t[i - 1] >= v))
    check('  dejando al final los que no tienen precio',
      precios().indexOf(Infinity) === -1 ||
        precios().indexOf(Infinity) >= caro.length)

    st.orden = 'nuevo'
    const dias = st.productosFiltrados.map((p) => p.agregadoHace ?? 999)
    check('  y por lo más reciente',
      dias.every((v, i, t) => i === 0 || t[i - 1] <= v))
    st.orden = 'barato'

    // ——— el presupuesto ———
    const rango = st.rangoPrecios
    check('el rango de precios sale del catálogo, no inventado',
      rango.min > 0 && rango.max > rango.min, `${rango.min}–${rango.max}`)

    st.precioMaximo = rango.min
    check('el tope de presupuesto acota los resultados',
      st.productosFiltrados.length < st.productos.length &&
        st.productosFiltrados.every((p) => precioMasBajo(p).precio <= rango.min),
      `${st.productosFiltrados.length} prendas`)

    // ——— quitar filtros de uno en uno ———
    st.categoria = st.conteoPorCategoria[0].nombre
    check('los filtros puestos se listan', st.filtrosActivos.length === 2,
      st.filtrosActivos.map((f) => f.tipo).join(', '))

    st.quitarFiltro('precio')
    check('  y se quitan de uno en uno, sin perder el resto',
      st.precioMaximo === null && st.categoria !== '')

    st.limpiarFiltros()
    check('  limpiar los quita todos', st.hayFiltros === false)
  }

  console.log('\n=== anuncios en todas las vistas ===')
  {
    // Con bloques configurados, cada vista de contenido tiene que traer al
    // menos un anuncio. Se simula la configuración porque en las pruebas los
    // ids van vacíos y AdSlot no pinta nada.
    const conBloques = await createServer({
      mode: 'anuncios',
      // hmr:false: ya hay otra instancia de Vite arriba y pelearían por el
      // puerto del WebSocket.
      server: { middlewareMode: true, hmr: false },
      appType: 'custom',
      logLevel: 'error',
    })

    try {
      const { routes: rutasAnuncio } = await conBloques.ssrLoadModule(
        '/src/core/router/routes.js',
      )
      const AppAnuncio = (await conBloques.ssrLoadModule('/src/App.vue')).default
      const { useComparadorStore: usarCat } = await conBloques.ssrLoadModule(
        '/src/modules/comparador/store/comparador.store.js',
      )

      const pintarRuta = async (ruta) => {
        const p = createPinia()
        setActivePinia(p)
        const st = usarCat()
        await st.cargarProductos()

        if (ruta.startsWith('/producto/')) {
          st.producto = st.productoById(ruta.split('/').pop())
          st.cargarProducto = async () => {}
        }

        const router = createRouter({ history: createMemoryHistory(), routes: rutasAnuncio })
        const app = createSSRApp(AppAnuncio)
        app.use(p).use(router)
        await router.push(ruta)
        await router.isReady()

        return renderToString(app)
      }

      for (const ruta of [
        '/',
        '/comparador',
        '/producto/1',
        '/outfits',
        '/armar',
        '/terminos',
        '/privacidad',
        '/preguntas',
        // /login y /registro los pidió el proyecto explícitamente. Ver el aviso
        // de política justo debajo: son pantallas de sesión y AdSense trata ese
        // tipo de página como inventario sin valor.
        '/login',
        '/registro',
      ]) {
        const html = await pintarRuta(ruta)
        const cuantos = (html.match(/class="adsbygoogle/g) ?? []).length

        check(`${ruta} lleva anuncio`, cuantos > 0, `${cuantos} bloque(s)`)
      }

      // ——— y donde NO debe haberlos ———
      //
      // Las políticas de AdSense prohíben anuncios en páginas de error y en
      // páginas sin contenido propio. Saltárselo no da un aviso: puede costar
      // la cuenta entera, y con ella todos los ingresos del sitio.
      for (const [ruta, motivo] of [
        ['/ruta-que-no-existe', 'página de error'],
        ['/auth/google', 'pantalla de paso, sin contenido propio'],
      ]) {
        const html = await pintarRuta(ruta)

        check(`${ruta} NO lleva anuncio`,
          !html.includes('class="adsbygoogle'), motivo)
      }

      nota('AVISO: /login y /registro llevan anuncio por decisión del proyecto.')
      nota('  «Valuable Inventory» de AdSense trata las pantallas de sesión como')
      nota('  páginas sin contenido propio. Para quitarlos: meta.sinAnuncios en')
      nota('  src/modules/cuenta/routes.js (una línea por ruta).')
    } finally {
      await conBloques.close()
    }
  }

  console.log('\n=== filtrado avanzado ===')
  {
    setActivePinia(createPinia())
    const st = useComparadorStore()
    await st.cargarProductos()
    st.limpiarFiltros()

    const TODOS = st.totalResultados

    // ——— palabra clave ———
    st.busqueda = 'jeans'
    check('filtra por palabra clave',
      st.totalResultados > 0 && st.totalResultados < TODOS,
      `${st.totalResultados} de ${TODOS}`)
    check('  buscando también en marca y categoría',
      (() => {
        st.busqueda = 'Zara'
        const n = st.totalResultados
        st.busqueda = ''
        return n > 0
      })())

    // ——— marca ———
    check('las marcas salen del catálogo, no de una lista fija',
      st.marcasDisponibles.length > 1,
      st.marcasDisponibles.map((m) => m.nombre).join(' '))

    st.alternarMarca('Zara')
    check('  filtra por marca',
      st.productosFiltrados.every((p) => p.marca === 'Zara'),
      `${st.totalResultados} prendas`)

    st.alternarMarca('Mango')
    check('  y admite varias a la vez',
      st.productosFiltrados.every((p) => ['Zara', 'Mango'].includes(p.marca)) &&
        st.totalResultados > 0)
    st.limpiarFiltros()

    // ——— talla ———
    check('las tallas salen de las ofertas con stock',
      st.tallasDisponibles.length > 1,
      st.tallasDisponibles.map((t) => t.nombre).join(' '))

    const unaTalla = st.tallasDisponibles[0].nombre
    st.talla = unaTalla
    check(`  filtra por talla ${unaTalla}`,
      st.productosFiltrados.every((p) =>
        p.precios.some((o) => o.stock && (o.tallas ?? []).includes(unaTalla))),
      `${st.totalResultados} prendas`)

    // Una talla que sólo está en una tienda AGOTADA no cuenta: no se puede
    // comprar, y ofrecerla sería mandar a alguien a una talla inexistente.
    // La tienda tiene que ser una REAL: con un id inventado el producto se
    // descartaba por el filtro de tiendas y la comprobación pasaba sin llegar
    // a mirar el stock, que es lo que dice comprobar.
    const tiendaReal = st.tiendasActivas[0]

    st.productos = [
      ...st.productos,
      {
        id: 'z',
        nombre: 'Rara',
        marca: '',
        categoria: '',
        specs: [],
        historial: [],
        precios: [
          { tienda: tiendaReal, precio: 1, stock: false, tallas: ['XXXL'] },
        ],
      },
    ]

    st.talla = 'XXXL'
    check('  una talla sólo disponible en tienda agotada no cuenta',
      st.totalResultados === 0,
      `${st.totalResultados} · tienda ${tiendaReal}`)
    st.limpiarFiltros()
    await st.cargarProductos({ forzar: true })

    // ——— atributos según el tipo de prenda ———
    st.categoria = 'Zapatillas'
    const deCalzado = st.atributosDisponibles.map((a) => a.clave)
    st.categoria = 'Poleras'
    const dePolera = st.atributosDisponibles.map((a) => a.clave)
    st.categoria = ''

    check('los atributos dependen del tipo de prenda',
      deCalzado.includes('Suela') && !dePolera.includes('Suela'),
      `calzado: ${deCalzado.join(', ')}`)
    check('  y sólo se ofrecen los que discriminan',
      st.atributosDisponibles.every((a) => a.valores.length > 1))

    const attr = st.atributosDisponibles.find((a) => a.clave === 'Material')
    st.ponerAtributo('Material', attr.valores[0].valor)
    check('  filtra por atributo',
      st.totalResultados > 0 && st.totalResultados < TODOS,
      `Material=${attr.valores[0].valor} → ${st.totalResultados}`)

    // ——— los conteos ———
    check('los conteos de una faceta excluyen su propio filtro',
      (() => {
        st.limpiarFiltros()
        const antes = st.marcasDisponibles.length
        st.alternarMarca('Zara')
        const despues = st.marcasDisponibles.length
        st.limpiarFiltros()
        // Si se contaran con el filtro puesto, quedaría 1 y no se podría
        // cambiar de marca sin limpiar antes.
        return despues === antes
      })())

    // ——— quitar de uno en uno ———
    st.alternarMarca('Zara')
    st.talla = st.tallasDisponibles[0].nombre
    st.ponerAtributo('Corte', st.atributosDisponibles.find((a) => a.clave === 'Corte')?.valores[0].valor)

    const puestos = st.filtrosActivos.map((f) => f.tipo)
    check('cada filtro puesto se lista por separado',
      puestos.some((t) => t.startsWith('marca:')) &&
        puestos.includes('talla') &&
        puestos.some((t) => t.startsWith('atributo:')),
      puestos.join(' '))

    st.quitarFiltro('marca:Zara')
    check('  y se quita sin tocar los demás',
      st.marcas.length === 0 && st.talla !== '')

    st.limpiarFiltros()
    check('limpiar los quita todos',
      st.hayFiltros === false && st.totalResultados === TODOS)

    // ——— en pantalla ———
    const vista = await render('/comparador', (s2) => s2.cargarProductos())
    check('el panel ofrece marca, talla y atributos',
      vista.includes('>Marca<') && vista.includes('>Talla<') &&
        vista.includes('>Material<'))
    check('  y avisa de que la talla depende de la tienda',
      vista.includes('puede estar en una y no en otra'))
  }

  console.log('\n=== comparar productos ===')
  {
    setActivePinia(createPinia())
    const cat = useComparadorStore()
    await cat.cargarProductos()

    const { useCompararStore, MAXIMO } = await load(
      '/src/modules/comparar/store/comparar.store.js',
    )
    const cmp = useCompararStore()
    cmp.vaciar()

    check('empieza vacío', cmp.cuantos === 0)

    check('se añade y se quita alternando',
      cmp.alternar('1') === 'agregado' &&
        cmp.tiene('1') &&
        cmp.alternar('1') === 'quitado' &&
        !cmp.tiene('1'))

    // El tope existe porque tres columnas es lo que cabe legible en pantalla.
    ;['1', '2', '3'].forEach((id) => cmp.alternar(id))
    check(`admite hasta ${MAXIMO}`, cmp.cuantos === MAXIMO, `${cmp.cuantos}`)

    check('  el cuarto se rechaza y se avisa',
      cmp.alternar('9') === 'lleno' &&
        cmp.cuantos === MAXIMO &&
        cmp.avisoTope === true)
    check('  pero los que ya están se pueden quitar',
      cmp.bloqueado('9') === true && cmp.bloqueado('1') === false)

    cmp.quitar('1')
    check('  al hacer hueco el aviso desaparece', cmp.avisoTope === false)

    // Un producto que desaparezca del catálogo no puede romper la vista.
    cmp.ids = ['2', 'no-existe']
    check('un id que ya no está en el catálogo se descarta',
      cmp.cuantos === 1 && cmp.productos[0].id === '2')

    // ——— la vista ———
    cmp.vaciar()
    const vacia = await render('/comparar', (st) => st.cargarProductos())
    check('sin selección la vista explica cómo empezar',
      vacia.includes('Todavía no has elegido nada'))

    const conDos = await render('/comparar', async (st) => {
      await st.cargarProductos()
      const c = useCompararStore()
      c.vaciar()
      c.alternar('1')
      c.alternar('2')
    })

    check('con dos prendas se ven las dos columnas',
      conDos.includes('Polera b') && conDos.includes('Jeans slim'))
    check('  con las mismas filas en el mismo orden',
      (conDos.match(/Precio hoy/g) ?? []).length === 2 &&
        (conDos.match(/Mínimo registrado/g) ?? []).length === 2)
    check('  y la más barata marcada', conDos.includes('columna--mejor'))
    check('  cada una con su enlace a la tienda y a la ficha',
      conDos.includes('Ver ficha completa') && conDos.includes('Ver en'))

    // ——— la barra ———
    const conBarra = await render('/comparador', async (st) => {
      await st.cargarProductos()
      const c = useCompararStore()
      c.vaciar()
      c.alternar('1')
    })
    check('la barra aparece con algo seleccionado',
      conBarra.includes('aria-label="Comparación"'))

    // En la propia comparación la barra sobra: ya la estás viendo.
    const enComparar = await render('/comparar', async (st) => {
      await st.cargarProductos()
      const c = useCompararStore()
      c.vaciar()
      c.alternar('1')
    })
    check('  y NO aparece dentro de la propia comparación',
      !enComparar.includes('aria-label="Comparación"'))

    // El botón se queda pulsable al llegar al tope: deshabilitarlo lo saca del
    // teclado y del lector, justo a quien más falta le hace la explicación.
    const { default: Boton } = await load(
      '/src/modules/comparar/components/BotonComparar.vue',
    )
    const { renderToString: pintar } = await import('vue/server-renderer')
    const { createSSRApp: crear } = await import('vue')

    const c2 = useCompararStore()
    c2.vaciar()
    ;['1', '2', '3'].forEach((id) => c2.alternar(id))

    const topeHtml = await pintar(crear(Boton, { productoId: '9' }))
    // Con límite de palabra: `aria-disabled="true"` CONTIENE la subcadena
    // `disabled="`, así que buscarla sin más daba siempre negativo y la
    // comprobación fallaba con el código correcto.
    check('el botón al tope sigue siendo pulsable, no deshabilitado',
      topeHtml.includes('aria-disabled="true"') &&
        !/\s(disabled)(=|\s|>)/.test(topeHtml))
    c2.vaciar()
  }

  console.log('\n=== seguridad de rutas ===')
  {
    const { rutaInternaSegura, idProductoValido, textoDeUrl } = await load(
      '/src/shared/utils/rutas.js',
    )

    for (const malo of [
      'https://sitio-falso.cl',
      '//sitio-falso.cl',
      'javascript:alert(1)',
      'data:text/html,<script>',
      '/\\sitio-falso.cl',
      'http:/\\/\\sitio-falso.cl',
      '',
      '   ',
      null,
      undefined,
      123,
      '/'.padEnd(600, 'a'),
    ]) {
      check(`  rechaza ${JSON.stringify(malo)?.slice(0, 34) ?? malo}`,
        rutaInternaSegura(malo) === null)
    }

    check('  acepta una ruta interna', rutaInternaSegura('/comparador?q=polera') === '/comparador?q=polera')
    check('  acepta una ruta con ancla', rutaInternaSegura('/producto/2#precio') === '/producto/2#precio')

    check('id de producto: sólo dígitos',
      idProductoValido('12') && !idProductoValido('../etc/passwd') &&
      !idProductoValido('1;DROP') && !idProductoValido(''))

    check('el texto de la URL se recorta a 120',
      textoDeUrl('x'.repeat(500)).length === 120)
    check('  y descarta caracteres de control',
      textoDeUrl('pol\u0000er\u001fa') === 'polera')

    // La ruta declara la forma del id: una basura ni llega a la vista.
    const basura = await render('/producto/no-soy-un-id', () => {})
    check('  /producto/<basura> cae en el 404', basura.includes('404'))
    const negativo = await render('/producto/-1', () => {})
    check('  /producto/-1 también', negativo.includes('404'))
  }

  const s404 = await render('/ruta-que-no-existe', () => {})
  check('una ruta desconocida cae en el 404', s404.includes('404'))

} catch (e) {
  console.log('\nEXCEPCIÓN:', e.message)
  fallos++
} finally {
  await vite.close()
}

// ——— conexión real con el Product Service (sólo si está levantado) ———
const API = process.env.PRODUCT_SERVICE_URL ?? 'http://localhost:8081'
const VERSION = process.env.VITE_API_VERSION ?? '0.3.0'

async function pedir(ruta, cabeceras = {}) {
  const ctrl = new AbortController()
  const t = setTimeout(() => ctrl.abort(), 4000)

  try {
    const r = await fetch(`${API}${ruta}`, {
      headers: { Accept: 'application/json', ...cabeceras },
      signal: ctrl.signal,
    })

    return { estado: r.status, cuerpo: await r.text() }
  } finally {
    clearTimeout(t)
  }
}

console.log('\n=== conexión con el Product Service ===')
try {
  const salud = await pedir('/health')
  check(`el servicio responde en ${API}`, salud.estado === 200, `/health ${salud.estado}`)

  const sinVersion = await pedir('/productos')
  check(
    'sin cabecera de versión el servicio devuelve 400',
    sinVersion.estado === 400 && sinVersion.cuerpo.includes('More than 1 route matched'),
    `HTTP ${sinVersion.estado}`,
  )

  const conVersion = await pedir('/productos', { 'X-API-VERSION': VERSION })
  check(
    `con X-API-VERSION: ${VERSION} responde 200`,
    conVersion.estado === 200,
    `HTTP ${conVersion.estado}`,
  )

  const filas = JSON.parse(conVersion.cuerpo)
  check('la respuesta es un array', Array.isArray(filas), `${filas.length} filas`)

  const cats = JSON.parse((await pedir('/catalogos', { 'X-API-VERSION': VERSION })).cuerpo)
  const { adaptarProductos } = await import('../src/modules/comparador/services/producto.adapter.js')
  const adaptados = adaptarProductos(filas, cats)

  check(
    'el adaptador digiere la respuesta real',
    Array.isArray(adaptados) && adaptados.length === filas.length,
    `${filas.length} filas → ${adaptados.length} productos`,
  )

  if (adaptados.length > 0) {
    check(
      '  resuelve el nombre de la categoría desde /catalogos',
      adaptados.every((p) => p.categoria !== ''),
      [...new Set(adaptados.map((p) => p.categoria))].join(', '),
    )
    check(
      '  cada producto trae un precio numérico',
      adaptados.every((p) => Number.isFinite(p.precios[0].precio)),
    )

    const { precioMasBajo } = await import('../src/shared/utils/precios.js')
    check(
      '  el cálculo de precio funciona sobre datos reales',
      adaptados.every((p) => precioMasBajo(p) !== null || p.precios[0].stock === false),
    )

    const fuentes = new Set(adaptados.flatMap((p) => p.precios.map((o) => o.tienda)))
    if (fuentes.size < 2) {
      nota(
        `sólo hay ${fuentes.size} fuente de precio: la API no guarda precios por ` +
          'tienda, así que hoy la app puede listar y buscar, pero no comparar.',
      )
    }
  }

  const cors = await fetch(`${API}/productos`, {
    headers: { 'X-API-VERSION': VERSION, Origin: 'http://localhost:5173' },
  })
  const permite = cors.headers.get('access-control-allow-origin')

  if (permite) {
    check('CORS configurado en el servicio', true, permite)
  } else {
    nota(
      'el servicio no manda Access-Control-Allow-Origin: en desarrollo se usa ' +
        'el proxy de Vite (/api). Para producción hay que configurar CORS en ' +
        'Micronaut o servir front y API bajo el mismo dominio.',
    )
  }

  if (filas.length === 0) {
    nota('la base de datos del Product Service está vacía (0 productos).')
  }
} catch (e) {
  console.log(`  --  servicio no alcanzable en ${API} (${e.message}); se omite esta sección`)
}

// ——— la CAPA DE SERVICIOS contra el backend LOCAL ———
//
// Usa .env.humo (localhost), no .env.live: ese último es la configuración de
// despliegue del proyecto y apunta a la API de producción, que puede no existir
// todavía. Mezclarlos hacía que esta prueba fallara por algo que no es un fallo.
//
// Esto es distinto del bloque anterior: allí se hacía fetch a pelo, aquí se
// llaman las funciones que usa la aplicación de verdad. La diferencia importa —
// el fetch en crudo daba 200 mientras la ficha de producto devolvía 404, porque
// el servicio pedía el detalle con una versión de la API que no lo implementa.
console.log('\n=== capa de servicios contra el backend real ===')

// Alcanzabilidad y aserciones van en bloques SEPARADOS a propósito. Con un
// try/catch alrededor de todo, un fallo de una aserción se reportaba como
// "backend no alcanzable" y la comprobación no contaba: verde falso.
let backendVivo = false

try {
  const r = await fetch(`${API}/health`, { signal: AbortSignal.timeout(3000) })
  backendVivo = r.ok
} catch {
  backendVivo = false
}

if (!backendVivo) {
  console.log(`  --  backend no alcanzable en ${API}; se omite esta sección`)
} else {
  const vivo = await createServer({
    mode: 'humo',
    server: { middlewareMode: true },
    appType: 'custom',
    logLevel: 'error',
  })

  // Envuelve una llamada para que un rechazo se cuente como fallo con su
  // mensaje, en vez de abortar el script entero.
  const intentar = async (fn) => {
    try {
      return { ok: true, valor: await fn() }
    } catch (e) {
      return { ok: false, error: e.message }
    }
  }

  try {
    const svc = await vivo.ssrLoadModule(
      '/src/modules/comparador/services/comparador.service.js',
    )

    const lista = await intentar(() => svc.obtenerProductos())
    check('obtenerProductos() trae el catálogo',
      lista.ok && lista.valor.length > 0,
      lista.ok ? `${lista.valor.length} productos` : lista.error)

    const cats = await intentar(() => svc.obtenerCategorias())
    check('obtenerCategorias() trae los catálogos',
      cats.ok && cats.valor.length > 0,
      cats.ok ? cats.valor.map((c) => c.nombre).join(', ') : cats.error)

    if (lista.ok && lista.valor.length > 0) {
      const uno = lista.valor[0]
      const ficha = await intentar(() => svc.obtenerProducto(uno.id))

      check('obtenerProducto(id) trae la ficha',
        ficha.ok && ficha.valor !== null,
        ficha.ok ? (ficha.valor?.nombre ?? 'null') : ficha.error)

      if (ficha.ok && ficha.valor) {
        check('  con el mismo id que se pidió', ficha.valor.id === uno.id,
          `${ficha.valor.id} vs ${uno.id}`)
        check('  con su categoría resuelta', Boolean(ficha.valor.categoria),
          ficha.valor.categoria)
        check('  y con un precio utilizable',
          Number.isFinite(ficha.valor.precios?.[0]?.precio))
      }
    }

    // Un id que no existe tiene que dar un mensaje manejable, no reventar.
    const inexistente = await intentar(() => svc.obtenerProducto('999999'))
    check('un id inexistente da un error con mensaje, no una excepción cruda',
      inexistente.ok || typeof inexistente.error === 'string',
      inexistente.ok ? 'devolvió null' : inexistente.error)
  } finally {
    await vivo.close()
  }
}

// ——— publicidad ———
console.log('\n=== publicidad (AdSense) ===')
{
  const LINEA = 'google.com, pub-2105662597936673, DIRECT, f08c47fec0942fa0'

  const leerAds = (ruta) => {
    try {
      return readFileSync(ruta, 'utf8')
    } catch {
      return null
    }
  }

  const enPublic = leerAds('public/ads.txt')

  check('public/ads.txt existe', enPublic !== null)
  check('  con exactamente la línea que pide Google',
    enPublic?.trim() === LINEA, JSON.stringify(enPublic?.trim()?.slice(0, 40)))
  check('  sin BOM al principio', !enPublic?.startsWith('\ufeff'))
  check('  una sola línea de contenido',
    enPublic?.trim().split('\n').length === 1)

  // Lo que de verdad se sirve es la copia del build, no la de public/.
  const enDist = leerAds('dist/ads.txt')

  check('llega a dist/ads.txt tras compilar', enDist !== null)
  check('  con el mismo contenido', enDist?.trim() === enPublic?.trim())

  // El id de ads.txt y el de la configuración tienen que ser el mismo número.
  // Un desajuste aquí es silencioso: los anuncios no se sirven y nada avisa.
  const idAds = /pub-(\d+)/.exec(enPublic ?? '')?.[1]
  const idEnv = /VITE_ADSENSE_CLIENT=ca-pub-(\d+)/.exec(
    leerAds('.env.example') ?? '',
  )?.[1]

  check('el id de ads.txt coincide con VITE_ADSENSE_CLIENT',
    Boolean(idAds) && idAds === idEnv, `ads.txt=${idAds} · env=${idEnv}`)

  // robots.txt no puede bloquear el rastreo de ads.txt.
  const robots = leerAds('public/robots.txt') ?? ''
  check('robots.txt no bloquea ads.txt',
    !/Disallow: *\/(ads\.txt|\s*$)/m.test(robots))
}

// ——— disciplina de diseño ———
//
// Los siete puntos de "Practical Tips for Cheating at Design" (Refactoring UI),
// convertidos en comprobaciones. No miden si algo es bonito —eso no se puede
// medir— sino si se respetan las reglas que evitan los errores más comunes.
console.log('\n=== las tarjetas aguantan cualquier contenido ===')
{
  // Los nombres de producto, de marca y de tienda los escriben las tiendas, no
  // nosotros. Este bloque vigila las defensas estructurales que impiden que un
  // texto largo —o uno vacío— deforme la rejilla. Son reglas de CSS, así que se
  // comprueban leyendo las hojas: el render en servidor da HTML, no medidas.
  const baseCss = readFileSync('src/assets/base.css', 'utf8')
  const archivos = readdirSync('src', { recursive: true })
    .filter((f) => typeof f === 'string' && (f.endsWith('.vue') || f.endsWith('.css')))
    .map((f) => ['src/' + f, readFileSync('src/' + f, 'utf8')])

  // ——— 1. las columnas de las rejillas ———
  //
  // `1fr` es en realidad `minmax(auto, 1fr)`: el mínimo es el contenido, así
  // que UNA palabra larga ensancha la columna y descuadra la fila entera.
  const rejillasMalas = []
  for (const [ruta, texto] of archivos) {
    for (const m of texto.matchAll(/grid-template-columns:\s*([^;]+)/g)) {
      if (/\d*\.?\d+fr/.test(m[1]) && !m[1].includes('minmax(0')) {
        rejillasMalas.push(`${ruta.replace('src/', '')}: ${m[1].trim()}`)
      }
    }
  }
  check('ninguna rejilla deja que el contenido ensanche su columna',
    rejillasMalas.length === 0, rejillasMalas.slice(0, 3).join(' | '))

  // ——— 2. la red de seguridad del texto ———
  check('ninguna palabra puede salirse de su caja',
    /body \{[^}]*overflow-wrap: break-word/.test(baseCss))

  // ——— 3. la columna del ticket ———
  //
  // Dos trampas que ya mordieron una vez, las dos silenciosas.
  const reglaColumna = /\.ticket\.ticket--columna \{([^}]*)\}/.exec(baseCss)?.[1]
  check('la tarjeta en columna gana a la tarjeta-enlace',
    reglaColumna !== undefined,
    'hace falta la doble clase .ticket.ticket--columna: si no, el '
      + 'display:block de .ticket--enlace la anula por ir después')
  check('  y no se da el alto con un porcentaje',
    reglaColumna !== undefined && !/height:\s*100%/.test(reglaColumna),
    'un alto en % anula el align-self:stretch de la rejilla')
  check('  el cuerpo del ticket crece para llenarla',
    /\.ticket--columna > \.ticket__contenido \{[^}]*flex: 1/.test(baseCss))

  // ——— 4. las utilidades de recorte reservan el alto ———
  //
  // Recortar sin reservar deja las tarjetas de una fila con alturas distintas,
  // que es la mitad del problema que se venía a resolver.
  for (const n of [1, 2, 3]) {
    // Las propiedades vienen de DOS reglas: una agrupada con lo común y otra
    // propia con el número de líneas. Hay que sumar las dos, y por eso no vale
    // buscar `.recorte-N {`: ese patrón también engancha el selector agrupado.
    const declaraciones = [...baseCss.matchAll(/([^{}]+)\{([^}]*)\}/g)]
      .filter(([, selector]) =>
        selector.split(',').some((sel) => sel.trim() === `.recorte-${n}`))
      .map(([, , cuerpo]) => cuerpo)
      .join(' ')

    check(`  .recorte-${n} recorta Y reserva el alto`,
      /line-clamp/.test(declaraciones) && /min-height/.test(declaraciones),
      declaraciones === '' ? 'no se encontró la regla' : '')
  }

  // ——— 5. la tarjeta de producto, que es la que más se repite ———
  const tarjeta = readFileSync(
    'src/modules/comparador/components/ProductoCard.vue', 'utf8')

  check('la tarjeta de producto se estira a la celda', /\bcolumna\b/.test(tarjeta))
  check('  el nombre se recorta a dos líneas', /tarjeta__nombre recorte-2/.test(tarjeta))
  check('  y deja el nombre entero en el title',
    /:title="producto\.nombre"/.test(tarjeta))
  check('  la marca y la categoría no desbordan',
    /tarjeta__marca truncar/.test(tarjeta))
  check('  el nombre de la tienda cede antes que el precio',
    /tarjeta__tienda truncar/.test(tarjeta) &&
      /\.tarjeta__monto \{[^}]*flex: none/.test(tarjeta))
  check('  el pie queda pegado abajo, alineado con el de al lado',
    /\.tarjeta__pie \{[^}]*margin-top: auto/.test(tarjeta))

  // La silueta tiene que medir lo mismo que la tarjeta real, o al llegar los
  // datos la rejilla pega un salto.
  check('  la silueta se estira igual que la tarjeta',
    /\bcolumna\b/.test(readFileSync(
      'src/modules/comparador/components/ProductoCardSkeleton.vue', 'utf8')))

  // ——— 6. las pastillas del filtro ———
  const panel = readFileSync(
    'src/modules/comparador/components/PanelFiltros.vue', 'utf8')
  check('las pastillas del filtro no se salen del panel',
    /\.pildora \{[^}]*max-width: 100%/.test(panel))
  check('  y su cuenta no se recorta nunca',
    /\.pildora__cuenta \{[^}]*flex: none/.test(panel))
  check('  el nombre de cada pastilla sí',
    (panel.match(/<span class="truncar">/g) ?? []).length >= 4,
    `${(panel.match(/<span class="truncar">/g) ?? []).length} de 4`)

  // ——— 7. el banner del carrusel ———
  //
  // Tiene alto fijo y `overflow: hidden`: un título de cuatro líneas empujaba
  // el botón «Ver ficha» fuera de la tarjeta y desaparecía sin dejar rastro.
  const banner = readFileSync(
    'src/modules/comparador/components/RecienteBanner.vue', 'utf8')
  check('el banner de novedades no se traga su propio botón',
    /banner__titulo recorte-2/.test(banner) && /banner__sub truncar/.test(banner))

  // ——— 8. la columna de precios de «Elige tu tienda» ———
  //
  // Cada oferta es su PROPIA rejilla, así que con columnas `auto` cada fila las
  // medía por su cuenta: un botón «Ver en Mango» y otro «Ver en Ripley» daban
  // anchos distintos y los precios bailaban de fila en fila. En un comparador
  // esa columna es justo lo que la gente recorre con la vista.
  const elige = readFileSync(
    'src/modules/comparador/components/EligeTuTienda.vue', 'utf8')
  const anchaEnFila = /@media \(min-width: 560px\) \{\s*\.oferta \{[\s\S]*?grid-template-columns:([^;]+);/
    .exec(elige)?.[1] ?? ''
  check('los precios de las ofertas caen todos en la misma vertical',
    anchaEnFila.includes('minmax') && !/\bauto\s+auto\b/.test(anchaEnFila),
    anchaEnFila.trim())

  // ——— 9. .truncar sólo funciona si puede encoger ———
  check('.truncar lleva el min-width que lo hace funcionar dentro de un flex',
    /\.truncar \{[^}]*min-width: 0/.test(baseCss))
}

console.log('\n=== disciplina de diseño ===')
{
  const css = readFileSync('src/assets/base.css', 'utf8')
  const fuentes = readdirSync('src', { recursive: true })
    .filter((f) => typeof f === 'string' && (f.endsWith('.vue') || f.endsWith('.css')))
    .map((f) => readFileSync(`src/${f}`, 'utf8'))
    .join('\n')

  // 1 · jerarquía por color y peso, no por tamaño
  const pesos = [...fuentes.matchAll(/font-weight: *(\d{3})/g)].map((m) => Number(m[1]))

  check('1 · ningún peso por debajo de 400',
    pesos.every((p) => p >= 400),
    `pesos: ${[...new Set(pesos)].sort().join(', ')}`)
  check('  y como mucho dos pesos de énfasis',
    new Set(pesos.filter((p) => p >= 600)).size <= 2)

  // El escalón de 11px es para etiquetas: si la mayoría del texto vive ahí, se
  // está usando el tamaño para lo que debería hacer el color.
  const dosXs = (fuentes.match(/var\(--cep-fs-2xs\)/g) ?? []).length
  const total = (fuentes.match(/var\(--cep-fs-[a-z0-9]+\)/g) ?? []).length

  check('  el tamaño más pequeño es minoritario',
    dosXs / total < 0.2, `${dosXs} de ${total} (${Math.round((dosXs / total) * 100)}%)`)

  // 2 · nada de gris sobre fondos de color
  const grisSobreColor = [...fuentes.matchAll(/\.[a-z0-9_-]+\s*\{[^}]*\}/g)]
    .map((m) => m[0])
    .filter(
      (regla) =>
        /background: var\(--cep-(ink|accent|exito|alerta)\)/.test(regla) &&
        /color: var\(--cep-muted\)/.test(regla),
    )

  check('2 · no hay gris sobre fondos de color', grisSobreColor.length === 0,
    grisSobreColor.length ? grisSobreColor[0].slice(0, 60) : 'ninguno')

  // 3 · las sombras van desplazadas hacia abajo, no sólo difuminadas
  const sombras = [...css.matchAll(/--cep-shadow-\d: *0 (\d+)px (\d+)px/g)]

  check('3 · todas las sombras tienen desplazamiento vertical',
    sombras.length > 0 && sombras.every((m) => Number(m[1]) > 0),
    sombras.map((m) => `${m[1]}px`).join(' '))

  // 4 · pocos bordes: la tarjeta no puede separarse de tres formas a la vez
  const ticket = /\.ticket \{[^}]*\}/.exec(css)?.[0] ?? ''

  check('4 · la tarjeta no usa borde, sombra y fondo a la vez',
    ticket.includes('var(--cep-borde-tarjeta)'),
    'el borde queda sólo donde el fondo no basta (tema oscuro)')

  // 5 · los iconos no se amplían: se meten en una forma con fondo
  check('5 · ningún icono ampliado a un tamaño absurdo',
    !/\.[a-z_-]*icono[a-z_-]*\s*\{[^}]*font-size: [4-9]\dpx/.test(fuentes))
  check('  el aviso de error encierra su icono en una forma',
    fuentes.includes('fallo__marca') && /\.fallo__marca\s*\{[^}]*border-radius: 50%/.test(fuentes))

  // 6 · bordes de acento para dar color
  const acentos = (fuentes.match(/border-(left|top)(-color)?: *[23]px solid var\(--cep-(accent|alerta|exito|rate)\)/g) ?? []).length

  check('6 · hay bordes de acento repartidos', acentos >= 4, `${acentos} usos`)
  check('  incluida la franja superior de la página',
    /\.barra \{[^}]*border-top: 3px solid var\(--cep-accent\)/.test(fuentes))
  check('  y el elemento activo de la navegación',
    /router-link-active \{[^}]*border-bottom-color: var\(--cep-accent\)/.test(fuentes))

  // 7 · jerarquía de botones: no todos llevan fondo
  const variantes = ['primario', 'secundario', 'texto']

  check('7 · hay tres niveles de botón',
    variantes.every((v) => fuentes.includes(`btn--${v}`)))
  check('  el terciario no lleva fondo ni borde',
    /\.btn--texto \{[^}]*background: none[^}]*border-color: transparent/s.test(fuentes))
}

// ——— sitemap y preparación para publicar ———
console.log('\n=== listo para publicar ===')
{
  const leer = (ruta) => {
    try {
      return readFileSync(ruta, 'utf8')
    } catch {
      return null
    }
  }

  const sitemap = leer('dist/sitemap.xml')
  const robots = leer('dist/robots.txt') ?? ''

  check('el sitemap llega al build', sitemap !== null)

  const urls = [...(sitemap ?? '').matchAll(/<loc>([^<]+)<\/loc>/g)].map((m) => m[1])

  check('  con las páginas públicas', urls.length >= 5, `${urls.length} URLs`)

  // El dominio del sitemap y el que anuncia robots.txt tienen que ser el
  // mismo: un buscador que encuentra un sitemap de otro dominio lo descarta.
  const dominioRobots = /Sitemap: *(https?:\/\/[^/\s]+)/.exec(robots)?.[1]

  check('  el dominio coincide con el que anuncia robots.txt',
    urls.every((u) => u.startsWith(dominioRobots ?? '\u0000')),
    dominioRobots ?? 'robots.txt no anuncia sitemap')

  // Lo que robots.txt bloquea no puede estar en el sitemap: mandar a un
  // buscador a una URL que le prohibimos rastrear es contradictorio.
  const bloqueadas = [...robots.matchAll(/Disallow: *(\S+)/g)].map((m) => m[1])

  check('  no incluye ninguna ruta bloqueada en robots.txt',
    !urls.some((u) => bloqueadas.some((b) => new URL(u).pathname.startsWith(b))),
    bloqueadas.join(' '))

  // Un patrón de ruta no es una URL.
  check('  no incluye patrones de ruta con :parámetro',
    !urls.some((u) => u.includes(':') && !u.startsWith('http')))

  // Cada URL del sitemap tiene que existir de verdad en el router. Es lo que
  // caza una página renombrada cuyo sitemap quedó apuntando a la anterior.
  // Se recorren TODOS los módulos, no una lista escrita a mano: si mañana hay
  // un módulo nuevo, sus rutas entran solas en la comprobación.
  const { readdirSync: leerDir } = await import('node:fs')

  const rutasDelRouter = new Set(
    leerDir('src/modules')
      .map((modulo) => leer(`src/modules/${modulo}/routes.js`) ?? '')
      .join('')
      .match(/path: '([^']+)'/g)
      ?.map((m) => m.slice(7, -1)) ?? [],
  )

  const rutasReales = rutasDelRouter

  check('  todas sus URLs son rutas reales del router',
    urls.every((u) => rutasReales.has(new URL(u).pathname)),
    urls.map((u) => new URL(u).pathname).join(' '))

  // El script de despliegue tiene que existir y ser ejecutable.
  check('existe el script de despliegue', leer('scripts/desplegar.sh') !== null)
  check('  y sube ads.txt con tipo de contenido explícito',
    (leer('scripts/desplegar.sh') ?? '').includes('text/plain'))
}

// ——— el bundle de producción no lleva texto de desarrollador ———
console.log('\n=== build de producción ===')
{
  const dist = 'dist/assets'
  let js = ''

  try {
    const { readdirSync, readFileSync: leer } = await import('node:fs')
    js = readdirSync(dist)
      .filter((f) => f.endsWith('.js') || f.endsWith('.css'))
      .map((f) => leer(`${dist}/${f}`, 'utf8'))
      .join('\n')
  } catch {
    js = ''
  }

  if (js === '') {
    nota('no hay dist/ compilado; ejecuta `npm run build` antes de humo')
  } else {
    for (const frase of [
      'Datos de ejemplo',
      'VITE_API_BASE_URL',
      'pendiente de revisión legal',
      'En cuanto la API',
      'localhost:8081',
      '970 × 90',
      'banner de portada',
      'client_secret',
      'GOCSPX-',
      'TODO',
      'FIXME',
    ]) {
      check(`el bundle no contiene "${frase}"`, !js.includes(frase))
    }

    check('no quedan console.log en el bundle', !/console\.log\(/.test(js))
  }
}

console.log(`\ncomprobaciones fallidas: ${fallos}`)
process.exit(fallos ? 1 : 0)
