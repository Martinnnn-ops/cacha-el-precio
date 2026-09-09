// Inicio de sesión con Google — OAuth 2.0 Authorization Code + PKCE,
// mediado por Cognito (Google como Identity Provider federado).
//
// Se usa el flujo de código con PKCE y NO el implícito: el implícito devuelve
// el token en el fragmento de la URL, donde queda en el historial del
// navegador y a la vista de cualquier extensión instalada. Está desaconsejado
// para aplicaciones de navegador desde el BCP de OAuth 2.0.
//
// ¿Por qué Cognito y no Google directo? Google exige un client secret en el
// canje de código incluso usando PKCE, para clientes tipo "Web application" —
// y un secret no puede vivir en código de navegador. Cognito sí permite PKCE
// puro para app clients públicos (sin secret) y es quien guarda el secret de
// Google de forma segura en su lado, hablando con Google por nosotros.
//
// ┌─ NOTA DE SEGURIDAD ───────────────────────────────────────────────────┐
// │ PKCE permite canjear el código sin secreto de cliente, que es lo que   │
// │ hace viable este flujo desde el navegador. Aun así, lo más seguro es   │
// │ que el canje lo haga el backend y devuelva una cookie de sesión        │
// │ HttpOnly + Secure + SameSite: así el token nunca pasa por JavaScript   │
// │ y un XSS no puede leerlo. Para migrar a eso sólo cambia canjearCodigo. │
// └───────────────────────────────────────────────────────────────────────┘

const DOMINIO_COGNITO = import.meta.env.VITE_COGNITO_DOMAIN ?? ''

const AUTORIZACION = `${DOMINIO_COGNITO}/oauth2/authorize`
const TOKEN = `${DOMINIO_COGNITO}/oauth2/token`
const PERFIL = `${DOMINIO_COGNITO}/oauth2/userInfo`

// El client_id ahora es el del App Client de Cognito, no el de Google.
// Cognito es quien tiene registrado el client_id/secret de Google por dentro.
const CLIENT_ID = import.meta.env.VITE_COGNITO_CLIENT_ID ?? ''

// Sin cliente configurado no se puede hablar con Cognito. La aplicación lo
// consulta para avisar en vez de mandar al usuario a una pantalla de error.
export const GOOGLE_CONFIGURADO = CLIENT_ID !== '' && DOMINIO_COGNITO !== ''

// Claves de un solo uso, en sessionStorage y no en localStorage: valen para el
// viaje de ida y vuelta y no tienen por qué sobrevivir a la pestaña.
const CLAVE_VERIFIER = 'cep:pkce'
const CLAVE_ESTADO = 'cep:estado'
const CLAVE_VOLVER = 'cep:volver'

export function urlDeRetorno() {
  return `${window.location.origin}/auth/google`
}

function base64Url(bytes) {
  let texto = ''

  bytes.forEach((b) => {
    texto += String.fromCharCode(b)
  })

  return btoa(texto).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function aleatorio(bytes = 32) {
  const buf = new Uint8Array(bytes)

  crypto.getRandomValues(buf)

  return base64Url(buf)
}

async function desafio(verificador) {
  const hash = await crypto.subtle.digest(
    'SHA-256',
    new TextEncoder().encode(verificador),
  )

  return base64Url(new Uint8Array(hash))
}

function guardar(clave, valor) {
  try {
    sessionStorage.setItem(clave, valor)
  } catch {
    // Almacenamiento bloqueado. Se detecta al volver, en verificarEstado.
  }
}

function consumir(clave) {
  try {
    const valor = sessionStorage.getItem(clave)

    // Se borra al leerlo: son de un solo uso, y dejarlos permitiría reutilizar
    // un código interceptado.
    sessionStorage.removeItem(clave)

    return valor
  } catch {
    return null
  }
}

/**
 * Manda al usuario a Cognito, que a su vez lo redirige a Google. No
 * devuelve: la página se sustituye.
 * @param {string|null} volver ruta interna a la que regresar tras entrar
 */
export async function irAGoogle(volver = null) {
  const verificador = aleatorio()
  const estado = aleatorio(16)

  guardar(CLAVE_VERIFIER, verificador)
  guardar(CLAVE_ESTADO, estado)

  if (volver) guardar(CLAVE_VOLVER, volver)

  const params = new URLSearchParams({
    client_id: CLIENT_ID,
    redirect_uri: urlDeRetorno(),
    response_type: 'code',
    scope: 'openid email profile',
    code_challenge: await desafio(verificador),
    code_challenge_method: 'S256',
    // `estado` es la defensa contra CSRF: si al volver no coincide con el que
    // guardamos, la respuesta no corresponde a la petición que hicimos.
    state: estado,
    // Salta directo a Google sin pasar por la pantalla de selección de
    // proveedor de Cognito, ya que solo ofrecemos login con Google.
    identity_provider: 'Google',
  })

  window.location.assign(`${AUTORIZACION}?${params.toString()}`)
}

/** Comprueba el estado de vuelta y consume el verificador. */
export function verificarEstado(estadoRecibido) {
  const esperado = consumir(CLAVE_ESTADO)
  const verificador = consumir(CLAVE_VERIFIER)

  if (!esperado || !verificador || esperado !== estadoRecibido) return null

  return verificador
}

export function destinoGuardado() {
  return consumir(CLAVE_VOLVER)
}

/** Canjea el código de autorización por el token de acceso, con Cognito. */
export async function canjearCodigo(codigo, verificador) {
  const respuesta = await fetch(TOKEN, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      client_id: CLIENT_ID,
      code: codigo,
      code_verifier: verificador,
      grant_type: 'authorization_code',
      redirect_uri: urlDeRetorno(),
    }),
  })

  if (!respuesta.ok) {
    throw new Error('No pudimos completar el inicio de sesión con Google.')
  }

  return respuesta.json()
}

/**
 * Datos de la persona que entró.
 *
 * Se piden al endpoint de perfil de Cognito con el access_token, en vez de
 * leer las claims del id_token: verificar la firma de un JWT en el navegador
 * es fácil de hacer mal, y creerse un id_token sin verificar es peor que no
 * mirarlo.
 */
export async function obtenerPerfil(accessToken) {
  const respuesta = await fetch(PERFIL, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })

  if (!respuesta.ok) {
    throw new Error('No pudimos leer tu perfil de Google.')
  }

  const datos = await respuesta.json()

  return {
    correo: datos.email ?? '',
    nombre: datos.name ?? datos.given_name ?? datos.email ?? '',
    avatar: datos.picture ?? null,
    correoVerificado: datos.email_verified === true,
  }
}