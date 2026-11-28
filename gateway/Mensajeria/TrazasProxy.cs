using System.Text.Json;
using System.Text.Json.Nodes;

namespace CachaElPrecio.Gateway.Mensajeria;

// «Seguir un barrido» (PLAN-EFT §3, paso 4). Junta en una sola respuesta lo que
// cada servicio sabe de un correlationId:
//   scraper-service       -> el barrido: estado, URLs, guardados, enviados, duración
//   product-service       -> ofertas guardadas, precios por Kinesis, cambios
//                            publicados, auditados, sospechosos, duplicados
//   notification-service  -> cambios recibidos y avisos enviados
// más los cambios auditados de ese barrido. Si un servicio no responde, su
// parte viene vacía con un aviso: la línea de tiempo se dibuja igual.
public sealed class TrazasProxy(IHttpClientFactory fabrica, ILogger<TrazasProxy> log)
{
    public const string Scraper = "trazas-scraper";
    public const string Productos = "trazas-productos";
    public const string Notificaciones = "trazas-notificaciones";

    public async Task<IResult> RecientesAsync(int limite, CancellationToken ct)
    {
        var barridos = await LeerAsync(Scraper, $"barridos?limite={limite}", ct) as JsonArray ?? [];
        var conocidos = barridos.Select(b => b?["correlationId"]?.GetValue<string>()).ToHashSet();

        // Los que el scraper ya olvidó (se reinició) pero dejaron huella en la base.
        if (await LeerAsync(Productos, $"internal/trazas?prefijo=barrido-&limite={limite}", ct) is JsonArray deBase)
        {
            foreach (var t in deBase)
            {
                string? id = t?["correlationId"]?.GetValue<string>();
                if (id is null || conocidos.Contains(id)) continue;
                barridos.Add(new JsonObject
                {
                    ["correlationId"] = id,
                    ["tienda"] = id.Split('-').ElementAtOrDefault(1),
                    ["estado"] = "sin registro del scraper",
                    ["inicio"] = t!["primeraVez"]?.DeepClone(),
                });
            }
        }
        return Results.Ok(barridos);
    }

    public async Task<IResult> DeAsync(string correlationId, CancellationToken ct)
    {
        string id = Uri.EscapeDataString(correlationId);
        var scraper = LeerAsync(Scraper, $"barridos/{id}", ct);
        var productos = LeerAsync(Productos, $"internal/trazas/{id}", ct);
        var avisos = LeerAsync(Notificaciones, $"internal/trazas/{id}", ct);
        var auditoria = LeerAsync(Productos, $"internal/auditoria?limite=50&correlationId={id}", ct);
        await Task.WhenAll(scraper, productos, avisos, auditoria);

        var etapas = new JsonArray();
        foreach (var lista in new[] { await productos, await avisos })
            if (lista is JsonArray a)
                foreach (var e in a) etapas.Add(e?.DeepClone());

        return Results.Ok(new JsonObject
        {
            ["correlationId"] = correlationId,
            ["scraper"] = (await scraper)?.DeepClone(),
            ["etapas"] = etapas,
            ["auditoria"] = (await auditoria)?["eventos"]?.DeepClone() ?? new JsonArray(),
            ["faltan"] = new JsonArray(
                new[] { (await scraper, "scraper-service"), (await productos, "product-service"), (await avisos, "notification-service") }
                    .Where(x => x.Item1 is null).Select(x => (JsonNode)JsonValue.Create(x.Item2)!).ToArray())
        });
    }

    // null si el servicio no respondió o respondió con error (404 = no sabe nada de ese id).
    private async Task<JsonNode?> LeerAsync(string cliente, string ruta, CancellationToken ct)
    {
        try
        {
            using var r = await fabrica.CreateClient(cliente).GetAsync(ruta, ct);
            if (!r.IsSuccessStatusCode) return null;
            return JsonNode.Parse(await r.Content.ReadAsStringAsync(ct));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            log.LogWarning("Trazas: {Cliente} no respondió a {Ruta}: {Mensaje}", cliente, ruta, e.Message);
            return null;
        }
    }

    public static void Registrar(IServiceCollection services, IConfiguration config)
    {
        void Cliente(string nombre, string? url, string porDefecto) =>
            services.AddHttpClient(nombre, http =>
            {
                http.BaseAddress = new Uri((url ?? porDefecto).TrimEnd('/') + "/");
                http.Timeout = TimeSpan.FromSeconds(8);
            });
        Cliente(Scraper, config["SCRAPER_URL"], "http://localhost:8000");
        Cliente(Productos, config["PRODUCT_URL"], "http://localhost:8081");
        Cliente(Notificaciones, config["NOTIFICATION_URL"], "http://localhost:8082");
        services.AddSingleton<TrazasProxy>();
    }

    public static void Mapear(RouteGroupBuilder admin)
    {
        admin.MapGet("/trazas", (int? limite, TrazasProxy p, CancellationToken ct) =>
            p.RecientesAsync(Math.Clamp(limite ?? 20, 1, 50), ct));
        admin.MapGet("/trazas/{correlationId}", (string correlationId, TrazasProxy p, CancellationToken ct) =>
            p.DeAsync(correlationId, ct));
    }
}
