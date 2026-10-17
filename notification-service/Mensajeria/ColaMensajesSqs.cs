using Amazon.SQS;
using Amazon.SQS.Model;

namespace Notification_Service.Mensajeria;

// Implementacion de IColaMensajes sobre Amazon SQS. Lee de cep-notificaciones y
// manda los definitivos a cep-notificaciones-dlq.
public sealed class ColaMensajesSqs : IColaMensajes
{
    private readonly IAmazonSQS sqs;
    private readonly string colaUrl;
    private readonly string dlqUrl;
    private readonly ILogger<ColaMensajesSqs> log;

    public ColaMensajesSqs(IAmazonSQS sqs, IConfiguration config, ILogger<ColaMensajesSqs> log)
    {
        this.sqs = sqs;
        this.log = log;
        colaUrl = config["SQS_NOTIFICACIONES_URL"] ?? "";
        dlqUrl = config["SQS_NOTIFICACIONES_DLQ_URL"] ?? "";
    }

    public bool Habilitada => !string.IsNullOrWhiteSpace(colaUrl);

    public async Task<IReadOnlyList<MensajeCola>> RecibirAsync(CancellationToken ct)
    {
        var respuesta = await sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = colaUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 20,
            MessageSystemAttributeNames = ["ApproximateReceiveCount"]
        }, ct);

        return respuesta.Messages?.Select(m => new MensajeCola(
            m.MessageId,
            m.Body,
            m.ReceiptHandle,
            int.TryParse(Atributo(m, "ApproximateReceiveCount"), out var n) ? n : 0)).ToList() ?? [];
    }

    public Task BorrarAsync(MensajeCola mensaje, CancellationToken ct) =>
        sqs.DeleteMessageAsync(colaUrl, mensaje.ReceiptHandle, ct);

    public async Task EnviarADlqAsync(MensajeCola mensaje,
        IReadOnlyDictionary<string, string> diagnostico, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dlqUrl))
        {
            log.LogError("No hay SQS_NOTIFICACIONES_DLQ_URL; el mensaje {MessageId} no se puede " +
                "mandar a la DLQ y se deja en la cola para revision manual", mensaje.MessageId);
            return;
        }

        await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = dlqUrl,
            MessageBody = mensaje.Cuerpo,
            MessageAttributes = diagnostico.ToDictionary(
                kv => kv.Key,
                kv => new MessageAttributeValue { DataType = "String", StringValue = kv.Value })
        }, ct);

        await BorrarAsync(mensaje, ct);
    }

    private static string? Atributo(Message m, string nombre) =>
        m.Attributes is not null && m.Attributes.TryGetValue(nombre, out var v) ? v : null;
}
