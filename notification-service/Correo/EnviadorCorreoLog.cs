namespace Notification_Service.Correo;

// No envia nada: escribe el correo en el log. Es el modo de desarrollo y de
// pruebas, para ver el flujo completo sin una cuenta de Gmail (CORREO_MODO=log).
public sealed class EnviadorCorreoLog(ILogger<EnviadorCorreoLog> log) : IEnviadorCorreo
{
    public Task EnviarAsync(CorreoSalida correo, CancellationToken ct)
    {
        log.LogInformation("CORREO (log) -> {Para} | {Asunto}\n{Texto}",
            correo.Para, correo.Asunto, correo.Texto);
        return Task.CompletedTask;
    }
}
