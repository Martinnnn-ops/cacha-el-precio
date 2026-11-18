import { computed, ref, watchEffect } from 'vue'
import { defineStore } from 'pinia'

// Preferencias de interfaz. No pertenecen a ningún módulo de negocio, así que
// viven en shared/.
//
// Tres valores posibles y no dos: por defecto va "sistema", así la web sigue la
// configuración del dispositivo —de noche, con el sistema en oscuro, el sitio
// aparece oscuro sin tener que elegirlo— y nadie termina con la pantalla en
// blanco a oscuras. "claro" y "oscuro" fijan uno u otro aunque el sistema diga
// otra cosa, para quien lo prefiera.

const CLAVE = 'cep:tema'
const TEMAS = ['sistema', 'claro', 'oscuro']
const POR_DEFECTO = 'sistema'

function leerGuardado() {
  try {
    const valor = localStorage.getItem(CLAVE)

    return TEMAS.includes(valor) ? valor : POR_DEFECTO
  } catch {
    // Modo privado o cookies bloqueadas: no es motivo para romper la app.
    return POR_DEFECTO
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

    // La barra del navegador en móvil sigue al tema que se ve, no al del
    // sistema. El color se lee del propio token, así no se duplica aquí. Sin
    // un navegador de verdad (render en servidor, pruebas) no hay nada que leer.
    if (
      typeof document.querySelector !== 'function' ||
      typeof getComputedStyle !== 'function'
    ) {
      return
    }

    const barra = document.querySelector('meta[name="theme-color"]')
    const fondo = getComputedStyle(document.documentElement)
      .getPropertyValue('--cep-bg')
      .trim()

    if (barra && fondo) barra.setAttribute('content', fondo)
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
