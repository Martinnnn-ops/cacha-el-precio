import { createApp } from 'vue'

import App from './App.vue'
import pinia from '@/core/store'
import router from '@/core/router'
import '@/assets/base.css'

createApp(App).use(pinia).use(router).mount('#app')
