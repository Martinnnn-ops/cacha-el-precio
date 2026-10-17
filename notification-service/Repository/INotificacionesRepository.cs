namespace Notification_Service.Repository;

public interface INotificacionesRepository
{
    // ¿Ya se le mando este evento a esta persona? (clave (event_id, owner)).
    Task<bool> YaEnviadoAsync(Guid eventId, string owner, CancellationToken ct);

    // Deja la marca despues de enviar. Idempotente: si otra instancia la puso
    // primero, no falla.
    Task MarcarEnviadoAsync(Guid eventId, string owner, string tipo, CancellationToken ct);
}
