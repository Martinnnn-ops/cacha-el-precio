using System.Text;
using Amazon.Kinesis;
using Amazon.Kinesis.Model;

namespace Product_Service.Streaming;

/// <summary>
/// <see cref="ILectorStream"/> sobre Amazon Kinesis. Lee cep-precios en lotes.
///
/// Kinesis se lee con un "iterador de shard": un token que se pide una vez (desde
/// el checkpoint o desde el inicio) y que cada GetRecords renueva para la próxima
/// llamada. Ese token es efímero (vive ~5 min), así que se guarda en memoria y se
/// vuelve a pedir si se perdió. El avance durable lo lleva el checkpoint en la
/// base, no este token.
/// </summary>
public sealed class LectorStreamKinesis : ILectorStream
{
    private readonly IAmazonKinesis kinesis;
    private readonly string stream;
    private readonly ILogger<LectorStreamKinesis> log;

    // Iterador vigente por shard, entre llamadas. null = hay que volver a pedirlo.
    private readonly Dictionary<string, string?> iteradores = [];

    public LectorStreamKinesis(IAmazonKinesis kinesis, IConfiguration config, ILogger<LectorStreamKinesis> log)
    {
        this.kinesis = kinesis;
        this.log = log;
        stream = config["KINESIS_PRECIOS_STREAM"] ?? "";
    }

    public bool Habilitada => !string.IsNullOrWhiteSpace(stream);

    public async Task<IReadOnlyList<string>> ShardsAsync(CancellationToken ct)
    {
        var shards = new List<string>();
        string? siguiente = null;
        do
        {
            var req = siguiente is null
                ? new ListShardsRequest { StreamName = stream }
                : new ListShardsRequest { NextToken = siguiente };
            var resp = await kinesis.ListShardsAsync(req, ct);
            shards.AddRange(resp.Shards.Select(s => s.ShardId));
            siguiente = resp.NextToken;
        } while (!string.IsNullOrEmpty(siguiente));
        return shards;
    }

    public async Task<IReadOnlyList<RegistroStream>> LeerAsync(string shardId, string? checkpoint, CancellationToken ct)
    {
        string? iterador = await IteradorAsync(shardId, checkpoint, ct);
        if (iterador is null) return [];

        GetRecordsResponse resp;
        try
        {
            resp = await kinesis.GetRecordsAsync(new GetRecordsRequest { ShardIterator = iterador, Limit = 500 }, ct);
        }
        catch (ExpiredIteratorException)
        {
            // El token caducó (lector pausado un rato): se descarta y la próxima
            // vuelta se pide uno nuevo desde el checkpoint.
            log.LogWarning("Iterador de {Shard} caducado; se pedirá de nuevo", shardId);
            iteradores[shardId] = null;
            return [];
        }

        iteradores[shardId] = resp.NextShardIterator; // null = el shard se cerró

        return resp.Records.Select(r => new RegistroStream(
            shardId,
            r.SequenceNumber,
            r.PartitionKey,
            Encoding.UTF8.GetString(r.Data.ToArray()))).ToList();
    }

    private async Task<string?> IteradorAsync(string shardId, string? checkpoint, CancellationToken ct)
    {
        if (iteradores.TryGetValue(shardId, out var vigente) && vigente is not null)
            return vigente;

        var req = new GetShardIteratorRequest { StreamName = stream, ShardId = shardId };
        if (string.IsNullOrWhiteSpace(checkpoint))
        {
            // Primera vez sin checkpoint: desde el registro más viejo que quede
            // (hasta 24 h), para no perder lo capturado mientras el lector no corría.
            req.ShardIteratorType = ShardIteratorType.TRIM_HORIZON;
        }
        else
        {
            // Justo después de lo último ya procesado.
            req.ShardIteratorType = ShardIteratorType.AFTER_SEQUENCE_NUMBER;
            req.StartingSequenceNumber = checkpoint;
        }

        var resp = await kinesis.GetShardIteratorAsync(req, ct);
        iteradores[shardId] = resp.ShardIterator;
        return resp.ShardIterator;
    }
}
