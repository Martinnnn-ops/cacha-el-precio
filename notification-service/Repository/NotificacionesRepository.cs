using Microsoft.EntityFrameworkCore;
using Notification_Service.Data;
using Notification_Service.Models.Entities;

namespace Notification_Service.Repository;

public sealed class NotificacionesRepository(NotificationDbContext db) : INotificacionesRepository
{
    public Task<bool> YaEnviadoAsync(Guid eventId, string owner, CancellationToken ct) =>
        db.NotificacionesEnviadas.AsNoTracking()
            .AnyAsync(n => n.EventId == eventId && n.Owner == owner, ct);

    public async Task MarcarEnviadoAsync(Guid eventId, string owner, string tipo, CancellationToken ct)
    {
        db.NotificacionesEnviadas.Add(new NotificacionEnviada
        {
            EventId = eventId,
            Owner = owner,
            Tipo = tipo,
            EnviadoEn = DateTimeOffset.UtcNow
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Otra entrega del mismo mensaje ya dejo la fila. El correo salio una
            // sola vez; la marca existe. No es un error.
            db.ChangeTracker.Clear();
        }
    }
}
