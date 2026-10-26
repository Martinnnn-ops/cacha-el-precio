using Microsoft.EntityFrameworkCore;
using Notification_Service.Correo;
using Notification_Service.Data;
using Notification_Service.Models.Entities;

namespace Notification_Service.Repository;

public sealed class BandejaCorreos(NotificationDbContext db) : IBandejaCorreos
{
    // La bandeja es para mirar, no un archivo historico: con 50 alcanza para una
    // demo y la tabla no crece sin limite.
    public const int Maximo = 50;

    public async Task GuardarAsync(Guid eventId, string tipo, string modo, CorreoSalida correo, CancellationToken ct)
    {
        db.CorreosGenerados.Add(new CorreoGenerado
        {
            EventId = eventId,
            Tipo = tipo,
            Para = correo.Para,
            Asunto = correo.Asunto,
            Html = correo.Html,
            Texto = correo.Texto,
            Modo = modo,
            GeneradoEn = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        // Todo lo que quede detras de los ultimos Maximo se borra.
        long? corte = await db.CorreosGenerados.AsNoTracking()
            .OrderByDescending(c => c.Id)
            .Skip(Maximo)
            .Select(c => (long?)c.Id)
            .FirstOrDefaultAsync(ct);
        if (corte is not null)
            await db.CorreosGenerados.Where(c => c.Id <= corte).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<CorreoGenerado>> ListarAsync(int limite, CancellationToken ct) =>
        await db.CorreosGenerados.AsNoTracking()
            .OrderByDescending(c => c.Id)
            .Take(limite)
            .Select(c => new CorreoGenerado
            {
                Id = c.Id,
                EventId = c.EventId,
                Tipo = c.Tipo,
                Para = c.Para,
                Asunto = c.Asunto,
                Modo = c.Modo,
                GeneradoEn = c.GeneradoEn
            })
            .ToListAsync(ct);

    public Task<CorreoGenerado?> ObtenerAsync(long id, CancellationToken ct) =>
        db.CorreosGenerados.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
}
