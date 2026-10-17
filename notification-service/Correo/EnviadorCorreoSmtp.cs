using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Notification_Service.Exceptions;

namespace Notification_Service.Correo;

// Gmail por SMTP con MailKit: smtp.gmail.com:587, STARTTLS, contrasena de
// aplicacion. La configuracion (SMTP_USUARIO, SMTP_CLAVE) va solo en el .env del
// servidor; en el repo queda vacia. Se valida al arrancar (ver Program.cs).
public sealed class EnviadorCorreoSmtp(IConfiguration config, ILogger<EnviadorCorreoSmtp> log)
    : IEnviadorCorreo
{
    private const string Host = "smtp.gmail.com";
    private const int Puerto = 587;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly string usuario = config["SMTP_USUARIO"] ?? "";
    private readonly string clave = config["SMTP_CLAVE"] ?? "";

    public async Task EnviarAsync(CorreoSalida correo, CancellationToken ct)
    {
        MimeMessage mensaje;
        try
        {
            mensaje = new MimeMessage();
            mensaje.From.Add(MailboxAddress.Parse(usuario));
            mensaje.To.Add(MailboxAddress.Parse(correo.Para));
            mensaje.Subject = correo.Asunto;
            mensaje.Body = new BodyBuilder { HtmlBody = correo.Html, TextBody = correo.Texto }.ToMessageBody();
        }
        catch (ParseException e)
        {
            // Direccion de destino con formato invalido: no se arregla reintentando.
            throw new ErrorDefinitivoException($"direccion invalida: {correo.Para}", e);
        }

        using var cliente = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
        try
        {
            await cliente.ConnectAsync(Host, Puerto, SecureSocketOptions.StartTls, ct);
            await cliente.AuthenticateAsync(usuario, clave, ct);
            await cliente.SendAsync(mensaje, ct);
            await cliente.DisconnectAsync(true, ct);
        }
        catch (SmtpCommandException e) when (
            e.StatusCode is SmtpStatusCode.MailboxUnavailable or SmtpStatusCode.MailboxNameNotAllowed)
        {
            // 550 / 553: el servidor rechaza la direccion. Definitivo.
            throw new ErrorDefinitivoException($"correo rechazado ({(int)e.StatusCode}): {correo.Para}", e);
        }
        catch (Exception e) when (e is not ErrorDefinitivoException)
        {
            // Timeout, conexion caida, 4xx temporal: recuperable. Se propaga para
            // que el mensaje no se borre y SQS lo reintente.
            log.LogWarning(e, "Fallo recuperable enviando a {Para}", correo.Para);
            throw;
        }
    }
}
