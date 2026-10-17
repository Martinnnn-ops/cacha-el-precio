using System.Text.Json;
using System.Text.Json.Serialization;

namespace Notification_Service.Seguidores;

// Llama a GET {PRODUCT_URL}/internal/personal/seguidores/{productId}. Un fallo
// de red o un 5xx se propaga como recuperable: el mensaje no se borra y SQS lo
// reintenta.
public sealed class SeguidoresClient(HttpClient http) : ISeguidoresClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<string>> ObtenerSeguidoresAsync(int productId, CancellationToken ct)
    {
        using var respuesta = await http.GetAsync($"internal/personal/seguidores/{productId}", ct);
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaSeguidores>(Json, ct);
        return cuerpo?.Seguidores ?? [];
    }

    private sealed record RespuestaSeguidores(
        [property: JsonPropertyName("productId")] int ProductId,
        [property: JsonPropertyName("seguidores")] List<string> Seguidores);
}
