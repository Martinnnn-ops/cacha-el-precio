import http, { USAR_MOCK } from '@/core/api/http'

// Panel de la mensajeria (EP3, T12). Habla con /api/admin/mensajeria del
// gateway (T11), que exige el grupo admin. Contrato y errores en
// docs/MENSAJERIA.md › Administracion.

const BASE = '/api/admin/mensajeria'
const nombre = (n) => encodeURIComponent(n)

function sinBackend() {
  return Promise.reject(new Error('Sin backend configurado: el panel necesita la API real.'))
}

export const listarColas = () => (USAR_MOCK ? sinBackend() : http.get(`${BASE}/colas`))
export const listarTopicos = () => (USAR_MOCK ? sinBackend() : http.get(`${BASE}/topicos`))
export const compararTopologia = () => (USAR_MOCK ? sinBackend() : http.get(`${BASE}/topologia`))

export const verMensajes = (cola) => http.get(`${BASE}/colas/${nombre(cola)}/mensajes`)

// El redrive espera a SQS (hasta ~25 s); el borde corta a los 30.
export const reprocesar = (dlq) => http.post(`${BASE}/colas/${nombre(dlq)}/redrive`, null, { timeout: 30000 })

export const crearCola = (datos) => http.post(`${BASE}/colas`, datos)
export const crearTopico = (datos) => http.post(`${BASE}/topicos`, datos)
export const crearSuscripcion = (datos) => http.post(`${BASE}/suscripciones`, datos)

const forzar = (si) => (si ? '?forzar=true' : '')
export const borrarCola = (n, f) => http.delete(`${BASE}/colas/${nombre(n)}${forzar(f)}`)
export const borrarTopico = (n, f) => http.delete(`${BASE}/topicos/${nombre(n)}${forzar(f)}`)
export const borrarSuscripcion = (topico, cola, f) =>
  http.delete(`${BASE}/suscripciones/${nombre(topico)}/${nombre(cola)}${forzar(f)}`)

/**
 * Traduce un error del panel a { mensaje, campos }. El 400 del servidor trae
 * errores: [{ campo, error }] y el 409/404/502 un mensaje propio: eso es lo que
 * se muestra, no el texto generico del cliente HTTP.
 */
export function leerError(e) {
  const datos = e?.datos ?? {}
  const campos = {}
  for (const { campo, error } of datos.errores ?? []) {
    ;(campos[campo] ??= []).push(error)
  }
  return { mensaje: datos.mensaje ?? e?.message ?? 'Algo no salió bien.', campos }
}

// Bandeja de correos generados por notification-service (ver
// notification-service/Correo/EndpointsBandeja.cs). El destinatario llega
// enmascarado; el HTML es el mismo que recibe quien sigue la prenda.
export const listarCorreos = (limite = 20) => http.get(`${BASE}/correos`, { params: { limite } })
export const verCorreo = (id) => http.get(`${BASE}/correos/${id}`)
export const verEjemplo = (tipo) => http.get(`${BASE}/correos/ejemplo/${nombre(tipo)}`)

// Simular un aviso: el gateway arma el cambio con los datos reales del producto
// y lo publica en cep-cambios-oferta. No toca precios del catálogo.
export const simularAviso = (datos) => http.post(`${BASE}/simular-aviso`, datos)
export const verProducto = (id) => http.get(`/productos/${id}`)
// Seguir la prenda con la cuenta del admin: sin seguidores no hay a quién avisar.
export const seguirProducto = (id) => http.post(`/seguimiento/${id}`)
