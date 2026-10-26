namespace Notification_Service.Models.Entities;

// Copia de cada correo que el servicio genero, tal como salio (o tal como se
// registro en modo log). Es la bandeja que muestra el panel de mensajeria: deja
// ver el correo que recibe quien sigue la prenda sin abrir los logs de la EC2.
// Solo se guardan los ultimos (ver BandejaCorreos.Maximo).
public sealed class CorreoGenerado
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string Tipo { get; set; } = "";
    public string Para { get; set; } = "";
    public string Asunto { get; set; } = "";
    public string Html { get; set; } = "";
    public string Texto { get; set; } = "";

    // "log" = solo se registro; "smtp" = salio por Gmail.
    public string Modo { get; set; } = "";
    public DateTimeOffset GeneradoEn { get; set; }
}
