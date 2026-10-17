using Notification_Service.Exceptions;
using Notification_Service.Mensajeria;
using Notification_Service.Service;

namespace Notification_Service.Consumidor;

// Lee cep-notificaciones en un bucle de long polling. Por cada mensaje:
//   exito         -> se borra de la cola (el ACK);
//   definitivo    -> va a la DLQ con atributos de diagnostico y se borra;
//   recuperable   -> no se borra, reaparece a los 60 s y SQS reintenta.
// Si la cola no esta configurada (URL vacia), no arranca y el servicio sigue
// sirviendo /health, para poder programar en local sin AWS.
public sealed class ConsumidorNotificaciones(
    IColaMensajes cola,
    IServiceScopeFactory scopes,
    ILogger<ConsumidorNotificaciones> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!cola.Habilitada)
        {
            log.LogWarning("SQS_NOTIFICACIONES_URL vacia: el consumidor no arranca");
            return;
        }

        log.LogInformation("ConsumidorNotificaciones escuchando cep-notificaciones");
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
                // Fallo al recibir (SQS caido, credenciales): espera y reintenta.
                log.LogError(e, "Error leyendo la cola; se reintenta en 5 s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ManejarAsync(MensajeCola mensaje, CancellationToken ct)
    {
        using var _ = log.BeginScope("MessageId={MessageId} recibidos={Recibidos}",
            mensaje.MessageId, mensaje.ApproximateReceiveCount);
        try
        {
            using var scope = scopes.CreateScope();
            var servicio = scope.ServiceProvider.GetRequiredService<ServicioNotificaciones>();
            await servicio.ProcesarAsync(mensaje.Cuerpo, mensaje.ApproximateReceiveCount, ct);
            await cola.BorrarAsync(mensaje, ct);
        }
        catch (ErrorDefinitivoException e)
        {
            log.LogWarning(e, "Error definitivo: {Razon}. A la DLQ", e.Razon);
            await cola.EnviarADlqAsync(mensaje, Diagnostico(mensaje, e), ct);
        }
        catch (Exception e)
        {
            // Recuperable: no se borra. Reaparece y, al 5.o intento, SQS lo mueve
            // a la DLQ por si solo (maxReceiveCount en topologia.json).
            log.LogError(e, "Error recuperable; el mensaje no se borra y se reintenta");
        }
    }

    private static Dictionary<string, string> Diagnostico(MensajeCola mensaje, ErrorDefinitivoException e) => new()
    {
        ["error-type"] = "definitivo",
        ["error-reason"] = e.Razon,
        ["error-exception"] = (e.InnerException ?? e).GetType().Name,
        ["failed-by"] = "notification-service",
        ["failed-at"] = DateTimeOffset.UtcNow.ToString("O"),
        ["original-message-id"] = mensaje.MessageId
    };
}
