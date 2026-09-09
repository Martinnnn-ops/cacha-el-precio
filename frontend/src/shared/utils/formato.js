const CLP = new Intl.NumberFormat('es-CL', {
  style: 'currency',
  currency: 'CLP',
  maximumFractionDigits: 0,
})

export function formatearPrecio(valor) {
  if (typeof valor !== 'number' || Number.isNaN(valor)) return 'Sin precio'

  return CLP.format(valor)
}

export function formatearFecha(iso) {
  const fecha = new Date(iso)

  if (Number.isNaN(fecha.getTime())) return ''

  // timeZone UTC a propósito. "2026-08-15" se interpreta como medianoche UTC,
  // y al formatearlo en la hora de Chile (UTC−4) caía en el día ANTERIOR: un
  // precio del 15 se mostraba como del 14. Como son fechas sin hora, lo
  // correcto es leerlas y escribirlas en el mismo huso.
  return new Intl.DateTimeFormat('es-CL', {
    day: '2-digit',
    month: 'short',
    timeZone: 'UTC',
  }).format(fecha)
}

const RELATIVO = new Intl.RelativeTimeFormat('es-CL', { numeric: 'auto' })

// "hace 10 minutos", "hace 2 horas". Se usa para decir de cuándo son unos
// precios cuando no se han podido actualizar.
export function tiempoRelativo(marca) {
  if (typeof marca !== 'number' || Number.isNaN(marca)) return ''

  const segundos = Math.round((marca - Date.now()) / 1000)
  const escalas = [
    [60, 'second', 1],
    [3600, 'minute', 60],
    [86400, 'hour', 3600],
    [Infinity, 'day', 86400],
  ]

  const [, unidad, divisor] = escalas.find(
    ([limite]) => Math.abs(segundos) < limite,
  )

  return RELATIVO.format(Math.round(segundos / divisor), unidad)
}
