using Product_Service.Exceptions;

namespace Product_Service.Mensajeria;

// Lee cep-ofertas-crudas (flujo 1 del EP3) en un bucle de long polling. Por
// cada mensaje:
//   exito         -> se borra de la cola (el ACK);
//   definitivo    -> va a la DLQ con atributos de diagnostico y se borra;
//   recuperable   -> no se borra, reaparece a los 60 s y SQS reintenta; en el
//                    ultimo intento (maxReceiveCount de la cola) va a la DLQ con
//                    diagnostico "recuperable-agotado".
// Si la cola no esta configurada (URL vacia), no arranca y el servicio sigue
// atendiendo HTTP como antes. Mismo patron que ConsumidorNotificaciones.
public sealed class ConsumidorOfertas(
    IColaMensajes cola,
    IServiceScopeFactory scopes,
    ILogger<ConsumidorOfertas> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!cola.Habilitada)
        {
            log.LogWarning("SQS_OFERTAS_CRUDAS_URL vacia: el consumidor de ofertas no arranca");
            return;
        }

        log.LogInformation("ConsumidorOfertas escuchando cep-ofertas-crudas");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var mensajes = await cola.RecibirAsync(stoppingToken);
                foreach (var mensaje in mensajes)
                    await ManejarAsync(mensaje, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                // Fallo al recibir (SQS caido, credenciales vencidas): espera y reintenta.
                log.LogError(e, "Error leyendo la cola de ofertas; se reintenta en 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    // Publico para probar las tres salidas (exito, definitivo, recuperable) sin SQS.
    public async Task ManejarAsync(MensajeCola mensaje, CancellationToken ct)
    {
        using var _ = log.BeginScope("MessageId={MessageId} recibidos={Recibidos}",
            mensaje.MessageId, mensaje.ApproximateReceiveCount);
        try
        {
            using var scope = scopes.CreateScope();
            var procesador = scope.ServiceProvider.GetRequiredService<ProcesadorOfertaCapturada>();
            string eventId = await procesador.ProcesarAsync(mensaje.Cuerpo, ct);
            await cola.BorrarAsync(mensaje, ct);
            log.LogInformation("Resultado=ok eventId={EventId} MessageId={MessageId} intento={Intento}",
                eventId, mensaje.MessageId, mensaje.ApproximateReceiveCount);
        }
        catch (ErrorDefinitivoException e)
        {
            log.LogWarning(e, "Resultado=dlq MessageId={MessageId} intento={Intento} razon={Razon}",
                mensaje.MessageId, mensaje.ApproximateReceiveCount, e.Razon);
            await cola.EnviarADlqAsync(mensaje,
                Diagnostico(mensaje, "definitivo", e.Razon, e.InnerException ?? e), ct);
        }
        // TaskCanceledException hereda de OperationCanceledException y es un timeout
        // de la base: tambien es recuperable. Solo se deja pasar si el servicio se
        // esta apagando de verdad.
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            // Recuperable (NpgsqlException, TimeoutException, TaskCanceledException...):
            // no se borra y reaparece tras la visibilidad (60 s). Un warning y no un
            // error: es el camino previsto.
            //
            // En el ULTIMO intento lo aparta el consumidor y no SQS: si lo moviera
            // SQS, llegaria a la DLQ sin ningun atributo y desde el panel no se
            // sabria por que fallo. Asi queda con la razon del ultimo error.
            int? max = await cola.MaxIntentosAsync(ct);
            if (max is int m && mensaje.ApproximateReceiveCount >= m)
            {
                log.LogWarning(e, "Resultado=dlq-agotado MessageId={MessageId} intento={Intento} de {Max}: {Mensaje}",
                    mensaje.MessageId, mensaje.ApproximateReceiveCount, m, e.Message);
                await cola.EnviarADlqAsync(mensaje, Diagnostico(mensaje, "recuperable-agotado",
                    $"{m} intentos sin exito; el ultimo: {e.Message}", e), ct);
                return;
            }

            log.LogWarning(e, "Resultado=reintento MessageId={MessageId} intento={Intento}: {Mensaje}",
                mensaje.MessageId, mensaje.ApproximateReceiveCount, e.Message);
        }
    }

    private static Dictionary<string, string> Diagnostico(
        MensajeCola mensaje, string tipo, string razon, Exception causa) => new()
        {
            ["error-type"] = tipo,
            ["error-reason"] = razon,
            ["error-exception"] = causa.GetType().Name,
            ["failed-by"] = "product-service",
            ["failed-at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["original-message-id"] = mensaje.MessageId
        };
}
