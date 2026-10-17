namespace Notification_Service.Correo;

// Un correo listo para salir: destinatario, asunto y los dos cuerpos.
public sealed record CorreoSalida(string Para, string Asunto, string Html, string Texto);

public interface IEnviadorCorreo
{
    // Envia el correo. Una direccion rechazada por el servidor (SMTP 550/553) es
    // un ErrorDefinitivoException; un fallo de conexion o timeout se propaga tal
    // cual (recuperable).
    Task EnviarAsync(CorreoSalida correo, CancellationToken ct);
}
