namespace Product_Service.Models.Entities;

/// <summary>
/// Cuántas veces pasó algo en una etapa, por correlationId: «el barrido
/// barrido-paris-… guardó 217 ofertas», «… publicó 3 cambios». Con esto el panel
/// de admin dibuja la línea de tiempo de un barrido (PLAN-EFT §3, paso 4).
/// </summary>
public sealed class Traza
{
    public required string CorrelationId { get; set; }
    public required string Etapa { get; set; }
    public long Cantidad { get; set; }
    public DateTimeOffset PrimeraVez { get; set; }
    public DateTimeOffset UltimaVez { get; set; }
}
