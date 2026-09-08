// Genera public/sitemap.xml a partir del mapa de rutas real.
//
// Se deriva de las rutas y no se escribe a mano para que no se desincronice:
// añadir una página al proyecto la mete en el sitemap sola, y quitarla la saca.
// Se ejecuta antes de compilar (ver el script "build" de package.json).

import { writeFileSync } from 'node:fs'

import { createServer, loadEnv } from 'vite'

const SITIO = (process.env.VITE_SITE_URL ?? 'https://cacha-el-precio.com').replace(
  /\/+$/,
  '',
)

// Misma fuente de configuración que el build: Vite carga .env.production en
// "npm run build" y aquí hace falta saber contra qué API consultar los
// productos sin duplicar el valor a mano (en .env.production está VITE_API_BASE_URL).
const env = loadEnv('production', process.cwd(), '')

const API = (env.VITE_API_BASE_URL ?? 'https://api.cacha-el-precio.com/api').replace(
  /\/+$/,
  '',
)

// Versión del contrato ASP.NET Core, enviada mediante la cabecera `Version`.
const VERSION = env.VITE_API_VERSION ?? '1.0'

// Rutas que NO deben indexarse. Coincide con lo que bloquea robots.txt: las
// pantallas de sesión no aportan nada en un buscador y sólo generan
// resultados que no llevan a ninguna parte.
const EXCLUIDAS = new Set(['login', 'registro', 'retorno-google', 'no-encontrado'])

// Cada cuánto merece la pena que un buscador vuelva a mirar.
const FRECUENCIA = {
  inicio: ['daily', '1.0'],
  comparador: ['daily', '0.9'],
  outfits: ['daily', '0.9'],
  armar: ['weekly', '0.8'],
  comparar: ['weekly', '0.6'],
  preguntas: ['monthly', '0.5'],
  terminos: ['yearly', '0.3'],
  privacidad: ['yearly', '0.3'],
}

const vite = await createServer({
  mode: 'production',
  server: { middlewareMode: true },
  appType: 'custom',
  logLevel: 'error',
})

try {
  const { routes } = await vite.ssrLoadModule('/src/core/router/routes.js')
  const { slugProducto } = await vite.ssrLoadModule('/src/shared/utils/slug.js')

  // Productos contra el backend real. Si el backend no está (build local sin
  // URL de API), el sitemap queda con las páginas estáticas y listo: generar
  // el sitemap no debe tumbar la compilación.
  let productos = []
  try {
    const respuesta = await fetch(`${API}/products`, {
      headers: {
        Accept: 'application/json',
        Version: VERSION,
      },
    })

    if (!respuesta.ok) {
      console.warn(`sitemap · GET /api/products → ${respuesta.status}: sin productos`)
    } else {
      productos = await respuesta.json()
    }
  } catch (error) {
    console.warn(`sitemap · GET /api/products no disponible: ${error.message}`)
  }

  const paginas = routes.filter(
    (ruta) =>
      !EXCLUIDAS.has(ruta.name) &&
      // Fuera las rutas con parámetro: /producto/:id no es una URL, es un
      // patrón. Para incluir los productos haría falta consultar la API, y eso
      // pertenece a un sitemap generado en el servidor, no aquí.
      !ruta.path.includes(':') &&
      // Fuera los redirects: /entrar sólo existe para no romper los enlaces
      // viejos. La URL buena es su destino, y ésa ya va en la lista. Mandar a
      // un buscador a las dos es pedirle que indexe contenido duplicado.
      ruta.redirect === undefined,
  )

  const hoy = new Date().toISOString().slice(0, 10)

  // El detalle se sirve en /producto/<slug>, con el slug derivado del nombre
  // (ver shared/utils/slug.js). El slug que genera este script y el que genera
  // la app en el navegador son idénticos porque usan la misma función.
  const urlsProductos = productos
    .map((producto) =>
      [
        '  <url>',
        `    <loc>${SITIO}/producto/${slugProducto(producto)}</loc>`,
        `    <lastmod>${hoy}</lastmod>`,
        '    <changefreq>daily</changefreq>',
        '    <priority>0.7</priority>',
        '  </url>',
      ].join('\n'),
    )
    .join('\n')

  const urls = paginas
    .map((ruta) => {
      const [frecuencia, prioridad] = FRECUENCIA[ruta.name] ?? ['monthly', '0.5']

      return [
        '  <url>',
        `    <loc>${SITIO}${ruta.path}</loc>`,
        `    <lastmod>${hoy}</lastmod>`,
        `    <changefreq>${frecuencia}</changefreq>`,
        `    <priority>${prioridad}</priority>`,
        '  </url>',
      ].join('\n')
    })
    .join('\n')

  const todas = [urls, urlsProductos].filter(Boolean).join('\n')

  writeFileSync(
    'public/sitemap.xml',
    `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${todas}
</urlset>
`,
  )

  console.log(
    `sitemap.xml · ${paginas.length} páginas · ${productos.length} productos · ${SITIO}`,
  )
} finally {
  await vite.close()
}
