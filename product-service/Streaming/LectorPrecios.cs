using Microsoft.EntityFrameworkCore;
using Product_Service.Data;
using Product_Service.Exceptions;
using Product_Service.Models.Entities;

namespace Product_Service.Streaming;

/// <summary>
/// Lee cep-precios (Kinesis) en segundo plano y va calculando el precio habitual
/// de cada oferta. Por cada registro:
///   éxito o duplicado -> se procesa (o se ignora) y se avanza el checkpoint;
///   definitivo (JSON roto, precio inválido) -> va a la DLQ y se avanza igual,
///                                              para no trabar el shard;
///   recuperable (BD caída) -> se reintenta 1 s -> 2 s -> 4 s; si no cede, a la DLQ.
/// Si no hay stream configurado no arranca y el servicio sigue atendiendo HTTP.
/// Mismo espíritu que ConsumidorOfertas, pero el avance durable es el checkpoint
/// (Kinesis no reparte de nuevo un registro como hace SQS). Ver docs/PLAN-EFT.md §5-6.
/// </summary>
public sealed class LectorPrecios(
    ILectorStream lector,
    IBuzonErrores buzon,
    IServiceScopeFactory scopes,
    ILogger<LectorPrecios> log) : BackgroundService
{
    // Esperas entre reintentos de un registro con fallo recuperable. Tras agotarlas
    // el registro va a la DLQ. Seam de pruebas para no dormir de verdad.
    internal IReadOnlyList<TimeSpan> EsperasReintento { get; set; } =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    // Cada cuánto se vuelve a mirar el stream cuando no hay registros nuevos.
    internal TimeSpan Intervalo { get; set; } = TimeSpan.FromSeconds(30);

    // Cada cuánto se borran los eventId procesados viejos (> retención del stream).
    internal TimeSpan EdadMaximaProcesados { get; set; } = TimeSpan.FromHours(36);

    internal Func<TimeSpan, CancellationToken, Task> Dormir { get; set; } = Task.Delay;

    private DateTimeOffset ultimaPoda = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!lector.Habilitada)
        {
            log.LogWarning("KINESIS_PRECIOS_STREAM vacío: el lector de precios no arranca");
            return;
        }

        log.LogInformation("LectorPrecios escuchando cep-precios");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool huboRegistros = await UnaVueltaAsync(stoppingToken);
                await PodarProcesadosAsync(stoppingToken);
                if (!huboRegistros)
                    await Dormir(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                log.LogError(e, "Error leyendo el stream de precios; se reintenta en 5 s");
                await Dormir(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    // Una pasada por todos los shards. Devuelve true si procesó algún registro.
    // Pública para probar el bucle con un ILectorStream en memoria.
    public async Task<bool> UnaVueltaAsync(CancellationToken ct)
    {
        bool hubo = false;
        foreach (string shard in await lector.ShardsAsync(ct))
        {
            string? checkpoint = await LeerCheckpointAsync(shard, ct);
            var registros = await lector.LeerAsync(shard, checkpoint, ct);
            if (registros.Count == 0) continue;
            hubo = true;

            string? ultimo = null;
            foreach (RegistroStream reg in registros)
            {
                await ManejarAsync(reg, ct);
                ultimo = reg.NumeroSecuencia;
            }
            if (ultimo is not null)
                await GuardarCheckpointAsync(shard, ultimo, ct);
        }
        return hubo;
    }

    // Procesa un registro hasta dejarlo resuelto (guardado, ignorado o en la DLQ).
    // Pública para probar las tres salidas sin Kinesis. Si ni la DLQ responde,
    // propaga: la vuelta no guarda el checkpoint y el registro se reintenta luego.
    public async Task ManejarAsync(RegistroStream reg, CancellationToken ct)
    {
        using var _ = log.BeginScope("Shard={Shard} Seq={Seq}", reg.ShardId, reg.NumeroSecuencia);
        for (int intento = 0; ; intento++)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var procesador = scope.ServiceProvider.GetRequiredService<IProcesadorPrecio>();
                string eventId = await procesador.ProcesarAsync(reg.Cuerpo, ct);
                log.LogDebug("Resultado=ok eventId={EventId} Seq={Seq}", eventId, reg.NumeroSecuencia);
                return;
            }
            catch (ErrorDefinitivoException e)
            {
                log.LogWarning(e, "Resultado=dlq Seq={Seq} razón={Razon}", reg.NumeroSecuencia, e.Razon);
                await buzon.EnviarAsync(reg.Cuerpo, Diagnostico("definitivo", e.Razon, e.InnerException ?? e), ct);
                return;
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                if (intento >= EsperasReintento.Count)
                {
                    var causa = e.GetBaseException();
                    log.LogWarning(e, "Resultado=dlq-agotado Seq={Seq} tras {Intentos} reintentos: {Mensaje}",
                        reg.NumeroSecuencia, EsperasReintento.Count, causa.Message);
                    await buzon.EnviarAsync(reg.Cuerpo,
                        Diagnostico("recuperable-agotado",
                            $"{EsperasReintento.Count} reintentos sin éxito; el último: {causa.Message}", causa), ct);
                    return;
                }

                log.LogWarning(e, "Resultado=reintento Seq={Seq} intento={Intento}: {Mensaje}",
                    reg.NumeroSecuencia, intento + 1, e.Message);
                await Dormir(EsperasReintento[intento], ct);
            }
        }
    }

    private async Task<string?> LeerCheckpointAsync(string shard, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var cp = await db.CheckpointsStream.FindAsync([shard], ct);
        return cp?.NumeroSecuencia;
    }

    private async Task GuardarCheckpointAsync(string shard, string secuencia, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var cp = await db.CheckpointsStream.FindAsync([shard], ct);
        if (cp is null)
        {
            cp = new CheckpointStream { StreamShard = shard, NumeroSecuencia = secuencia };
            db.CheckpointsStream.Add(cp);
        }
        else
        {
            cp.NumeroSecuencia = secuencia;
        }
        cp.ActualizadoEn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task PodarProcesadosAsync(CancellationToken ct)
    {
        DateTimeOffset ahora = DateTimeOffset.UtcNow;
        if (ahora - ultimaPoda < TimeSpan.FromHours(1)) return;
        ultimaPoda = ahora;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        DateTimeOffset corte = ahora - EdadMaximaProcesados;
        int borrados = await db.EventosPrecioProcesados
            .Where(e => e.ProcesadoEn < corte)
            .ExecuteDeleteAsync(ct);
        if (borrados > 0)
            log.LogDebug("Podados {Borrados} eventId procesados de más de {Horas} h",
                borrados, EdadMaximaProcesados.TotalHours);
    }

    private static Dictionary<string, string> Diagnostico(string tipo, string razon, Exception causa) => new()
    {
        ["error-type"] = tipo,
        ["error-reason"] = razon,
        ["error-exception"] = causa.GetType().Name,
        ["failed-by"] = "product-service (lector de precios)",
        ["failed-at"] = DateTimeOffset.UtcNow.ToString("O")
    };
}
