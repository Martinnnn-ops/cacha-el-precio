// Rutas del módulo. Se concatenan en core/router/routes.js.
// Las vistas van con import() perezoso: cada una viaja en su propio trozo y no
// se descarga hasta que alguien entra en esa ruta.

import { useComparadorStore } from '@/modules/comparador/store/comparador.store'
import { slugProducto } from '@/shared/utils/slug'

// ¿Ese slug corresponde a algún producto del catálogo?
//
// Vive en el router y no en la vista a propósito: la respuesta decide si la
// página llega a existir, y eso es una decisión de enrutado. Resuelta en la
// vista, la ficha ya se montó —con su layout y su bloque de anuncio— antes de
// descubrir que no había nada que enseñar.
//
// Los import van arriba y no dentro de la función: el store ya viaja en el
// trozo inicial porque lo importa `App.vue`, así que un import() dinámico no
// lo movería a ninguna parte —Vite avisa de eso con INEFFECTIVE_DYNAMIC_IMPORT—
// y solo dejaría el código pareciendo más cuidadoso de lo que es.
async function elSlugExiste(slug) {
  const store = useComparadorStore()

  if (store.productos.length === 0) {
    try {
      await store.cargarProductos()
    } catch {
      // Si el catálogo no se pudo traer, no sabemos si el producto existe.
      // Se deja pasar: la vista tiene su estado de error con reintento, que es
      // mejor que un 404 que afirma algo falso.
      return true
    }
  }

  return store.productos.some((producto) => slugProducto(producto) === slug)
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

    // Un slug que no es de ningún producto se va al 404 antes de pintar nada.
    // Dos motivos, y el segundo cuesta dinero:
    //
    //   · una URL inventada respondiendo como página buena es un «soft 404»,
    //     y Google lo cuenta contra el sitio entero, no contra esa página;
    //   · la ruta del 404 lleva `meta.sinAnuncios` y esta no, así que la ficha
    //     vacía pintaría igual el bloque de cierre del layout. Las políticas
    //     de AdSense prohíben anuncios en páginas sin contenido propio, y
    //     saltárselo puede costar la cuenta completa.
    //
    // `beforeEnter` NO se ejecuta cuando solo cambia el parámetro —al saltar
    // de un producto a otro dentro de la aplicación—, y por eso la vista
    // conserva su propio redirect para ese caso. Aquí se cubre el que importa
    // para lo de arriba: la entrada directa, que es como llegan los enlaces
    // compartidos y los rastreadores.
    beforeEnter: async (to) =>
      (await elSlugExiste(to.params.slug)) || { name: 'no-encontrado' },
  },
]
