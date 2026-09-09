import { fileURLToPath, URL } from 'node:url'

import vue from '@vitejs/plugin-vue'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  // Gateway de cacha-el-precio (ASP.NET Core). En desarrollo el navegador
  // pide /api y Vite reenvía esa misma ruta al gateway local. Mantener el
  // prefijo es importante: el contrato actual publica /api/products.
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
        },
      },
    },
  }
})
