import { fileURLToPath, URL } from 'node:url'

import vue from '@vitejs/plugin-vue'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  // Product Service (Micronaut). En desarrollo se habla con él a través del
  // proxy de Vite y NO directamente: el servicio no manda cabeceras CORS, así
  // que una petición del navegador a localhost:8081 la bloquea el navegador
  // antes de salir. Con el proxy todo sale del mismo origen (5173) y Vite
  // reenvía por detrás, que es servidor a servidor y no pasa por CORS.
  //
  // En producción esto no aplica: o el front y la API van tras el mismo
  // dominio (mismo origen), o hay que configurar CORS de verdad en Micronaut.
  const destino = env.PRODUCT_SERVICE_URL ?? 'http://localhost:8081'

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
          rewrite: (ruta) => ruta.replace(/^\/api/, ''),
        },
      },
    },
  }
})
