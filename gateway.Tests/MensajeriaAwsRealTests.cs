using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using CachaElPrecio.Gateway.Mensajeria;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using Sns = Amazon.SimpleNotificationService.Model;
using Sqs = Amazon.SQS.Model;

namespace CachaElPrecio.Gateway.Tests;

/// <summary>Un test que solo corre contra AWS de verdad cuando se pide con CEP_AWS_REAL=1.</summary>
public sealed class FactAwsAttribute : FactAttribute
{
    public FactAwsAttribute()
    {
        if (Environment.GetEnvironmentVariable("CEP_AWS_REAL") != "1")
            Skip = "Contra AWS real: correr con CEP_AWS_REAL=1 y credenciales del Learner Lab.";
    }
}

/// <summary>
/// La administración de mensajería contra SQS y SNS de verdad (T11). Usa las
/// credenciales por defecto (~/.aws/credentials). No toca los recursos del
/// sistema salvo para leerlos: crea los suyos con el nombre cep-prueba-XXXX y
/// los borra al final, pase lo que pase.
///
///     CEP_AWS_REAL=1 dotnet test gateway.Tests --filter MensajeriaAwsRealTests
/// </summary>
public class MensajeriaAwsRealTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly IAmazonSQS sqs = new AmazonSQSClient(RegionEndpoint.USEast1);
    private readonly IAmazonSimpleNotificationService sns = new AmazonSimpleNotificationServiceClient(RegionEndpoint.USEast1);
    private readonly ServicioAdministracion servicio;

    public MensajeriaAwsRealTests()
    {
        var aws = new AdministradorMensajeriaAws(sqs, sns, "cep-", NullLogger<AdministradorMensajeriaAws>.Instance);
        servicio = new ServicioAdministracion(aws, CargarTopologia(), NullLogger<ServicioAdministracion>.Instance);
    }

    [FactAws]
    public async Task La_topologia_real_esta_sincronizada_y_aplicarla_no_cambia_nada()
    {
        var estado = await servicio.CompararTopologiaAsync(Ct);
        Assert.True(estado.Sincronizada, string.Join("\n", estado.Diferencias.Select(d => $"{d.Estado} {d.Nombre}: {d.Detalle}")));

        var acciones = await servicio.AplicarTopologiaAsync(Ct);
        Assert.All(acciones, a => Assert.Equal("sin cambios", a.Resultado));

        var crudas = await servicio.ObtenerColaAsync("cep-ofertas-crudas", Ct);
        Assert.Equal(new ReglaDlq("cep-ofertas-crudas-dlq", 5), crudas.Dlq);
        var topico = Assert.Single(await servicio.ListarTopicosAsync(Ct), t => t.Nombre == "cep-cambios-oferta");
        Assert.Equal(2, topico.Suscripciones.Count);
    }

    [FactAws]
    public async Task Ciclo_completo_crear_filtrar_mirar_redrive_y_borrar()
    {
        string sufijo = Guid.NewGuid().ToString("N")[..6];
        string dlq = $"cep-prueba-{sufijo}-dlq", cola = $"cep-prueba-{sufijo}", topico = $"cep-prueba-{sufijo}-topico";
        try
        {
            // 1. Crear: DLQ, cola con su DLQ tras 3 intentos y un tópico.
            await servicio.CrearColaAsync(new CrearColaSolicitud(dlq, null, 1209600, 0, null, null), Ct);
            var creada = await servicio.CrearColaAsync(new CrearColaSolicitud(cola, 30, null, 1, dlq, 3), Ct);
            Assert.Equal(new ReglaDlq(dlq, 3), creada.Dlq);
            // EsDlqDe sale del listado, que tarda unos segundos en mostrar la cola nueva.
            var laDlq = await Reintentar(() => servicio.ObtenerColaAsync(dlq, Ct), c => c.EsDlqDe.Count == 1, veces: 40);
            Assert.Equal(new[] { cola }, laDlq.EsDlqDe);
            await servicio.CrearTopicoAsync(new CrearTopicoSolicitud(topico), Ct);

            // 2. Regla de distribución: solo los agotados llegan a la cola.
            await servicio.CrearSuscripcionAsync(new CrearSuscripcionSolicitud(topico, cola,
                JsonNode.Parse("""{"type": ["oferta.agotada"]}"""), true), Ct);
            var sub = Assert.Single((await servicio.ListarTopicosAsync(Ct)).Single(t => t.Nombre == topico).Suscripciones);
            Assert.True(sub.EntregaCruda);

            // 3. Se publican dos cambios: el filtro deja pasar solo uno.
            string arnTopico = (await servicio.ListarTopicosAsync(Ct)).Single(t => t.Nombre == topico).Arn!;
            await Publicar(arnTopico, "precio.bajo", """{"eventId":"p1","type":"precio.bajo"}""");
            await Publicar(arnTopico, "oferta.agotada", """{"eventId":"p2","type":"oferta.agotada"}""");
            var unico = Assert.Single(await RecibirTodo(cola, esperados: 1));
            Assert.Contains("oferta.agotada", unico.Body);   // entrega cruda: el JSON tal cual, sin sobre de SNS

            // 4. Un fallo en la DLQ se mira sin borrarlo, con su diagnóstico.
            await sqs.SendMessageAsync(new Sqs.SendMessageRequest
            {
                QueueUrl = await Url(dlq),
                MessageBody = """{"eventId":"d1","type":"oferta.capturada"}""",
                MessageAttributes = new() { ["error-type"] = new Sqs.MessageAttributeValue { DataType = "String", StringValue = "definitivo" } }
            }, Ct);
            var vistos = await Reintentar(() => servicio.VerMensajesAsync(dlq, 10, Ct), r => r.Count == 1);
            Assert.Equal("definitivo", Assert.Single(vistos).Diagnostico["error-type"]);

            // 5. Redrive: el mensaje vuelve a la cola de origen.
            var redrive = await servicio.ReprocesarDlqAsync(dlq, null, Ct);
            Assert.Equal(cola, redrive.Destino);
            var devueltos = await RecibirTodo(cola, esperados: 1, segundos: 60);
            var tareas = await sqs.ListMessageMoveTasksAsync(new Sqs.ListMessageMoveTasksRequest
            {
                SourceArn = (await servicio.ObtenerColaAsync(dlq, Ct)).Arn
            }, Ct);
            string estadoTarea = string.Join("; ", (tareas.Results ?? []).Select(t =>
                $"{t.Status} movidos={t.ApproximateNumberOfMessagesMoved}/{t.ApproximateNumberOfMessagesToMove} {t.FailureReason}"));
            Assert.True(devueltos.Count == 1, $"metodo={redrive.Metodo} tarea={redrive.TareaId} estado: {estadoTarea}");
            Assert.Contains("d1", devueltos[0].Body);

            // 6. Las reglas de estado contra AWS real.
            await Assert.ThrowsAsync<ConflictoException>(() => servicio.EliminarColaAsync(dlq, true, Ct));      // DLQ en uso
            await Assert.ThrowsAsync<ConflictoException>(() => servicio.EliminarTopicoAsync(topico, true, Ct)); // con suscripción
            await Assert.ThrowsAsync<ConflictoException>(() => servicio.VerMensajesAsync(cola, 10, Ct));        // cola con DLQ
        }
        finally
        {
            // Limpieza en el orden en que las reglas lo permiten. Los listados de SQS y
            // SNS tardan unos segundos en reflejar un borrado, asi que cada paso se
            // reintenta hasta que la regla lo deje pasar (o hasta ~45 s).
            await Insistir(() => servicio.EliminarSuscripcionAsync(topico, cola, true, Ct));
            await Insistir(() => servicio.EliminarTopicoAsync(topico, true, Ct));
            await Insistir(() => servicio.EliminarColaAsync(cola, true, Ct));
            await Insistir(() => servicio.EliminarColaAsync(dlq, true, Ct));
        }

        Assert.Null(await ObtenerSiExiste(cola));
        Assert.Null(await ObtenerSiExiste(dlq));
    }

    [FactAws]
    public async Task Redrive_con_un_consumidor_que_rechaza_todo_no_hace_ping_pong()
    {
        // Lo que paso en la EC2 el 18-10: el consumidor devuelve a la DLQ, como
        // mensaje NUEVO, todo lo que el redrive le entrega. El redrive tiene que
        // mover solo lo que ya estaba y responder antes de los 30 s del borde.
        string sufijo = Guid.NewGuid().ToString("N")[..6];
        string dlq = $"cep-prueba-{sufijo}-dlq", cola = $"cep-prueba-{sufijo}";
        using var apagar = new CancellationTokenSource();
        Task? rechazador = null;
        try
        {
            await servicio.CrearColaAsync(new CrearColaSolicitud(dlq, null, 1209600, 0, null, null), Ct);
            await servicio.CrearColaAsync(new CrearColaSolicitud(cola, 30, null, 1, dlq, 3), Ct);
            string urlDlq = await Url(dlq), urlCola = await Url(cola);
            for (int i = 0; i < 3; i++)
                await sqs.SendMessageAsync(urlDlq, $$"""{"eventId":"pp{{i}}","type":"oferta.capturada"}""", Ct);
            await Task.Delay(2000, Ct);

            rechazador = Task.Run(async () =>
            {
                while (!apagar.IsCancellationRequested)
                {
                    var r = await sqs.ReceiveMessageAsync(new Sqs.ReceiveMessageRequest
                    {
                        QueueUrl = urlCola,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 1
                    }, CancellationToken.None);
                    foreach (var m in r.Messages ?? [])
                    {
                        await sqs.SendMessageAsync(urlDlq, m.Body, CancellationToken.None);
                        await sqs.DeleteMessageAsync(urlCola, m.ReceiptHandle, CancellationToken.None);
                    }
                }
            });

            var reloj = System.Diagnostics.Stopwatch.StartNew();
            var resultado = await servicio.ReprocesarDlqAsync(dlq, null, Ct);
            reloj.Stop();

            Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(25), $"tardo {reloj.Elapsed.TotalSeconds:F1} s");
            Assert.True(resultado.MensajesMovidos is null or <= 3,
                $"movio {resultado.MensajesMovidos} con el metodo {resultado.Metodo}: ping-pong");
        }
        finally
        {
            apagar.Cancel();
            if (rechazador is not null) await rechazador;
            await Insistir(() => servicio.EliminarColaAsync(cola, true, Ct));
            await Insistir(() => servicio.EliminarColaAsync(dlq, true, Ct));
        }
    }

    // =====================================================================

    private Task Publicar(string arnTopico, string tipo, string cuerpo) =>
        sns.PublishAsync(new Sns.PublishRequest
        {
            TopicArn = arnTopico,
            Message = cuerpo,
            MessageAttributes = new() { ["type"] = new Sns.MessageAttributeValue { DataType = "String", StringValue = tipo } }
        }, Ct);

    private async Task<string> Url(string cola) => (await sqs.GetQueueUrlAsync(cola, Ct)).QueueUrl;

    // Consume (lee y borra) hasta juntar `esperados` mensajes distintos o vencer el
    // plazo; despues sigue unos segundos mas para comprobar que no llegan de sobra.
    private async Task<List<Sqs.Message>> RecibirTodo(string cola, int esperados, int segundos = 30)
    {
        var url = await Url(cola);
        var todos = new Dictionary<string, Sqs.Message>();
        var limite = DateTime.UtcNow.AddSeconds(segundos);
        var extra = DateTime.MaxValue;
        while (DateTime.UtcNow < limite && DateTime.UtcNow < extra)
        {
            var r = await sqs.ReceiveMessageAsync(new Sqs.ReceiveMessageRequest
            {
                QueueUrl = url,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 5,
                VisibilityTimeout = 30
            }, Ct);
            foreach (var m in r.Messages ?? [])
            {
                todos.TryAdd(m.MessageId, m);
                await sqs.DeleteMessageAsync(url, m.ReceiptHandle, Ct);
            }
            if (todos.Count >= esperados && extra == DateTime.MaxValue) extra = DateTime.UtcNow.AddSeconds(8);
        }
        return [.. todos.Values];
    }

    private async Task<ColaInfo?> ObtenerSiExiste(string cola)
    {
        try { return await servicio.ObtenerColaAsync(cola, Ct); }
        catch (RecursoNoEncontradoException) { return null; }
    }

    private static async Task Insistir(Func<Task> accion)
    {
        for (int i = 0; i < 15; i++)
        {
            try { await accion(); return; }
            catch (RecursoNoEncontradoException) { return; }      // ya no estaba
            catch (ConflictoException) { await Task.Delay(3000); } // el listado aun no se entera
        }
    }

    private static async Task<T> Reintentar<T>(Func<Task<T>> accion, Func<T, bool> listo, int veces = 10)
    {
        T ultimo = await accion();
        for (int i = 0; i < veces && !listo(ultimo); i++)
        {
            await Task.Delay(1500);
            ultimo = await accion();
        }
        return ultimo;
    }


    private static TopologiaDeclarada CargarTopologia()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "infra", "mensajeria", "topologia.json")))
            dir = dir.Parent;
        return TopologiaDeclarada.Cargar(Path.Combine(dir!.FullName, "infra", "mensajeria", "topologia.json"));
    }
}
