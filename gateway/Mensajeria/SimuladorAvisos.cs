using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CachaElPrecio.Gateway.Mensajeria;

// «Simular aviso» del panel: arma un cambio de oferta con los datos reales de un
// producto y lo publica en cep-cambios-oferta, con el mismo sobre y los mismos
// atributos que usa product-service (PublicadorEventosSns). Desde ahi el
// mensaje recorre el flujo 2 de verdad: las reglas del topico, las colas,
// notification-service y la bandeja. No cambia ningun precio del catalogo.
//
// Se distingue de un cambio real por source = "panel-admin".
public sealed record SimularAvisoSolicitud(int? ProductId, string? Tipo, int? Descuento);

public sealed class SimuladorAvisos(HttpClient productos, IAdministradorMensajeria mensajeria, TopologiaDeclarada topologia)
{
    public const string Topico = "cambios-oferta";
    private static readonly string[] Tipos = ["precio.bajo", "oferta.agotada", "oferta.repuesta"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<object> SimularAsync(SimularAvisoSolicitud s, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        if (s.ProductId is null or < 1)
            errores["productId"] = ["Indica el id de un producto del catálogo."];
        if (s.Tipo is null || !Tipos.Contains(s.Tipo))
            errores["tipo"] = [$"Usa uno de estos tipos: {string.Join(", ", Tipos)}."];
        if (s.Tipo == "precio.bajo" && s.Descuento is null or < 1 or > 90)
            errores["descuento"] = ["El descuento va de 1 a 90 (%)."];
        if (errores.Count > 0) throw new SolicitudInvalidaException(errores);

        var producto = await LeerProductoAsync(s.ProductId!.Value, ct);
        var oferta = producto["offers"]?.AsArray()
            .Select(o => o!)
            .OrderByDescending(o => o["active"]?.GetValue<bool>() ?? false)
            .FirstOrDefault()
            ?? throw new ConflictoException("Ese producto no tiene ofertas de ninguna tienda: no hay precio que avisar.");

        int precio = oferta["price"]?.GetValue<int>() ?? 0;
        var datos = new JsonObject
        {
            ["productId"] = s.ProductId,
            ["slug"] = producto["slug"]?.GetValue<string>(),
            ["name"] = producto["name"]?.GetValue<string>(),
            ["image"] = oferta["image"]?.GetValue<string>() ?? producto["image"]?.GetValue<string>(),
            ["store"] = oferta["store"]?.GetValue<string>(),
            ["url"] = oferta["url"]?.GetValue<string>(),
        };
        var atributos = new List<AtributoMensaje> { new("type", "String", s.Tipo!) };
        switch (s.Tipo)
        {
            case "precio.bajo":
                int nuevo = (int)Math.Round(precio * (100 - s.Descuento!.Value) / 100.0 / 10) * 10;
                datos["previousPrice"] = precio;
                datos["newPrice"] = nuevo;
                datos["discountPct"] = s.Descuento;
                atributos.Add(new("discountPct", "Number", s.Descuento.Value.ToString()));
                break;
            case "oferta.agotada":
                datos["lastPrice"] = precio;
                break;
            default:
                datos["price"] = precio;
                break;
        }

        var eventId = Guid.NewGuid();
        var sobre = new JsonObject
        {
            ["eventId"] = eventId.ToString(),
            ["type"] = s.Tipo,
            ["version"] = 1,
            ["source"] = "panel-admin",
            ["occurredAt"] = DateTimeOffset.UtcNow,
            ["data"] = datos
        };

        string topico = topologia.Completo(Topico);
        string messageId = await mensajeria.PublicarAsync(topico, sobre.ToJsonString(Json), atributos, ct);
        return new
        {
            eventId,
            messageId,
            topico,
            atributos = atributos.ToDictionary(a => a.Nombre, a => a.Valor),
            evento = sobre
        };
    }

    private async Task<JsonNode> LeerProductoAsync(int id, CancellationToken ct)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, $"api/products/{id}");
        pedido.Headers.TryAddWithoutValidation("Version", "1.0");
        try
        {
            using var r = await productos.SendAsync(pedido, ct);
            if (r.StatusCode == HttpStatusCode.NotFound)
                throw new RecursoNoEncontradoException("el producto", id.ToString());
            r.EnsureSuccessStatusCode();
            return JsonNode.Parse(await r.Content.ReadAsStringAsync(ct))
                ?? throw new ErrorProveedorException("Product Service respondió vacío.", false);
        }
        catch (HttpRequestException e)
        {
            throw new ErrorProveedorException("Product Service no respondió.", false, e);
        }
    }

    public static void Registrar(IServiceCollection services, IConfiguration config)
    {
        string url = config["PRODUCT_URL"] ?? "http://localhost:8081";
        services.AddHttpClient<SimuladorAvisos>(http =>
        {
            http.BaseAddress = new Uri(url.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(10);
        });
    }
}
