namespace Notification_Service.Models.Entities;

// Una fila por (evento, seguidor) ya avisado. Es la marca que evita reenviar el
// mismo correo cuando SQS entrega el mensaje mas de una vez (ver MENSAJERIA.md).
public sealed class NotificacionEnviada
{
    public Guid EventId { get; set; }
    public string Owner { get; set; } = "";
    public string Tipo { get; set; } = "";
    public DateTimeOffset EnviadoEn { get; set; }
}
