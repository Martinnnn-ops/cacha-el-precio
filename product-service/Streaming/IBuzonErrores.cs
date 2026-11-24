namespace Product_Service.Streaming;

/// <summary>
/// La DLQ del lector de Kinesis. Kinesis no trae cola de mensajes muertos
/// propia: cuando un registro no se puede procesar (JSON roto, precio inválido,
/// o fallo recuperable que agotó los reintentos), el lector copia aquí el cuerpo
/// con el motivo, igual que <c>IColaMensajes.EnviarADlqAsync</c> para SQS, y
/// avanza el checkpoint para no trabar el shard (docs/PLAN-EFT.md §6).
/// </summary>
public interface IBuzonErrores
{
    /// <summary>false si no hay SQS_PRECIOS_DLQ_URL: se registra el error y no se aparta.</summary>
    bool Habilitado { get; }

    Task EnviarAsync(string cuerpo, IReadOnlyDictionary<string, string> diagnostico, CancellationToken ct);
}
