// Carga del script de Google AdSense.
//
// Se inyecta desde aquí y NO desde index.html a propósito: así no se descarga
// nada de terceros cuando no hay anuncios configurados —en desarrollo, en las
// pruebas y mientras la cuenta de AdSense esté a medias—. Un <script> fijo en
// el <head> se baja siempre, aunque no haya un solo bloque que mostrar.

const ORIGEN = 'https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js'

// Ojo al prefijo: en ads.txt el identificador va como "pub-…" y aquí como
// "ca-pub-…". Es el mismo número con distinta forma, y confundirlos deja la
// cuenta sin verificar sin que nada avise.
export const ADSENSE_CLIENT = import.meta.env.VITE_ADSENSE_CLIENT ?? ''

export const ADSENSE_ACTIVO = ADSENSE_CLIENT !== ''

// La promesa se guarda a nivel de módulo: varios bloques en la misma página
// piden el script a la vez y sólo debe inyectarse uno.
let carga = null

export function cargarAdsense() {
  if (!ADSENSE_ACTIVO) {
    return Promise.reject(new Error('AdSense no está configurado'))
  }

  if (carga) return carga

  carga = new Promise((resolve, reject) => {
    if (typeof document === 'undefined') {
      reject(new Error('Sin documento'))

      return
    }

    const script = document.createElement('script')

    script.src = `${ORIGEN}?client=${encodeURIComponent(ADSENSE_CLIENT)}`
    script.async = true
    // Lo pide Google en su fragmento oficial.
    script.crossOrigin = 'anonymous'

    script.addEventListener('load', () => resolve())
    script.addEventListener('error', () => {
      // Se limpia para que un fallo puntual —red caída, bloqueador— no deje la
      // promesa rechazada para siempre en el resto de la sesión.
      carga = null
      reject(new Error('No se pudo cargar AdSense'))
    })

    document.head.appendChild(script)
  })

  return carga
}

// Sólo para las pruebas: permite volver al estado inicial entre casos.
export function reiniciarAdsense() {
  carga = null
}
