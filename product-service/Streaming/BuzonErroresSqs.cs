using Amazon.SQS;
using Amazon.SQS.Model;

namespace Product_Service.Streaming;

/// <summary>
/// <see cref="IBuzonErrores"/> sobre la cola SQS cep-precios-dlq (declarada en
/// topologia.json, creada por Terraform). Manda el registro con atributos de
/// diagnóstico, como la DLQ de ofertas, para que se vea en el panel por qué falló.
/// </summary>
public sealed class BuzonErroresSqs : IBuzonErrores
{
    private readonly IAmazonSQS sqs;
    private readonly string dlqUrl;
    private readonly ILogger<BuzonErroresSqs> log;

    public BuzonErroresSqs(IAmazonSQS sqs, IConfiguration config, ILogger<BuzonErroresSqs> log)
    {
        this.sqs = sqs;
        this.log = log;
        dlqUrl = config["SQS_PRECIOS_DLQ_URL"] ?? "";
    }

    public bool Habilitado => !string.IsNullOrWhiteSpace(dlqUrl);

    public async Task EnviarAsync(string cuerpo, IReadOnlyDictionary<string, string> diagnostico, CancellationToken ct)
    {
        if (!Habilitado)
        {
            log.LogError("No hay SQS_PRECIOS_DLQ_URL; el registro de precio que falló no se puede " +
                "apartar y se descarta: {Cuerpo}", Recortar(cuerpo));
            return;
        }

        await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = dlqUrl,
            MessageBody = cuerpo,
            MessageAttributes = diagnostico.ToDictionary(
                kv => kv.Key,
                kv => new MessageAttributeValue { DataType = "String", StringValue = kv.Value })
        }, ct);
    }

    private static string Recortar(string s) => s.Length <= 500 ? s : s[..500];
}
