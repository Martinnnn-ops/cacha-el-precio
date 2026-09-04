// Saneado de todo lo que llega por la URL.
//
// El query string y los parámetros de ruta los controla quien manda el enlace,
// no la aplicación. Cualquier valor que venga de ahí se trata como entrada de
// un desconocido: se valida contra una forma esperada y, si no encaja, se
// descarta entero. Nunca se "arregla" a medias.

// Tope de longitud de un texto de búsqueda. No es seguridad en sí, pero evita
// que una URL de 100 kB acabe en un filtro y en el historial del navegador.
const MAX_TEXTO = 120

// Caracteres de control: sirven para partir cabeceras y para esconder texto a
// la vista. Se escriben escapados a propósito — un literal invisible en el
// código es justo lo que no se quiere al revisar un archivo de seguridad.
const CONTROL = /[\u0000-\u001f\u007f-\u009f]/

/**
 * Devuelve una ruta interna segura, o null.
 *
 * Sin esto, /entrar?volver=https://sitio-falso.cl te lleva a ese sitio después
 * de iniciar sesión, con la confianza de venir de tu propia página. Es un
 * open redirect: la pieza típica para montar un phishing creíble.
 */
export function rutaInternaSegura(valor) {
  if (typeof valor !== 'string') return null

  const ruta = valor.trim()

  if (ruta.length === 0 || ruta.length > 512) return null

  // Una sola barra al principio: "//sitio.cl" es una URL protocolo-relativa y
  // el navegador la trata como externa.
  if (!ruta.startsWith('/') || ruta.startsWith('//')) return null

  // Algunos navegadores normalizan "\\" a "/": "/\\evil.cl" acabaría fuera.
  if (ruta.includes('\\')) return null

  // Cualquier esquema explícito queda fuera (http:, javascript:, data:…).
  if (/^[a-z][a-z0-9+.-]*:/i.test(ruta) || ruta.includes('://')) return null

  if (CONTROL.test(ruta)) return null

  return ruta
}

/** Id de producto: sólo dígitos y con un tope razonable. */
export function idProductoValido(valor) {
  return typeof valor === 'string' && /^[0-9]{1,12}$/.test(valor)
}

/** Texto libre que llega por la URL (búsqueda, categoría). */
export function textoDeUrl(valor) {
  if (typeof valor !== 'string') return ''

  return valor
    .replace(new RegExp(CONTROL.source, 'g'), '')
    .trim()
    .slice(0, MAX_TEXTO)
}
