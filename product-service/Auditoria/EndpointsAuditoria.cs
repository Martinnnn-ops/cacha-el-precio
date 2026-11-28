using Microsoft.EntityFrameworkCore;
using Product_Service.Data;

namespace Product_Service.Auditoria;

// Lo que ve el panel de admin: los últimos cambios auditados. Ruta /internal,
// solo alcanzable dentro de la red del compose; el gateway la publica con el
// grupo admin (como la bandeja de correos de notification-service).
public static class EndpointsAuditoria
{
    public static void MapAuditoria(this IEndpointRouteBuilder app)
    {
        app.MapGet("/internal/auditoria", async (int? limite, bool? soloSospechosos, string? correlationId,
            ProductDbContext db, CancellationToken ct) =>
        {
            var consulta = db.EventosAuditoria.AsNoTracking();
            if (soloSospechosos == true) consulta = consulta.Where(e => e.Sospechoso);
            if (!string.IsNullOrWhiteSpace(correlationId)) consulta = consulta.Where(e => e.CorrelationId == correlationId);

            var eventos = await consulta
                .OrderByDescending(e => e.RecibidoEn)
                .Take(Math.Clamp(limite ?? 50, 1, 200))
                .ToListAsync(ct);

            var resumen = new
            {
                total = await db.EventosAuditoria.CountAsync(ct),
                sospechosos = await db.EventosAuditoria.CountAsync(e => e.Sospechoso, ct),
                baja = ProcesadorAuditoria.BajaSospechosaPct
            };
            return Results.Ok(new { resumen, eventos });
        });
    }
}
