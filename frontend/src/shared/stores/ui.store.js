import { computed, ref, watchEffect } from 'vue'
import { defineStore } from 'pinia'

// Preferencias de interfaz. No pertenecen a ningún módulo de negocio, así que
// viven en shared/.
//
// Tres valores posibles y no dos: "sistema" es el que respeta la configuración
// del sistema operativo, y es el que debe venir por defecto. Un usuario que
// tiene el móvil en oscuro no espera que una web le salga en blanco.

const CLAVE = 'cep:tema'
const TEMAS = ['sistema', 'claro', 'oscuro']

function leerGuardado() {
  try {
    const valor = localStorage.getItem(CLAVE)

    return TEMAS.includes(valor) ? valor : 'sistema'
  } catch {
    // Modo privado o cookies bloqueadas: no es motivo para romper la app.
    return 'sistema'
  }
}

export const useUiStore = defineStore('ui', () => {
  const tema = ref(leerGuardado())
  const prefiereOscuroElSistema = ref(false)

  // El sistema puede cambiar de tema mientras la página está abierta.
  if (typeof window !== 'undefined' && window.matchMedia) {
    const consulta = window.matchMedia('(prefers-color-scheme: dark)')

    prefiereOscuroElSistema.value = consulta.matches
    consulta.addEventListener('change', (e) => {
      prefiereOscuroElSistema.value = e.matches
    })
  }

  const esOscuro = computed(() =>
    tema.value === 'sistema' ? prefiereOscuroElSistema.value : tema.value === 'oscuro',
  )

  function aplicar() {
    // El valor se lee ANTES del guard, y el orden importa: si se sale por el
    // `return` sin haber leído `esOscuro`, el watchEffect no registra ninguna
    // dependencia y no se vuelve a ejecutar jamás. Es lo que pasaba al crear
    // el store en un entorno sin document (render en servidor, pruebas).
    const oscuro = esOscuro.value

    if (typeof document === 'undefined') return

    document.documentElement.dataset.tema = oscuro ? 'oscuro' : 'claro'
  }

  function elegirTema(valor) {
    if (!TEMAS.includes(valor)) return

    tema.value = valor

    try {
      localStorage.setItem(CLAVE, valor)
    } catch {
      // Sin persistencia el tema dura la sesión; peor sería caerse.
    }
  }

  // El atributo se escribe aquí y no en un componente: si dependiera de que
  // la cabecera esté montada, una vista sin cabecera saldría sin tema.
  watchEffect(aplicar)

  return { tema, esOscuro, elegirTema, TEMAS }
})
