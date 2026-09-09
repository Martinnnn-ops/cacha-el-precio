import { fileURLToPath, URL } from 'node:url'

import vue from '@vitejs/plugin-vue'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  // Gateway de cacha-el-precio (ASP.NET Core). En desarrollo el navegador pide
  // /api/... y Vite lo reenvía al gateway QUITANDO ese prefijo: el contrato
  // del BFF publica /productos, /catalogos y /seguimiento en la raíz.
  //
  // El prefijo existe solo para que el proxy sepa qué interceptar; con una
  // sola regla quedan cubiertas todas las rutas del backend, presentes y
  // futuras. Sin él habría que enumerar una por una y cada ruta nueva se
  // olvidaría, fallando solo en desarrollo.
  //
  // En producción esto no aplica: o el front y la API van tras el mismo
  // dominio (mismo origen), o hay que configurar CORS en el gateway.
  const destino = env.BACKEND_URL ?? 'http://localhost:8080'

  return {
    plugins: [vue()],

    resolve: {
      // "@" apunta a src/. Con una estructura por módulos los imports relativos
      // se vuelven ilegibles enseguida (../../../core/api/http), y además atan
      // el archivo a su ubicación: mover un módulo los rompería todos.
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },

    server: {
      proxy: {
        '/api': {
          target: destino,
          changeOrigin: true,
          // /api/productos → /productos. El gateway no conoce el prefijo.
          rewrite: (ruta) => ruta.replace(/^\/api/, ''),
        },
      },
    },
  }
})
