using Product_Service.Exceptions;

namespace Product_Service.Mensajeria;

// El bucle comun de los consumidores SQS de product-service (ofertas y
// auditoria). Por cada mensaje:
//   exito         -> se borra de la cola (el ACK);
//   definitivo    -> va a la DLQ con atributos de diagnostico y se borra;
//   recuperable   -> no se borra, reaparece tras la visibilidad y SQS reintenta;
//                    en el ultimo intento (maxReceiveCount) va a la DLQ con
//                    diagnostico "recuperable-agotado".
// Si la cola no esta configurada (URL vacia), no arranca y el servicio sigue
// atendiendo HTTP. Cada consumidor solo dice QUE hacer con el cuerpo.
public abstract class ConsumidorSqs(
    IColaMensajes cola,
    IServiceScopeFactory scopes,
    ILogger log,
    string nombreCola) : BackgroundService
{
    /// <summary>Procesa un cuerpo y devuelve su eventId. Lanza ErrorDefinitivoException si no sirve.</summary>
    protected abstract Task<string> ProcesarAsync(IServiceProvider servicios, string cuerpo, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!cola.Habilitada)
        {
            log.LogWarning("La URL de {Cola} viene vacia: su consumidor no arranca", nombreCola);
            return;
        }

        log.LogInformation("Consumidor escuchando {Cola}", nombreCola);
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
                log.LogError(e, "Error leyendo {Cola}; se reintenta en 5 s", nombreCola);
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
            string eventId = await ProcesarAsync(scope.ServiceProvider, mensaje.Cuerpo, ct);
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
                // La causa util esta ADENTRO: EF envuelve el error de Npgsql en un
                // "transient failure" generico (visto en la EC2 el 18-10).
                var causa = e.GetBaseException();
                await cola.EnviarADlqAsync(mensaje, Diagnostico(mensaje, "recuperable-agotado",
                    $"{m} intentos sin exito; el ultimo: {causa.Message}", causa), ct);
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
