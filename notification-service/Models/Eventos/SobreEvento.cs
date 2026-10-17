using System.Text.Json;

namespace Notification_Service.Models.Eventos;

// El sobre comun de MENSAJERIA.md. El cuerpo del mensaje SQS ya es este objeto,
// sin envoltorio de SNS (rawMessageDelivery=true en topologia.json).
public sealed record SobreEvento(
    Guid EventId,
    string Type,
    int Version,
    string Source,
    DateTimeOffset OccurredAt,
    JsonElement Data);

// Union de los campos de `data` de los tres tipos de cambio de oferta. Los
// comunes van siempre; los de precio solo en el tipo que corresponde.
public sealed record DatosCambioOferta(
    int ProductId,
    string? Slug,
    string? Name,
    string? Image,
    string? Store,
    string? Url,
    int? PreviousPrice,
    int? NewPrice,
    int? DiscountPct,
    int? LastPrice,
    int? Price);
