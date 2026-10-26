using Notification_Service.Repository;

namespace Notification_Service.Correo;

// La bandeja de correos generados, para el panel de mensajeria. Son rutas
// internas: el puerto 8082 solo escucha en la red del compose y en 127.0.0.1,
// y desde afuera se llega unicamente por el gateway, que exige el grupo admin
// (ver gateway/Mensajeria/BandejaCorreosProxy.cs). Aun asi, el destinatario
// sale enmascarado: el panel no necesita el correo completo para la demo.
public static class EndpointsBandeja
{
    public static void MapBandejaCorreos(this WebApplication app)
    {
        var rutas = app.MapGroup("/internal/correos");

        rutas.MapGet("/", async (int? limite, IBandejaCorreos bandeja, CancellationToken ct) =>
        {
            var correos = await bandeja.ListarAsync(Math.Clamp(limite ?? 20, 1, BandejaCorreos.Maximo), ct);
            return Results.Ok(correos.Select(c => new
            {
                c.Id,
                c.EventId,
                c.Tipo,
                Para = Enmascarar(c.Para),
                c.Asunto,
                c.Modo,
                c.GeneradoEn
            }));
        });

        rutas.MapGet("/{id:long}", async (long id, IBandejaCorreos bandeja, CancellationToken ct) =>
        {
            var c = await bandeja.ObtenerAsync(id, ct);
            return c is null
                ? Results.NotFound(new { estado = 404, error = "No existe", mensaje = $"No hay un correo con id {id}." })
                : Results.Ok(new
                {
                    c.Id,
                    c.EventId,
                    c.Tipo,
                    Para = Enmascarar(c.Para),
                    c.Asunto,
                    c.Html,
                    c.Texto,
                    c.Modo,
                    c.GeneradoEn
                });
        });

        rutas.MapGet("/ejemplo/{tipo}", (string tipo, IConfiguration config) =>
        {
            if (!PlantillaCorreo.Tipos.Contains(tipo))
                return Results.BadRequest(new
                {
                    estado = 400,
                    error = "Solicitud inválida",
                    mensaje = $"No hay plantilla para '{tipo}'. Usa {string.Join(", ", PlantillaCorreo.Tipos)}."
                });

            string sitioUrl = (config["SITIO_URL"] ?? "https://www.cacha-el-precio.com").TrimEnd('/');
            var c = PlantillaCorreo.Ejemplo(tipo, sitioUrl);
            return Results.Ok(new { Tipo = tipo, c.Para, c.Asunto, c.Html, c.Texto, Modo = "ejemplo" });
        });
    }

    // ana.perez@gmail.com -> a•••@gmail.com
    public static string Enmascarar(string correo)
    {
        int arroba = correo.IndexOf('@');
        return arroba <= 0 ? "•••" : correo[0] + "•••" + correo[arroba..];
    }
}
