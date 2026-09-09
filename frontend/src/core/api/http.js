import axios from 'axios'

// Única puerta de salida HTTP de la aplicación.
//
// Ningún componente ni store importa axios directamente: si mañana cambia la
// URL base, la versión de la API o el formato de error del backend, se toca
// este archivo y nada más.

export const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

// El Product Service ASP.NET Core versiona mediante la cabecera `Version`.
// El gateway también la fija al reenviar, pero enviarla desde aquí mantiene
// explícito el contrato y permite hablar directamente con el servicio.
export const API_VERSION = import.meta.env.VITE_API_VERSION ?? '1.0'

// Sin URL base no hay backend contra el que hablar: los servicios lo consultan
// para responder con datos de ejemplo en lugar de fallar.
export const USAR_MOCK = BASE_URL === ''

const http = axios.create({
  baseURL: BASE_URL,
  timeout: 10000,
  headers: { Accept: 'application/json' },
})

// ——— petición ———
http.interceptors.request.use((config) => {
  const version = config.version ?? API_VERSION

  config.headers.Version = version

  const token = leerToken()

  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

function leerToken() {
  try {
    // Mismo sitio donde lo deja cuenta.store: sessionStorage, no localStorage.
    const guardado = sessionStorage.getItem('cep:token')

    return guardado ? JSON.parse(guardado) : null
  } catch {
    // Modo privado o almacenamiento bloqueado: se sigue sin sesión.
    return null
  }
}

// ——— respuesta ———
// Traduce cualquier fallo a un Error con un mensaje que se pueda enseñar tal
// cual en pantalla. Sin esto a la vista le llega un AxiosError y termina
// mostrando "Request failed with status code 500".
http.interceptors.response.use(
  (response) => response.data,

  (error) => {
    const respuesta = error.response

    if (respuesta) {
      const { status } = respuesta

      // ASP.NET puede responder Problem Details, mientras que el gateway usa
      // { estado, error, mensaje }. Se admiten ambos para no filtrar detalles
      // técnicos a la interfaz.
      const detalle =
        respuesta.data?.detail ??
        respuesta.data?.mensaje ??
        respuesta.data?.title ??
        respuesta.data?.error

      // El detalle técnico —versión de la API, ruta ambigua, traza— va a la
      // consola SÓLO en desarrollo. Al usuario se le dice qué le pasa a él,
      // no qué le pasa al servidor.
      if (import.meta.env.DEV) {
        console.warn(
          `[api] ${respuesta.config?.method?.toUpperCase()} ${respuesta.config?.url} ` +
            `→ ${status}` +
            (respuesta.config?.version ? ` (versión ${respuesta.config.version})` : '') +
            (detalle ? ` · ${detalle}` : ''),
        )
      }

      const mensajes = {
        400: 'No pudimos completar la búsqueda.',
        401: 'Tu sesión expiró. Vuelve a entrar.',
        403: 'No tienes permiso para ver esto.',
        404: 'No encontramos lo que buscabas.',
        429: 'Demasiadas consultas seguidas. Espera un momento.',
      }

      return Promise.reject(
        new Error(
          mensajes[status] ??
            (status >= 500
              ? 'Estamos con problemas. Inténtalo en un rato.'
              : 'Algo no salió bien. Vuelve a intentarlo.'),
        ),
      )
    }

    if (error.code === 'ECONNABORTED') {
      return Promise.reject(
        new Error('La búsqueda tardó demasiado. Inténtalo otra vez.'),
      )
    }

    return Promise.reject(
      new Error('Sin conexión. Revisa tu internet y vuelve a intentarlo.'),
    )
  },
)

export default http
