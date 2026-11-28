using Microsoft.EntityFrameworkCore;
using Product_Service.Data;
using Product_Service.Models.Entities;

namespace Product_Service.Observabilidad;

/// <summary>
/// Anota que algo pasó en una etapa para el correlationId en curso. Si no hay
/// correlationId (un pedido sin seguimiento) no anota nada. Nunca rompe el
/// trabajo de verdad: si falla, deja un warning y sigue.
/// </summary>
public sealed class RegistroTrazas(ProductDbContext db, ILogger<RegistroTrazas> log)
{
    public async Task AnotarAsync(string etapa, CancellationToken ct = default)
    {
        // Se cuenta siempre, venga o no con correlationId (Prometheus no lo necesita).
        MetricasNegocio.Etapas.WithLabels(etapa).Inc();
        if (Correlacion.Actual is not { } id) return;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO product."Trazas" ("CorrelationId", "Etapa", "Cantidad", "PrimeraVez", "UltimaVez")
                VALUES ({id}, {etapa}, 1, now(), now())
                ON CONFLICT ("CorrelationId", "Etapa") DO UPDATE
                SET "Cantidad" = product."Trazas"."Cantidad" + 1, "UltimaVez" = now()
                """, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogWarning(e, "No se pudo anotar la etapa {Etapa} de {CorrelationId}", etapa, id);
        }
    }

    /// <summary>Las etapas de un correlationId, en orden de llegada.</summary>
    public async Task<IReadOnlyList<Traza>> DeAsync(string correlationId, CancellationToken ct) =>
        await db.Trazas.AsNoTracking()
            .Where(t => t.CorrelationId == correlationId)
            .OrderBy(t => t.PrimeraVez)
            .ToListAsync(ct);

    /// <summary>Los últimos correlationId vistos (por ejemplo, los barridos).</summary>
    public async Task<IReadOnlyList<object>> RecientesAsync(string? prefijo, int limite, CancellationToken ct)
    {
        var filas = await db.Trazas.AsNoTracking()
            .Where(t => prefijo == null || t.CorrelationId.StartsWith(prefijo))
            .GroupBy(t => t.CorrelationId)
            .Select(g => new { correlationId = g.Key, primeraVez = g.Min(t => t.PrimeraVez), ultimaVez = g.Max(t => t.UltimaVez) })
            .OrderByDescending(x => x.ultimaVez)
            .Take(Math.Clamp(limite, 1, 100))
            .ToListAsync(ct);
        return filas.Cast<object>().ToList();
    }
}

public static class EndpointsTrazas
{
    /// <summary>Rutas internas (solo red del compose); el gateway las publica con el grupo admin.</summary>
    public static void MapTrazas(this IEndpointRouteBuilder app)
    {
        app.MapGet("/internal/trazas", async (string? prefijo, int? limite, RegistroTrazas r, CancellationToken ct) =>
            Results.Ok(await r.RecientesAsync(prefijo, limite ?? 20, ct)));

        app.MapGet("/internal/trazas/{correlationId}", async (string correlationId, RegistroTrazas r, CancellationToken ct) =>
            Results.Ok((await r.DeAsync(correlationId, ct)).Select(t => new
            {
                servicio = LogsJson.Servicio,
                etapa = t.Etapa,
                cantidad = t.Cantidad,
                primeraVez = t.PrimeraVez,
                ultimaVez = t.UltimaVez
            })));
    }
}
