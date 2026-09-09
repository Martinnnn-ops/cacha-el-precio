// Rutas del módulo. Se concatenan en core/router/routes.js.
// Las vistas van con import() perezoso: cada una viaja en su propio trozo y no
// se descarga hasta que alguien entra en esa ruta.

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import { slugProducto } from '@/shared/utils/slug'

// ¿Ese slug NO corresponde a ningún producto? Solo se responde cuando se puede
// responder de verdad, es decir con el catálogo ya en memoria.
//
// El guard **no pide el catálogo**. Hacerlo tenía dos efectos malos, los dos
// medidos:
//
//   · bloqueaba el enrutado. Hasta que la petición terminaba no se montaba
//     nada —ni el layout ni el esqueleto de la ficha—, así que quien abría un
//     enlace compartido veía una página en blanco durante toda la descarga.
//     Antes veía el esqueleto de inmediato. Eso afecta a TODAS las URLs
//     válidas, que son la mayoría;
//   · con el backend caído el catálogo llegaba vacío, ningún slug coincidía y
//     **todo enlace válido acababa en el 404**, justo cuando el usuario más
//     necesita el estado de error con reintento que la vista ya tiene.
//
// Con el catálogo cargado —navegación dentro de la aplicación, que es de donde
// salen casi todos los clics— la comprobación es inmediata y no pide nada. En
// una entrada directa en frío se deja pasar y la vista se encarga: enseña su
// esqueleto, carga, y redirige al 404 si de verdad no existe.
function elSlugNoExiste(slug) {
  const store = useComparadorStore()

  // Sin catálogo no hay nada que afirmar. Decir "no existe" sería inventar.
  if (store.productos.length === 0) return false

  return !store.productos.some((producto) => slugProducto(producto) === slug)
}

// Destino del 404 conservando la URL que se pidió. La ruta se llama
// `no-encontrado` pero su path es el comodín `/:pathMatch(.*)*`: hay que
// pasarle el camino troceado o Vue Router compone la raíz.
//
// Recibe segmentos YA DECODIFICADOS. Vue Router codifica cada uno al componer
// la URL, así que pasarle `to.path` —que viene codificado— escapaba el `%` otra
// vez: `/producto/polera%20azul` terminaba enseñándose como
// `/producto/polera%2520azul`, y la idea era justamente que se pudiera leer y
// corregir la dirección que falló.
export function rutaNoEncontrada(...segmentos) {
  return {
    name: 'no-encontrado',
    params: { pathMatch: segmentos },
    replace: true,
  }
}

export default [
  {
    path: '/',
    name: 'inicio',
    component: () => import('@/modules/comparador/views/InicioView.vue'),
    meta: { titulo: 'Inicio' },
  },
  {
    path: '/comparador',
    name: 'comparador',
    component: () => import('@/modules/comparador/views/ComparadorView.vue'),
    meta: { titulo: 'Comparador' },
  },
  {
    // La URL del detalle es el slug: /producto/poleron-ck-institutional-blanco.
    // Se genera del nombre y se recorta para no alargar las URLs.
    //
    // ⚠️ El patrón acepta CUALQUIER texto: a diferencia del `:id(\d{1,12})`
    // que hubo antes, aquí el router ya no rechaza nada por su forma. Eso
    // convierte /producto/<lo-que-sea> en infinitas URLs válidas, así que la
    // comprobación de que el producto existe la hace el guard de abajo.
    path: '/producto/:slug',
    name: 'producto-detalle',
    component: () => import('@/modules/comparador/views/ProductoDetailView.vue'),
    props: true,
    meta: { titulo: 'Detalle del producto', ancho: 'lectura' },

    // Un slug que no es de ningún producto se va al 404 sin llegar a pintar la
    // ficha. Dos motivos, y el segundo cuesta dinero:
    //
    //   · una URL inventada respondiendo como página buena es un «soft 404»,
    //     y Google lo cuenta contra el sitio entero, no contra esa página;
    //   · la ruta del 404 lleva `meta.sinAnuncios` y esta no, así que la ficha
    //     vacía pintaría igual el bloque de cierre del layout. Las políticas
    //     de AdSense prohíben anuncios en páginas sin contenido propio, y
    //     saltárselo puede costar la cuenta completa.
    //
    // Es síncrono a propósito (ver `elSlugNoExiste`): con el catálogo cargado
    // responde al instante, y sin él deja pasar para que la vista lo resuelva
    // con su esqueleto. Tampoco se ejecuta cuando solo cambia el parámetro —al
    // saltar de un producto a otro—, y por eso la vista conserva su redirect.
    //
    // Se le pasa `to.params.slug`, que viene ya decodificado, y no `to.path`.
    beforeEnter: (to) =>
      elSlugNoExiste(to.params.slug)
        ? rutaNoEncontrada('producto', to.params.slug)
        : true,
  },
]
