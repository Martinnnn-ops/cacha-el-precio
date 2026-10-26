using Notification_Service.Correo;
using Notification_Service.Models.Entities;

namespace Notification_Service.Repository;

public interface IBandejaCorreos
{
    // Guarda la copia de un correo ya generado y recorta la bandeja a los
    // ultimos BandejaCorreos.Maximo.
    Task GuardarAsync(Guid eventId, string tipo, string modo, CorreoSalida correo, CancellationToken ct);

    // Los mas recientes primero, sin el HTML ni el texto (para la lista).
    Task<IReadOnlyList<CorreoGenerado>> ListarAsync(int limite, CancellationToken ct);

    Task<CorreoGenerado?> ObtenerAsync(long id, CancellationToken ct);
}
