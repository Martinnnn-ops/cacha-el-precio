// Cómo se agrupan los recursos de la mensajería para explicarlos: por flujo,
// no por tipo de recurso. Los nombres son los de infra/mensajeria/topologia.json
// sin el prefijo (cep-); lo que no aparece acá cae en «Administrar › Otros».

export const sinPrefijo = (nombre) => nombre.replace(/^cep-/, '')

// Nombre en palabras para cada cola o tópico, al lado del técnico.
export const ETIQUETAS = {
  'ofertas-crudas': 'Ofertas recién capturadas',
  'ofertas-crudas-dlq': 'Ofertas que no se pudieron guardar',
  'cambios-oferta': 'Cambios de oferta',
  notificaciones: 'Avisos por enviar',
  'notificaciones-dlq': 'Avisos que no se pudieron enviar',
  'auditoria-precios': 'Auditoría de cambios de precio',
}

export const etiqueta = (nombre) => ETIQUETAS[sinPrefijo(nombre)] ?? null

export const FLUJOS = [
  {
    id: 'flujo1',
    titulo: 'Flujo 1 · Ingesta de ofertas',
    corto: 'Ingesta',
    paraQue: 'Las ofertas que captura el scraper, desde que llegan hasta que quedan en el catálogo.',
    acciones: [
      'Ver si hay ofertas esperando o procesándose.',
      'Leer las que fallaron y el motivo (DLQ).',
      'Reintentarlas con el redrive.',
    ],
    resumen:
      'El scraper no escribe directo en la base: deja cada oferta en una cola y product-service la guarda a su ritmo. ' +
      'Si product-service está caído, las ofertas esperan en la cola y no se pierden.',
    pasos: [
      'El scraper captura una oferta y la manda a POST /ingesta.',
      'El API Gateway la deja en la cola cep-ofertas-crudas (sin pasar por el backend).',
      'product-service la lee, la valida y la guarda. Si todo sale bien, la borra de la cola (ACK).',
      'Si es inválida (precio negativo, JSON roto) va a la DLQ al primer intento, con el motivo.',
      'Si falla por algo pasajero (base caída), se reintenta hasta 5 veces y recién ahí va a la DLQ.',
    ],
    colas: ['ofertas-crudas', 'ofertas-crudas-dlq'],
    topicos: [],
  },
  {
    id: 'flujo2',
    titulo: 'Flujo 2 · Avisos de cambios',
    corto: 'Avisos',
    paraQue: 'Los cambios de precio y stock que se reparten a quien corresponda, y los avisos que salen de ahí.',
    acciones: [
      'Ver las colas que reciben los cambios y si algún aviso falló.',
      'Leer qué deja pasar cada regla del tópico.',
      'Revisar el historial de cambios en la auditoría.',
    ],
    resumen:
      'Cuando product-service guarda un cambio importante (bajó el precio, se agotó o volvió), lo publica una sola vez en un ' +
      'tópico. Las reglas deciden qué cola recibe qué: así el catálogo no sabe quién escucha.',
    pasos: [
      'product-service publica el cambio en el tópico cep-cambios-oferta.',
      'Regla 1: a cep-notificaciones solo llegan bajas de 10% o más, agotados y reposiciones.',
      'Regla 2: a cep-auditoria-precios llega todo, sin filtro, para tener el historial.',
      'notification-service lee cep-notificaciones, busca quién sigue la prenda y le arma el correo.',
      'Un aviso que no se puede procesar termina en cep-notificaciones-dlq con su diagnóstico.',
    ],
    colas: ['notificaciones', 'notificaciones-dlq', 'auditoria-precios'],
    topicos: ['cambios-oferta'],
  },
]

const enFlujos = new Set(FLUJOS.flatMap((f) => [...f.colas, ...f.topicos]))
export const esDeUnFlujo = (nombre) => enFlujos.has(sinPrefijo(nombre))

// Nombre en palabras de cada tipo de evento del tópico.
export const TIPOS_AVISO = {
  'precio.bajo': 'Bajó de precio',
  'oferta.agotada': 'Se agotó',
  'oferta.repuesta': 'Volvió a estar disponible',
}

const PLURAL = { 'precio.bajo': 'bajas de precio', 'oferta.agotada': 'agotados', 'oferta.repuesta': 'reposiciones' }
const CAMPOS = { discountPct: 'descuento' }
const UNIDAD = { discountPct: '%' }

function unirConY(partes) {
  return partes.length < 2 ? partes.join('') : `${partes.slice(0, -1).join(', ')} y ${partes.at(-1)}`
}

// Un filtro de SNS en una frase. Cubre lo que usa la topología (type, umbrales
// numéricos y $or); si llega algo más raro, se avisa y queda el JSON a mano.
export function describirFiltro(filtro) {
  if (!filtro) return 'Todo lo que se publica, sin filtro.'
  const texto = Array.isArray(filtro.$or)
    ? filtro.$or.map(describirCondicion).join(', o ')
    : describirCondicion(filtro)
  return texto.charAt(0).toUpperCase() + texto.slice(1) + '.'
}

function describirCondicion(condicion) {
  let sujeto = 'mensajes'
  const extras = []
  for (const [campo, valores] of Object.entries(condicion)) {
    if (campo === 'type') {
      sujeto = unirConY(valores.map((v) => PLURAL[v] ?? v))
      continue
    }
    for (const v of valores) {
      if (v?.numeric) {
        const [op, n] = v.numeric
        const frase = { '>=': 'de', '>': 'de más de', '<=': 'de hasta', '<': 'de menos de', '=': 'de' }[op] ?? op
        extras.push(`${frase} ${n}${UNIDAD[campo] ?? ''}${op === '>=' ? ' o más' : ''}`)
      } else {
        extras.push(`con ${CAMPOS[campo] ?? campo} = ${JSON.stringify(v)}`)
      }
    }
  }
  return [sujeto, ...extras].join(' ')
}

// ¿Este mensaje pasa el filtro de una regla? Mismo criterio que SNS para lo que
// usa la topología: listas de valores exactos, condiciones numéricas y $or. Un
// atributo que falta no cumple. Sirve para anticipar en el panel a qué colas
// llegará un aviso simulado; quien decide de verdad es SNS.
export function pasaFiltro(filtro, atributos) {
  if (!filtro) return true
  if (Array.isArray(filtro.$or)) return filtro.$or.some((f) => pasaFiltro(f, atributos))
  return Object.entries(filtro).every(([campo, condiciones]) => {
    const valor = atributos[campo]
    if (valor === undefined) return false
    return condiciones.some((c) => (c?.numeric ? cumpleNumerico(c.numeric, Number(valor)) : c === valor))
  })
}

function cumpleNumerico(partes, valor) {
  for (let i = 0; i < partes.length; i += 2) {
    const [op, n] = [partes[i], partes[i + 1]]
    const ok = { '>=': valor >= n, '>': valor > n, '<=': valor <= n, '<': valor < n, '=': valor === n }[op]
    if (!ok) return false
  }
  return true
}
