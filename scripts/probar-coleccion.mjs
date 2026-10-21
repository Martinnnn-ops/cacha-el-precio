import assert from 'node:assert/strict'
import { createServer } from 'vite'
import { createPinia, setActivePinia } from 'pinia'

const vite = await createServer({ mode: 'test', appType: 'custom',
  server: { middlewareMode: true }, logLevel: 'error',
  define: { 'import.meta.env.VITE_API_BASE_URL': JSON.stringify('/api') } })
try {
  const load = p => vite.ssrLoadModule(p)
  setActivePinia(createPinia())
  const { default: http } = await load('/src/core/api/http.js')
  const { useCuentaStore } = await load('/src/modules/cuenta/store/cuenta.store.js')
  const { useColeccionStore } = await load('/src/modules/cuenta/store/coleccion.store.js')
  const cuenta = useCuentaStore()
  const coleccion = useColeccionStore()
  cuenta.usuario = { id: 'a', nombre: 'A' }
  let peticiones = 0
  let resolver
  http.get = () => { peticiones++; return new Promise(r => { resolver = r }) }
  const carga = coleccion.cargar()
  const simultanea = coleccion.cargar()
  assert.equal(peticiones, 1, 'Las tarjetas deben compartir una sola petición')
  cuenta.usuario = { id: 'b', nombre: 'B' }
  resolver({ productos: [123], outfits: [] })
  await Promise.all([carga, simultanea])
  assert.deepEqual(coleccion.deseados, [], 'Una respuesta atrasada no debe aparecer en otra cuenta')
  assert.equal(coleccion.cargado, false)
  http.get = async () => ({ productos: [456], outfits: [] })
  await coleccion.cargar()
  assert.deepEqual(coleccion.deseados, ['456'])
  http.delete = async () => { throw new Error('Sin conexión') }
  assert.equal(await coleccion.alternar('456'), false)
  assert.deepEqual(coleccion.deseados, ['456'], 'Un fallo no debe fingir que quitó el favorito')
  cuenta.cerrarSesion()
  assert.deepEqual(coleccion.deseados, [])
  assert.deepEqual(coleccion.outfits, [])
  console.log('OK: una petición compartida, aislamiento de sesión, errores y cierre de sesión')

  const { useComparadorStore } = await load('/src/modules/comparador/store/comparador.store.js')
  const { useOutfitStore } = await load('/src/modules/outfits/store/outfit.store.js')
  const catalogo = useComparadorStore()
  catalogo.productos = [
    ['1', 'Poleras', 'Base', 'Hombre'], ['2', 'Polerones', 'Abrigo', 'Hombre'],
    ['3', 'Chaquetas', 'Abrigo', 'Hombre'], ['4', 'Pantalones', 'Inferior', 'Hombre'],
    ['5', 'Zapatillas', 'Calzado', 'Unisex'], ['6', 'Vestidos', 'Entero', 'Mujer'],
  ].map(([id, categoria, capa, genero]) => ({ id, categoria, capa, genero,
    nombre: categoria, zona: categoria === 'Vestidos' ? 'Cuerpo completo' : '',
    precios: [{ stock: true, precio: 10000, tallas: ['M'], tienda: 'test' }] }))
  const outfit = useOutfitStore()
  outfit.limpiar()
  outfit.completarConMasBarato()
  assert.equal(outfit.completo, true)
  assert.equal(outfit.piezas.length, 3, 'Las capas opcionales no se deben cobrar automáticamente')
  outfit.ponerPrenda('torso-intermedia', '2')
  outfit.ponerPrenda('torso-abrigo', '3')
  assert.equal(outfit.piezas.length, 5, 'Polera + polerón + chaqueta deben coexistir')
  outfit.genero = 'Hombre'
  assert(!outfit.opcionesPara('torso-base').some(p => p.id === '6'))
  outfit.ponerOutfit({ 'torso-base': '6', piernas: '4', calzado: '3' })
  assert.equal(outfit.seleccion.piernas, null, 'Vestido ocupa las piernas')
  assert.equal(outfit.seleccion.calzado, null, 'No restaurar una chaqueta en calzado')
  outfit.ponerPrenda('piernas', '4')
  assert.equal(outfit.seleccion.piernas, null, 'Respetar las ranuras ocupadas por un vestido')
  console.log('OK: capas opcionales, polera/polerón/chaqueta, género y restauración segura')
} finally { await vite.close() }
