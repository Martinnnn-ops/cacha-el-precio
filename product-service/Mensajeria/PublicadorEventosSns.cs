using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Product_Service.Models.Eventos;

namespace Product_Service.Mensajeria;

/// <summary>
/// Publica los cambios de oferta en el tópico SNS <c>cep-cambios-oferta</c>.
/// El cuerpo es el sobre común; el filtro de la cola de notificaciones solo lee
/// los atributos del mensaje, así que el <c>type</c> va siempre y el
/// <c>discountPct</c> en <c>precio.bajo</c> (ver docs/MENSAJERIA.md).
/// </summary>
public sealed class PublicadorEventosSns(
    IAmazonSimpleNotificationService sns,
    IConfiguration config,
    ILogger<PublicadorEventosSns> logger) : IPublicadorEventos
{
    // Los campos del sobre van en camelCase y los nulos del data no se escriben.
    private static readonly JsonSerializerOptions JsonOpciones = new(JsonSerializerDefaults.Web);

    private readonly string? _topicArn = config["SNS_CAMBIOS_OFERTA_ARN"];

    public bool Habilitado => !string.IsNullOrWhiteSpace(_topicArn);

    public async Task PublicarCambioOfertaAsync(string tipo, DatosCambioOferta datos, CancellationToken ct = default)
    {
        if (!Habilitado)
        {
            return;
        }

        var sobre = new SobreEvento(
            EventId: Guid.NewGuid().ToString(),
            Type: tipo,
            Version: 1,
            Source: "product-service",
            OccurredAt: DateTimeOffset.UtcNow,
            Data: datos);

        var atributos = new Dictionary<string, MessageAttributeValue>
        {
            ["type"] = new() { DataType = "String", StringValue = tipo }
        };

        // Solo en precio.bajo: el filtro de cep-notificaciones exige baja ≥ 10%.
        if (tipo == TiposEvento.PrecioBajo && datos.DiscountPct is int pct)
        {
            atributos["discountPct"] = new() { DataType = "Number", StringValue = pct.ToString() };
        }

        var request = new PublishRequest
        {
            TopicArn = _topicArn,
            Message = JsonSerializer.Serialize(sobre, JsonOpciones),
            MessageAttributes = atributos
        };

        try
        {
            PublishResponse respuesta = await sns.PublishAsync(request, ct);
            logger.LogInformation(
                "Evento {Tipo} publicado para el producto {ProductId}. eventId={EventId} MessageId={MessageId}",
                tipo, datos.ProductId, sobre.EventId, respuesta.MessageId);
        }
        catch (Exception ex)
        {
            // Best-effort: el upsert ya se guardó. Se registra y se sigue; el
            // aviso de este cambio se pierde (ADR-031, sin outbox).
            logger.LogError(ex,
                "No se pudo publicar el evento {Tipo} del producto {ProductId}. eventId={EventId}",
                tipo, datos.ProductId, sobre.EventId);
        }
    }
}
