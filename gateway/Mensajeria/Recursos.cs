using System.Text.Json.Nodes;

namespace CachaElPrecio.Gateway.Mensajeria;

// Lo que el componente de administracion devuelve al consultar. Son vistas de
// lectura: no exponen el SDK de AWS ni sus tipos, solo lo que una persona
// necesita para entender el estado de la mensajeria.

// Una cola SQS (normal o DLQ) con su configuracion y cuantos mensajes tiene.
public sealed record ColaInfo(
    string Nombre,
    string? Url,
    string? Arn,
    int VisibilidadSegundos,
    int RetencionSegundos,
    int EsperaSegundos,
    int MensajesVisibles,
    int MensajesEnVuelo,
    int MensajesDemorados,
    ReglaDlq? Dlq,
    IReadOnlyList<string> EsDlqDe,
    bool DeclaradaEnTopologia,
    string? Descripcion);

// La regla de mensajes muertos de una cola: a que DLQ van y tras cuantos intentos.
public sealed record ReglaDlq(string Cola, int MaxIntentos);

// Un topico SNS, con sus reglas de distribucion (suscripciones y filtros).
public sealed record TopicoInfo(
    string Nombre,
    string? Arn,
    bool DeclaradoEnTopologia,
    string? Descripcion,
    IReadOnlyList<SuscripcionInfo> Suscripciones);

// Una regla de distribucion: topico -> cola, con su filtro (null = recibe todo).
public sealed record SuscripcionInfo(
    string Topico,
    string Cola,
    string? Arn,
    bool EntregaCruda,
    JsonNode? Filtro,
    bool DeclaradaEnTopologia);

// Un mensaje que cayo en una DLQ, con los atributos de diagnostico que dejo el
// consumidor (error-type, error-reason, failed-by, ...). Se lee sin borrarlo.
public sealed record MensajeDlq(
    string MessageId,
    string Cuerpo,
    IReadOnlyDictionary<string, string> Diagnostico,
    int VecesRecibido,
    DateTimeOffset? EnviadoEn);

// Resultado de devolver los mensajes de una DLQ a su cola de origen.
public sealed record ResultadoReproceso(
    string Dlq,
    string Destino,
    string Metodo,
    int? MensajesMovidos,
    string? TareaId);
