namespace Product_Service.Models.Entities;

/// <summary>
/// Un cambio de oferta tal como llegó a cep-auditoria-precios (SNS copia ahí
/// TODOS los cambios, sin filtro). Es la trazabilidad del flujo 2: qué cambió,
/// cuándo y qué barrido lo provocó (CorrelationId).
///
/// La llave es el EventId: SQS entrega «al menos una vez», y un mismo cambio
/// que llega dos veces se guarda una sola (idempotencia técnica).
/// </summary>
public sealed class EventoAuditoria
{
    public required string EventId { get; set; }
    public required string Tipo { get; set; }
    public int ProductId { get; set; }
    public string? Slug { get; set; }
    public string? Nombre { get; set; }
    public string? Store { get; set; }
    public int? PrecioAnterior { get; set; }
    public int? PrecioNuevo { get; set; }
    public int? DescuentoPct { get; set; }

    /// <summary>Bajó tanto de golpe que probablemente es un error del scraper.</summary>
    public bool Sospechoso { get; set; }
    public string? Motivo { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset OcurridoEn { get; set; }
    public DateTimeOffset RecibidoEn { get; set; }
}
