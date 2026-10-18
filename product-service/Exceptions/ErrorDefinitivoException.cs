namespace Product_Service.Exceptions;

// Error que no se arregla reintentando: JSON roto, type/version desconocido o
// una oferta que no pasa la validacion del catalogo (precio <= 0, sin nombre,
// prenda infantil). El consumidor manda el mensaje a la DLQ con atributos de
// diagnostico y lo borra de la cola, sin gastar los 5 reintentos. Todo lo
// demas (BD caida, timeout) es recuperable: se propaga, no se borra y SQS lo
// reintenta. Mismo criterio que notification-service (docs/MENSAJERIA.md).
public sealed class ErrorDefinitivoException(string razon, Exception? inner = null)
    : Exception(razon, inner)
{
    public string Razon { get; } = razon;
}
