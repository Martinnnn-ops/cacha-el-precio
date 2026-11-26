import { createApp } from 'vue'

import App from './App.vue'
import pinia from '@/core/store'
import router from '@/core/router'
import '@/assets/base.css'

const app = createApp(App).use(pinia).use(router)

// Se monta cuando el router ya resolvió la primera ruta, no antes. Las vistas
// se cargan aparte (`() => import(...)`), así que montando al tiro el layout se
// pintaba con el <main> vacío: el pie y el anuncio quedaban arriba y bajaban de
// golpe al llegar la vista. En www eso daba un CLS de 0.7 en la portada (lo
// recomendable es menos de 0.1). Esperar cuesta lo que tarda el trozo de la
// vista, que igual había que bajar antes de mostrar algo útil.
router.isReady().then(() => app.mount('#app'))
