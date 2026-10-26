namespace CachaElPrecio.Gateway.Mensajeria;

// La bandeja de correos generados vive en notification-service (rutas
// /internal/correos, solo en la red del compose). El gateway la publica dentro
// de /api/admin/mensajeria/correos, con el mismo grupo admin que el resto del
// panel, y la pasa tal cual: no arma ni interpreta los correos.
public sealed class BandejaCorreosProxy(HttpClient client)
{
    public async Task<IResult> ReenviarAsync(string ruta, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage respuesta = await client.GetAsync(ruta, ct);
            string cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
            return Results.Content(cuerpo, "application/json", statusCode: (int)respuesta.StatusCode);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return Results.Json(new
            {
                estado = 502,
                error = "Servicio no disponible",
                mensaje = "Notification Service no respondió."
            }, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    public static void Registrar(IServiceCollection services, IConfiguration config)
    {
        string url = config["NOTIFICATION_URL"] ?? "http://localhost:8082";
        services.AddHttpClient<BandejaCorreosProxy>(http =>
        {
            http.BaseAddress = new Uri(url.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(10);
        });
    }

    public static void Mapear(RouteGroupBuilder admin)
    {
        admin.MapGet("/correos", (int? limite, BandejaCorreosProxy p, CancellationToken ct) =>
            p.ReenviarAsync($"internal/correos?limite={limite ?? 20}", ct));

        admin.MapGet("/correos/{id:long}", (long id, BandejaCorreosProxy p, CancellationToken ct) =>
            p.ReenviarAsync($"internal/correos/{id}", ct));

        admin.MapGet("/correos/ejemplo/{tipo}", (string tipo, BandejaCorreosProxy p, CancellationToken ct) =>
            p.ReenviarAsync($"internal/correos/ejemplo/{Uri.EscapeDataString(tipo)}", ct));
    }
}
