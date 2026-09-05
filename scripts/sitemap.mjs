// Genera public/sitemap.xml a partir del mapa de rutas real.
//
// Se deriva de las rutas y no se escribe a mano para que no se desincronice:
// añadir una página al proyecto la mete en el sitemap sola, y quitarla la saca.
// Se ejecuta antes de compilar (ver el script "build" de package.json).

import { writeFileSync } from 'node:fs'

import { createServer } from 'vite'

const SITIO = (process.env.VITE_SITE_URL ?? 'https://cacha-el-precio.com').replace(
  /\/+$/,
  '',
)

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

  writeFileSync(
    'public/sitemap.xml',
    `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls}
</urlset>
`,
  )

  console.log(`sitemap.xml · ${paginas.length} páginas · ${SITIO}`)
} finally {
  await vite.close()
}
