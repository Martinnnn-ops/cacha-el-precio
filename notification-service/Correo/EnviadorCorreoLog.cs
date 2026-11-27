namespace Notification_Service.Correo;

// No envia nada: escribe el correo en el log. Es el modo de desarrollo y de
// pruebas, para ver el flujo completo sin una cuenta de Gmail (CORREO_MODO=log).
public sealed class EnviadorCorreoLog(ILogger<EnviadorCorreoLog> log) : IEnviadorCorreo
{
    public string Modo => "log";

    public Task EnviarAsync(CorreoSalida correo, CancellationToken ct)
    {
        // Modo de desarrollo: el texto se muestra para ver el correo armado, pero
        // la dirección va enmascarada igual que en producción.
        log.LogInformation("CORREO (log) -> {Para} | {Asunto}\n{Texto}",
            Observabilidad.LogsJson.Enmascarar(correo.Para), correo.Asunto, correo.Texto);
        return Task.CompletedTask;
    }
}
