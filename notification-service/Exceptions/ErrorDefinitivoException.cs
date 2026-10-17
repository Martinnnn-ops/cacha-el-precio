namespace Notification_Service.Exceptions;

// Error que no se arregla reintentando: JSON roto, type/version desconocido o
// direccion de correo rechazada por el servidor (SMTP 550/553). El consumidor
// manda el mensaje a la DLQ con atributos de diagnostico y lo borra de la cola,
// sin gastar los 5 reintentos. Todo lo demas (BD caida, timeout, Gmail no
// responde) es recuperable: se propaga, no se borra y SQS lo reintenta.
public sealed class ErrorDefinitivoException(string razon, Exception? inner = null)
    : Exception(razon, inner)
{
    public string Razon { get; } = razon;
}
