import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import {
  GOOGLE_CONFIGURADO,
  canjearCodigo,
  destinoGuardado,
  irAGoogle,
  obtenerPerfil,
  verificarEstado,
} from '@/modules/cuenta/services/google.oauth'

// Sesión de la persona que usa la aplicación.
//
// El token vive en sessionStorage y no en localStorage: dura lo que la pestaña.
// Es un compromiso deliberado — obliga a volver a entrar más a menudo, pero un
// token que persiste en el disco del navegador es un token que un XSS puede
// robar y reutilizar mañana. Lo definitivo es una cookie HttpOnly emitida por
// el backend; mientras no exista, esto es lo más contenido que se puede hacer.
const CLAVE_TOKEN = 'cep:token'
const CLAVE_USUARIO = 'cep:usuario'

function leer(clave) {
  try {
    const valor = sessionStorage.getItem(clave)

    return valor ? JSON.parse(valor) : null
  } catch {
    return null
  }
}

function escribir(clave, valor) {
  try {
    if (valor === null) sessionStorage.removeItem(clave)
    else sessionStorage.setItem(clave, JSON.stringify(valor))
  } catch {
    // Sin almacenamiento la sesión dura lo que la página. No es motivo para
    // impedir entrar.
  }
}

export const useCuentaStore = defineStore('cuenta', () => {
  const usuario = ref(leer(CLAVE_USUARIO))
  const entrando = ref(false)
  const error = ref(null)

  const autenticado = computed(() => usuario.value !== null)
  const disponible = computed(() => GOOGLE_CONFIGURADO)

  const iniciales = computed(() => {
    const nombre = usuario.value?.nombre ?? usuario.value?.correo ?? ''

    return nombre.slice(0, 2).toUpperCase() || '?'
  })

  /** Arranca el flujo: manda a Google. */
  async function entrarConGoogle(volver = null) {
    if (!GOOGLE_CONFIGURADO) {
      error.value =
        'El inicio de sesión no está disponible en este momento. Inténtalo más tarde.'

      return
    }

    error.value = null
    entrando.value = true

    try {
      await irAGoogle(volver)
    } catch {
      entrando.value = false
      error.value = 'No pudimos conectar con Google. Vuelve a intentarlo.'
    }
  }

  /**
   * Procesa la vuelta de Google.
   * @returns {string|null} ruta interna a la que ir, si se guardó una
   */
  async function procesarRetorno({ code, state, error: errorGoogle }) {
    entrando.value = true
    error.value = null

    try {
      if (errorGoogle) {
        // access_denied es que la persona pulsó "cancelar": no es un fallo.
        error.value =
          errorGoogle === 'access_denied'
            ? 'Cancelaste el inicio de sesión.'
            : 'Google no pudo completar el inicio de sesión.'

        return null
      }

      const verificador = verificarEstado(state)

      if (!code || !verificador) {
        error.value =
          'No pudimos verificar la respuesta de Google. Vuelve a intentarlo desde el inicio.'

        return null
      }

      const { access_token: token } = await canjearCodigo(code, verificador)

      if (!token) {
        error.value = 'Google no devolvió una sesión válida.'

        return null
      }

      const perfil = await obtenerPerfil(token)

      escribir(CLAVE_TOKEN, token)
      usuario.value = perfil
      escribir(CLAVE_USUARIO, perfil)

      return destinoGuardado()
    } catch (e) {
      error.value = e.message

      return null
    } finally {
      entrando.value = false
    }
  }

  function cerrarSesion() {
    usuario.value = null
    error.value = null
    escribir(CLAVE_USUARIO, null)
    escribir(CLAVE_TOKEN, null)
  }

  return {
    usuario,
    entrando,
    error,
    autenticado,
    disponible,
    iniciales,
    entrarConGoogle,
    procesarRetorno,
    cerrarSesion,
  }
})
