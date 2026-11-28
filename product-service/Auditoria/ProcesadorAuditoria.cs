using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Product_Service.Data;
using Product_Service.Exceptions;
using Product_Service.Mensajeria;
using Product_Service.Models.Entities;
using Product_Service.Observabilidad;

namespace Product_Service.Auditoria;

/// <summary>
/// Un mensaje de cep-auditoria-precios: lo guarda en EventosAuditoria una sola
/// vez por eventId y lo marca sospechoso si el precio cayó de golpe.
/// Ver docs/PLAN-EFT.md §4.
/// </summary>
public sealed class ProcesadorAuditoria(
    ProductDbContext db,
    ILogger<ProcesadorAuditoria> log,
    RegistroTrazas? trazas = null)
{
    /// <summary>
    /// Desde qué baja se sospecha de un error del scraper (un 0 de menos, un
    /// precio de tarjeta leído como normal). Se ajusta mirando datos reales.
    /// </summary>
    public const int BajaSospechosaPct = 70;

    private static readonly HashSet<string> TiposConocidos = ["precio.bajo", "oferta.agotada", "oferta.repuesta"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Devuelve el eventId. Repetir el mismo mensaje no cambia nada.</summary>
    public async Task<string> ProcesarAsync(string cuerpo, CancellationToken ct)
    {
        SobreEntrante sobre = LeerSobre(cuerpo);
        using var _ = log.BeginScope("eventId={EventId}", sobre.EventId);
        using var __ = Correlacion.Iniciar(log, Correlacion.Validar(sobre.CorrelationId));

        if (await db.EventosAuditoria.AnyAsync(e => e.EventId == sobre.EventId, ct))
        {
            log.LogInformation("Resultado=duplicado: el cambio {EventId} ya estaba auditado; no se guarda de nuevo", sobre.EventId);
            if (trazas is not null) await trazas.AnotarAsync("duplicado-ignorado", ct);
            return sobre.EventId!;
        }

        EventoAuditoria evento = Armar(sobre);
        db.EventosAuditoria.Add(evento);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dos copias del mismo mensaje procesadas a la vez: la otra ganó.
            log.LogInformation("Resultado=duplicado: {EventId} lo guardó otra copia al mismo tiempo", sobre.EventId);
            if (trazas is not null) await trazas.AnotarAsync("duplicado-ignorado", ct);
            return sobre.EventId!;
        }

        if (trazas is not null)
        {
            await trazas.AnotarAsync("cambio-auditado", ct);
            if (evento.Sospechoso) await trazas.AnotarAsync("cambio-sospechoso", ct);
        }

        if (evento.Sospechoso)
            log.LogWarning("Cambio sospechoso en {Store}/{ProductId}: {Motivo}", evento.Store, evento.ProductId, evento.Motivo);
        else
            log.LogInformation("Cambio {Tipo} auditado: {Store}/{ProductId}", evento.Tipo, evento.Store, evento.ProductId);

        return sobre.EventId!;
    }

    /// <summary>El sobre de un cambio de oferta (precio.bajo, oferta.agotada, oferta.repuesta), v1.</summary>
    public static SobreEntrante LeerSobre(string cuerpo)
    {
        SobreEntrante? sobre;
        try
        {
            sobre = JsonSerializer.Deserialize<SobreEntrante>(cuerpo, Json);
        }
        catch (JsonException e)
        {
            throw new ErrorDefinitivoException("JSON del sobre invalido", e);
        }

        if (sobre is null)
            throw new ErrorDefinitivoException("el sobre viene vacio");
        if (string.IsNullOrWhiteSpace(sobre.EventId))
            throw new ErrorDefinitivoException("el sobre no trae eventId");
        if (sobre.Type is null || !TiposConocidos.Contains(sobre.Type))
            throw new ErrorDefinitivoException($"type '{sobre.Type}' no es un cambio de oferta");
        if (sobre.Version != 1)
            throw new ErrorDefinitivoException($"version {sobre.Version} no soportada (se espera 1)");
        return sobre;
    }

    public static EventoAuditoria Armar(SobreEntrante sobre)
    {
        Datos d;
        try
        {
            d = sobre.Data.Deserialize<Datos>(Json) ?? throw new ErrorDefinitivoException("data viene vacío");
        }
        catch (JsonException e)
        {
            throw new ErrorDefinitivoException($"data no calza con un cambio de oferta: {e.Message}", e);
        }

        int? anterior = d.PreviousPrice ?? d.LastPrice;
        int? nuevo = d.NewPrice ?? d.Price;
        int? pct = d.DiscountPct ?? (anterior is > 0 && nuevo is int n ? (int)Math.Round(100.0 * (anterior.Value - n) / anterior.Value) : null);
        bool sospechoso = sobre.Type == "precio.bajo" && pct >= BajaSospechosaPct;

        return new EventoAuditoria
        {
            EventId = sobre.EventId!,
            Tipo = sobre.Type!,
            ProductId = d.ProductId,
            Slug = d.Slug,
            Nombre = d.Name,
            Store = d.Store,
            PrecioAnterior = anterior,
            PrecioNuevo = nuevo,
            DescuentoPct = pct,
            Sospechoso = sospechoso,
            Motivo = sospechoso ? $"bajó {pct}% de golpe: posible error del scraper" : null,
            CorrelationId = Correlacion.Validar(sobre.CorrelationId),
            OcurridoEn = sobre.OccurredAt ?? DateTimeOffset.UtcNow,
            RecibidoEn = DateTimeOffset.UtcNow
        };
    }

    private sealed record Datos(
        int ProductId, string? Slug, string? Name, string? Store,
        int? PreviousPrice, int? NewPrice, int? DiscountPct, int? LastPrice, int? Price);
}
