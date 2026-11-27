namespace Product_Service.Models.Entities;

/// <summary>
/// El marcador del lector dentro de un shard de Kinesis: el número de secuencia
/// del último registro procesado. Al arrancar, el lector pide un iterador
/// "después de" este número, así no vuelve a leer lo ya hecho tras un reinicio
/// (el "checkpoint" del que habla docs/PLAN-EFT.md §5: lo anota uno mismo).
/// </summary>
public sealed class CheckpointStream
{
    public required string StreamShard { get; set; }
    public required string NumeroSecuencia { get; set; }
    public DateTimeOffset ActualizadoEn { get; set; }
}
