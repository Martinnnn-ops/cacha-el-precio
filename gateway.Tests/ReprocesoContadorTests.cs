using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using CachaElPrecio.Gateway.Mensajeria;
using Microsoft.Extensions.Logging.Abstractions;
using Sqs = Amazon.SQS.Model;

namespace CachaElPrecio.Gateway.Tests;

/// <summary>
/// El contador del redrive. La tarea nativa de SQS (StartMessageMoveTask)
/// informa cuantos movio con un numero APROXIMADO, que a veces termina en 0
/// aunque los haya movido todos, y el panel decia "0 movidos". Estos tests usan
/// una SQS falsa en memoria (el cliente del SDK con sus metodos reemplazados),
/// sin red ni credenciales, para reproducir ese caso a voluntad.
/// </summary>
public class ReprocesoContadorTests
{
    private const string Dlq = "cep-prueba-dlq";
    private const string Destino = "cep-prueba";

    [Fact]
    public async Task Si_la_tarea_mueve_todo_pero_informa_0_se_cuentan_los_que_habia()
    {
        var sqs = new SqsFalsa(enDlq: 3) { TareaMueve = true, TareaInforma = 0 };

        var r = await Administrador(sqs).ReprocesarAsync(Dlq, Destino, CancellationToken.None);

        Assert.Equal(3, sqs.EnDestino);
        Assert.Equal(3, r.MensajesMovidos);
    }

    [Fact]
    public async Task Si_la_tarea_no_mueve_nada_el_plan_b_cuenta_los_que_movio_el()
    {
        // El otro caso del 18-10: la tarea vio la DLQ vacia (contador atrasado)
        // y termino con 0 de 0; los mueve el plan B, uno por uno.
        var sqs = new SqsFalsa(enDlq: 2) { TareaMueve = false, TareaInforma = 0, ContadorDlqAtrasado = true };

        var r = await Administrador(sqs).ReprocesarAsync(Dlq, Destino, CancellationToken.None);

        Assert.Equal(2, sqs.EnDestino);
        Assert.Equal(2, r.MensajesMovidos);
    }

    [Fact]
    public async Task Si_la_tarea_informa_bien_se_usa_su_numero()
    {
        var sqs = new SqsFalsa(enDlq: 4) { TareaMueve = true, TareaInforma = 4 };

        var r = await Administrador(sqs).ReprocesarAsync(Dlq, Destino, CancellationToken.None);

        Assert.Equal("redrive", r.Metodo);
        Assert.Equal(4, r.MensajesMovidos);
    }

    [Fact]
    public async Task Con_la_dlq_vacia_informa_0()
    {
        var sqs = new SqsFalsa(enDlq: 0) { TareaMueve = true, TareaInforma = 0 };

        var r = await Administrador(sqs).ReprocesarAsync(Dlq, Destino, CancellationToken.None);

        Assert.Equal(0, r.MensajesMovidos);
    }

    // =====================================================================

    private static AdministradorMensajeriaAws Administrador(SqsFalsa sqs) =>
        new(sqs, new AmazonSimpleNotificationServiceClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1),
            "cep-", NullLogger<AdministradorMensajeriaAws>.Instance);

    /// <summary>
    /// Dos colas en memoria. Solo implementa lo que usa ReprocesarAsync; si el
    /// codigo llamara a otra operacion, el cliente base intentaria ir a AWS sin
    /// credenciales y el test fallaria, que es lo que se quiere.
    /// </summary>
    private sealed class SqsFalsa(int enDlq)
        : AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1)
    {
        private readonly Queue<Sqs.Message> dlq = new(Enumerable.Range(0, enDlq).Select(i => new Sqs.Message
        {
            MessageId = $"m-{i}",
            Body = $$"""{"eventId":"e{{i}}"}""",
            ReceiptHandle = $"r-{i}",
            // Llegaron antes del redrive: el plan B los mueve.
            Attributes = new() { ["SentTimestamp"] = "1" }
        }));

        public bool TareaMueve { get; init; }
        public int TareaInforma { get; init; }
        public bool ContadorDlqAtrasado { get; init; }
        public int EnDestino { get; private set; }

        private static string Url(string nombre) => $"https://sqs.us-east-1.amazonaws.com/000000000000/{nombre}";

        public override Task<Sqs.GetQueueUrlResponse> GetQueueUrlAsync(Sqs.GetQueueUrlRequest r, CancellationToken ct = default) =>
            Task.FromResult(new Sqs.GetQueueUrlResponse { QueueUrl = Url(r.QueueName) });

        public override Task<Sqs.GetQueueAttributesResponse> GetQueueAttributesAsync(
            Sqs.GetQueueAttributesRequest r, CancellationToken ct = default)
        {
            string nombre = r.QueueUrl.Split('/')[^1];
            return Task.FromResult(new Sqs.GetQueueAttributesResponse
            {
                Attributes = new()
                {
                    ["QueueArn"] = $"arn:aws:sqs:us-east-1:000000000000:{nombre}",
                    ["ApproximateNumberOfMessages"] = (nombre == Dlq && !ContadorDlqAtrasado ? dlq.Count : 0).ToString()
                }
            });
        }

        public override Task<Sqs.StartMessageMoveTaskResponse> StartMessageMoveTaskAsync(
            Sqs.StartMessageMoveTaskRequest r, CancellationToken ct = default)
        {
            if (TareaMueve)
            {
                EnDestino += dlq.Count;
                dlq.Clear();
            }
            return Task.FromResult(new Sqs.StartMessageMoveTaskResponse { TaskHandle = "tarea-1" });
        }

        public override Task<Sqs.ListMessageMoveTasksResponse> ListMessageMoveTasksAsync(
            Sqs.ListMessageMoveTasksRequest r, CancellationToken ct = default) =>
            Task.FromResult(new Sqs.ListMessageMoveTasksResponse
            {
                Results = [new Sqs.ListMessageMoveTasksResultEntry { Status = "COMPLETED", ApproximateNumberOfMessagesMoved = TareaInforma }]
            });

        public override Task<Sqs.ReceiveMessageResponse> ReceiveMessageAsync(
            Sqs.ReceiveMessageRequest r, CancellationToken ct = default) =>
            Task.FromResult(new Sqs.ReceiveMessageResponse { Messages = dlq.Take(r.MaxNumberOfMessages ?? 1).ToList() });

        public override Task<Sqs.SendMessageResponse> SendMessageAsync(Sqs.SendMessageRequest r, CancellationToken ct = default)
        {
            EnDestino++;
            return Task.FromResult(new Sqs.SendMessageResponse());
        }

        public override Task<Sqs.DeleteMessageResponse> DeleteMessageAsync(
            Sqs.DeleteMessageRequest r, CancellationToken ct = default)
        {
            var quedan = dlq.Where(m => m.ReceiptHandle != r.ReceiptHandle).ToList();
            dlq.Clear();
            foreach (var m in quedan) dlq.Enqueue(m);
            return Task.FromResult(new Sqs.DeleteMessageResponse());
        }
    }
}
