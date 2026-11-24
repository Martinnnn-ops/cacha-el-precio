namespace Product_Service.Models.Entities;

/// <summary>
/// Los eventId de precios ya sumados a las estadísticas. Es la idempotencia
/// TÉCNICA del stream: Kinesis entrega "al menos una vez", así que el mismo
/// precio puede llegar dos veces (reinicio del lector antes del checkpoint); con
/// esta tabla se suma una sola vez. Misma idea que el <c>(event_id, owner)</c>
/// de notification-service (docs/PLAN-EFT.md §4).
///
/// Se poda sola: como el stream retiene 24 h, un duplicado no puede llegar más
/// tarde que eso, y las filas viejas se borran (ver LectorPrecios).
/// </summary>
public sealed class EventoPrecioProcesado
{
    public required string EventId { get; set; }
    public DateTimeOffset ProcesadoEn { get; set; }
}
