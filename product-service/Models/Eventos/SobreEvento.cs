using System.Text.Json.Serialization;

namespace Product_Service.Models.Eventos;

/// <summary>
/// El sobre común de la mensajería (ver docs/MENSAJERIA.md). Lo produce este
/// servicio para los cambios de oferta del flujo 2; los campos van en inglés,
/// igual que <c>ProductRequest</c>.
/// </summary>
public sealed record SobreEvento(
    string EventId,
    string Type,
    int Version,
    string Source,
    DateTimeOffset OccurredAt,
    object Data);

/// <summary>
/// El <c>data</c> de los tres cambios de oferta. Los campos comunes van
/// siempre; los de precio solo en el evento que corresponde, y los nulos no se
/// serializan (así <c>precio.bajo</c>, <c>oferta.agotada</c> y
/// <c>oferta.repuesta</c> salen con la forma exacta del contrato).
/// </summary>
public sealed record DatosCambioOferta
{
    public required int ProductId { get; init; }
    public required string Slug { get; init; }
    public required string Name { get; init; }
    public string? Image { get; init; }
    public required string Store { get; init; }
    public required string Url { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PreviousPrice { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NewPrice { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DiscountPct { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LastPrice { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Price { get; init; }
}

/// <summary>Los tres tipos de cambio de oferta que produce este servicio.</summary>
public static class TiposEvento
{
    public const string PrecioBajo = "precio.bajo";
    public const string OfertaAgotada = "oferta.agotada";
    public const string OfertaRepuesta = "oferta.repuesta";
}
