using Microsoft.Extensions.Logging.Console;

namespace Notification_Service.Observabilidad;

/// <summary>
/// Logs en JSON: una línea por evento, con campos con nombre, para poder
/// filtrarlos (por servicio, nivel o correlationId) en vez de leer texto suelto.
/// Es la mitad de los «logs avanzados» de la EFT (docs/PLAN-EFT.md §3); la otra
/// mitad es el correlationId, que viaja en los scopes y por eso se incluyen.
///
/// Regla: nunca correos, tokens ni cuerpos de mensaje en un log.
/// </summary>
public static class LogsJson
{
    public const string Servicio = "notification-service";

    public static WebApplicationBuilder UsarLogsJson(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        // Cada consulta SQL de EF salía como Information: cientos por barrido,
        // tapando lo que importa. Solo se ven si fallan.
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        builder.Logging.AddJsonConsole(o =>
        {
            o.IncludeScopes = true;
            o.UseUtcTimestamp = true;
            o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            o.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
        });
        return builder;
    }

    /// <summary>"martin@gmail.com" → "ma***@gmail.com": se sabe a qué dominio iba, no a quién.</summary>
    public static string Enmascarar(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo)) return "(vacío)";
        int arroba = correo.IndexOf('@');
        if (arroba <= 0) return "***";
        string usuario = correo[..arroba];
        return $"{usuario[..Math.Min(2, usuario.Length)]}***{correo[arroba..]}";
    }
}
