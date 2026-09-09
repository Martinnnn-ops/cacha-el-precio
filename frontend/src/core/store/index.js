import { createPinia } from 'pinia'

// Instancia única de Pinia. Los stores de cada módulo se registran solos la
// primera vez que se llama a su useXStore(), así que aquí no hay que
// enumerarlos: un módulo nuevo no obliga a tocar este archivo.
const pinia = createPinia()

export default pinia
