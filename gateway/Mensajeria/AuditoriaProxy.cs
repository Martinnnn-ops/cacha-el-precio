namespace CachaElPrecio.Gateway.Mensajeria;

// Los cambios auditados viven en product-service (ruta /internal/auditoria,
// solo en la red del compose). El gateway los publica en
// /api/admin/mensajeria/auditoria con el mismo grupo admin que el resto del
// panel, y los pasa tal cual. Mismo patron que BandejaCorreosProxy.
public sealed class AuditoriaProxy(HttpClient client)
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
                mensaje = "Product Service no respondió."
            }, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    public static void Registrar(IServiceCollection services, IConfiguration config)
    {
        string url = config["PRODUCT_URL"] ?? "http://localhost:8081";
        services.AddHttpClient<AuditoriaProxy>(http =>
        {
            http.BaseAddress = new Uri(url.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(10);
        });
    }

    public static void Mapear(RouteGroupBuilder admin)
    {
        admin.MapGet("/auditoria", (int? limite, bool? soloSospechosos, string? correlationId,
            AuditoriaProxy p, CancellationToken ct) =>
        {
            var q = $"internal/auditoria?limite={limite ?? 50}";
            if (soloSospechosos == true) q += "&soloSospechosos=true";
            if (!string.IsNullOrWhiteSpace(correlationId)) q += $"&correlationId={Uri.EscapeDataString(correlationId)}";
            return p.ReenviarAsync(q, ct);
        });
    }
}
