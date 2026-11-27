namespace Product_Service.Streaming;

/// <summary>Un registro leído del stream, con lo justo para procesarlo y marcarlo.</summary>
public sealed record RegistroStream(
    string ShardId,
    string NumeroSecuencia,
    string ParticionKey,
    string Cuerpo);

/// <summary>
/// El lector habla con esta interfaz, no con el SDK de Kinesis (misma idea que
/// <c>IColaMensajes</c> para SQS). Migrar a otro stream sería otra
/// implementación, sin tocar <c>LectorPrecios</c>, y en las pruebas se usa un
/// doble en memoria.
/// </summary>
public interface ILectorStream
{
    /// <summary>false cuando no hay stream configurado: el lector no arranca.</summary>
    bool Habilitada { get; }

    /// <summary>Los shards del stream (con 1 shard, una sola entrada).</summary>
    Task<IReadOnlyList<string>> ShardsAsync(CancellationToken ct);

    /// <summary>
    /// Devuelve el siguiente lote de registros de un shard. <paramref name="checkpoint"/>
    /// solo se usa la primera vez, para empezar justo después del último número de
    /// secuencia ya procesado (o desde el principio si viene null). Después, la
    /// implementación avanza su propio iterador entre llamadas.
    /// </summary>
    Task<IReadOnlyList<RegistroStream>> LeerAsync(string shardId, string? checkpoint, CancellationToken ct);
}
