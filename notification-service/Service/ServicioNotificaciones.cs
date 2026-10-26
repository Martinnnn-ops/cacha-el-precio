using System.Text.Json;
using Notification_Service.Correo;
using Notification_Service.Exceptions;
using Notification_Service.Models.Eventos;
using Notification_Service.Repository;
using Notification_Service.Seguidores;

namespace Notification_Service.Service;

// El trabajo de un mensaje: validar el sobre, buscar a quienes siguen la prenda,
// resolver sus correos, y mandar a cada uno sin repetir. Lanza
// ErrorDefinitivoException cuando el mensaje no sirve (va a la DLQ); cualquier
// otra excepcion es recuperable y hace que el mensaje no se borre.
public sealed class ServicioNotificaciones(
    ISeguidoresClient seguidores,
    IDirectorioCorreos directorio,
    INotificacionesRepository repositorio,
    IBandejaCorreos bandeja,
    IEnviadorCorreo enviador,
    IConfiguration config,
    ILogger<ServicioNotificaciones> log)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> TiposValidos = ["precio.bajo", "oferta.agotada", "oferta.repuesta"];
    private readonly string sitioUrl = (config["SITIO_URL"] ?? "https://www.cacha-el-precio.com").TrimEnd('/');

    public async Task ProcesarAsync(string cuerpo, int intento, CancellationToken ct)
    {
        SobreEvento sobre = Leer(cuerpo);
        DatosCambioOferta datos = LeerDatos(sobre);

        using var _ = log.BeginScope("eventId={EventId} intento={Intento}", sobre.EventId, intento);

        var subs = await seguidores.ObtenerSeguidoresAsync(datos.ProductId, ct);
        if (subs.Count == 0)
        {
            log.LogInformation("Nadie sigue el producto {ProductId}; no hay a quien avisar", datos.ProductId);
            return;
        }

        var plantilla = PlantillaCorreo.Construir(sobre.Type, datos, sitioUrl);

        foreach (var sub in subs)
        {
            if (await repositorio.YaEnviadoAsync(sobre.EventId, sub, ct))
            {
                log.LogInformation("Ya se le habia avisado a {Sub}; se salta", sub);
                continue;
            }

            string? correo = await directorio.ObtenerCorreoAsync(sub, ct);
            if (correo is null) continue; // sin email verificado: ya se registro el motivo

            try
            {
                await enviador.EnviarAsync(plantilla with { Para = correo }, ct);
            }
            catch (ErrorDefinitivoException e)
            {
                // Direccion rechazada por el servidor: se salta a esta persona, no
                // se manda todo el evento a la DLQ (bloquearia a los demas).
                log.LogWarning("Se salta a {Sub}: {Razon}", sub, e.Razon);
                continue;
            }

            await repositorio.MarcarEnviadoAsync(sobre.EventId, sub, sobre.Type, ct);
            log.LogInformation("Aviso enviado a {Sub}", sub);
            await GuardarEnBandejaAsync(sobre, plantilla with { Para = correo }, ct);
        }
    }

    // La copia para el panel es un extra: si falla, el aviso ya salio y quedo
    // marcado, asi que no se reintenta el mensaje por esto.
    private async Task GuardarEnBandejaAsync(SobreEvento sobre, CorreoSalida correo, CancellationToken ct)
    {
        try
        {
            await bandeja.GuardarAsync(sobre.EventId, sobre.Type, enviador.Modo, correo, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogWarning(e, "No se pudo guardar la copia del correo en la bandeja");
        }
    }

    private static SobreEvento Leer(string cuerpo)
    {
        SobreEvento? sobre;
        try
        {
            sobre = JsonSerializer.Deserialize<SobreEvento>(cuerpo, Json);
        }
        catch (JsonException e)
        {
            throw new ErrorDefinitivoException("JSON del sobre invalido", e);
        }
        if (sobre is null)
            throw new ErrorDefinitivoException("el cuerpo del mensaje es nulo");
        if (!TiposValidos.Contains(sobre.Type))
            throw new ErrorDefinitivoException($"type desconocido: {sobre.Type}");
        if (sobre.Version != 1)
            throw new ErrorDefinitivoException($"version no soportada: {sobre.Version}");
        return sobre;
    }

    private static DatosCambioOferta LeerDatos(SobreEvento sobre)
    {
        DatosCambioOferta? datos;
        try
        {
            datos = sobre.Data.Deserialize<DatosCambioOferta>(Json);
        }
        catch (JsonException e)
        {
            throw new ErrorDefinitivoException("`data` del sobre invalido", e);
        }
        if (datos is null || datos.ProductId <= 0)
            throw new ErrorDefinitivoException("`data.productId` ausente o invalido");
        return datos;
    }
}
