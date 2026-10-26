using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Sns = Amazon.SimpleNotificationService.Model;
using Sqs = Amazon.SQS.Model;

namespace CachaElPrecio.Gateway.Mensajeria;

// Administracion real sobre SQS y SNS (MENSAJERIA_MODO=aws). Usa las credenciales
// de la cadena por defecto: en la EC2, el LabRole; en local, las del .env.
//
// ⚠️ SDK v4: las colecciones de las respuestas vienen en null cuando AWS no manda
//    nada (en la v3 venian vacias). Por eso cada lista se lee con `?? []`.
public sealed class AdministradorMensajeriaAws(
    IAmazonSQS sqs,
    IAmazonSimpleNotificationService sns,
    string prefijo,
    ILogger<AdministradorMensajeriaAws> log) : IAdministradorMensajeria
{
    public string Modo => "aws";

    // =======================================================================
    // Colas
    // =======================================================================
    public async Task<IReadOnlyList<ColaInfo>> ListarColasAsync(CancellationToken ct)
    {
        var urls = new List<string>();
        string? siguiente = null;
        do
        {
            var r = await Aws("listar las colas", () => sqs.ListQueuesAsync(new Sqs.ListQueuesRequest
            {
                QueueNamePrefix = prefijo,
                MaxResults = 1000,
                NextToken = siguiente
            }, ct));
            urls.AddRange(r.QueueUrls ?? []);
            siguiente = r.NextToken;
        } while (!string.IsNullOrEmpty(siguiente));

        var colas = new List<ColaInfo>();
        foreach (var url in urls)
        {
            var info = await LeerColaAsync(url, ct);
            if (info is not null) colas.Add(info);
        }
        return colas.OrderBy(c => c.Nombre, StringComparer.Ordinal).ToList();
    }

    public async Task<ColaInfo?> ObtenerColaAsync(string nombre, CancellationToken ct)
    {
        var url = await UrlDeColaAsync(nombre, ct);
        return url is null ? null : await LeerColaAsync(url, ct);
    }

    public async Task CrearColaAsync(ConfiguracionCola cola, CancellationToken ct)
    {
        var atributos = await AtributosColaAsync(cola, ct);
        await Aws($"crear la cola {cola.Nombre}", () => sqs.CreateQueueAsync(new Sqs.CreateQueueRequest
        {
            QueueName = cola.Nombre,
            Attributes = atributos
        }, ct));
        log.LogInformation("Cola creada: {Cola}", cola.Nombre);
    }

    public async Task ActualizarColaAsync(ConfiguracionCola cola, CancellationToken ct)
    {
        var url = await UrlDeColaAsync(cola.Nombre, ct)
            ?? throw new RecursoNoEncontradoException("la cola", cola.Nombre);
        var atributos = await AtributosColaAsync(cola, ct);
        // Sin DLQ: RedrivePolicy vacia la quita (si antes tenia).
        atributos.TryAdd("RedrivePolicy", "");
        await Aws($"actualizar la cola {cola.Nombre}", () => sqs.SetQueueAttributesAsync(
            new Sqs.SetQueueAttributesRequest { QueueUrl = url, Attributes = atributos }, ct));
        log.LogInformation("Cola actualizada: {Cola}", cola.Nombre);
    }

    public async Task EliminarColaAsync(string nombre, CancellationToken ct)
    {
        var url = await UrlDeColaAsync(nombre, ct) ?? throw new RecursoNoEncontradoException("la cola", nombre);
        await Aws($"eliminar la cola {nombre}", () => sqs.DeleteQueueAsync(new Sqs.DeleteQueueRequest { QueueUrl = url }, ct));
        log.LogWarning("Cola eliminada: {Cola}", nombre);
    }

    private async Task<Dictionary<string, string>> AtributosColaAsync(ConfiguracionCola cola, CancellationToken ct)
    {
        var atributos = new Dictionary<string, string>
        {
            ["VisibilityTimeout"] = cola.VisibilidadSegundos.ToString(),
            ["MessageRetentionPeriod"] = cola.RetencionSegundos.ToString(),
            ["ReceiveMessageWaitTimeSeconds"] = cola.EsperaSegundos.ToString(),
            // Cifrado en reposo con clave administrada por SQS (igual que crear-mensajeria.py).
            ["SqsManagedSseEnabled"] = "true"
        };

        if (cola.Dlq is not null)
        {
            var urlDlq = await UrlDeColaAsync(cola.Dlq.Cola, ct)
                ?? throw new RecursoNoEncontradoException("la DLQ", cola.Dlq.Cola);
            // Tras MaxIntentos recepciones sin DeleteMessage (sin ACK), SQS mueve el
            // mensaje a la DLQ en vez de seguir reintentando.
            atributos["RedrivePolicy"] = JsonSerializer.Serialize(new
            {
                deadLetterTargetArn = await ArnDeColaAsync(urlDlq, ct),
                maxReceiveCount = cola.Dlq.MaxIntentos.ToString()
            });
        }
        return atributos;
    }

    private async Task<ColaInfo?> LeerColaAsync(string url, CancellationToken ct)
    {
        Dictionary<string, string> a;
        try
        {
            var r = await sqs.GetQueueAttributesAsync(new Sqs.GetQueueAttributesRequest
            {
                QueueUrl = url,
                AttributeNames = ["All"]
            }, ct);
            a = r.Attributes ?? [];
        }
        catch (Sqs.QueueDoesNotExistException)
        {
            // Se borro entre el listado y la lectura: no se muestra.
            return null;
        }
        catch (AmazonServiceException e)
        {
            throw Traducir(e, "leer los atributos de una cola");
        }

        ReglaDlq? dlq = null;
        if (a.TryGetValue("RedrivePolicy", out var redrive) && !string.IsNullOrWhiteSpace(redrive))
        {
            var r = JsonNode.Parse(redrive);
            var arnDlq = r?["deadLetterTargetArn"]?.GetValue<string>();
            var intentos = r?["maxReceiveCount"];
            int max = intentos is JsonValue v && v.TryGetValue<int>(out var n) ? n
                : int.TryParse(intentos?.ToString(), out var m) ? m : 0;
            if (arnDlq is not null) dlq = new ReglaDlq(NombreDeArn(arnDlq), max);
        }

        return new ColaInfo(
            NombreDeUrl(url), url, Texto(a, "QueueArn"),
            Entero(a, "VisibilityTimeout"),
            Entero(a, "MessageRetentionPeriod"),
            Entero(a, "ReceiveMessageWaitTimeSeconds"),
            Entero(a, "ApproximateNumberOfMessages"),
            Entero(a, "ApproximateNumberOfMessagesNotVisible"),
            Entero(a, "ApproximateNumberOfMessagesDelayed"),
            dlq, [], false, null);
    }

    // =======================================================================
    // Topicos
    // =======================================================================
    public async Task<IReadOnlyList<TopicoInfo>> ListarTopicosAsync(CancellationToken ct)
    {
        var topicos = new List<TopicoInfo>();
        // Las suscripciones las agrega ServicioAdministracion con ListarSuscripcionesAsync.
        foreach (var arn in await ArnsDeTopicosAsync(ct))
            topicos.Add(new TopicoInfo(NombreDeArn(arn), arn, false, null, []));
        return topicos.OrderBy(t => t.Nombre, StringComparer.Ordinal).ToList();
    }

    public async Task CrearTopicoAsync(string nombre, CancellationToken ct)
    {
        await Aws($"crear el topico {nombre}", () => sns.CreateTopicAsync(new Sns.CreateTopicRequest { Name = nombre }, ct));
        log.LogInformation("Topico creado: {Topico}", nombre);
    }

    public async Task EliminarTopicoAsync(string nombre, CancellationToken ct)
    {
        var arn = await ArnDeTopicoAsync(nombre, ct) ?? throw new RecursoNoEncontradoException("el topico", nombre);
        await Aws($"eliminar el topico {nombre}", () => sns.DeleteTopicAsync(new Sns.DeleteTopicRequest { TopicArn = arn }, ct));
        log.LogWarning("Topico eliminado: {Topico}", nombre);
    }

    // =======================================================================
    // Suscripciones
    // =======================================================================
    public async Task<IReadOnlyList<SuscripcionInfo>> ListarSuscripcionesAsync(CancellationToken ct)
    {
        var resultado = new List<SuscripcionInfo>();
        foreach (var arnTopico in await ArnsDeTopicosAsync(ct))
        {
            foreach (var s in await SuscripcionesDeTopicoAsync(arnTopico, ct))
            {
                if (s.Protocol != "sqs") continue;
                var atributos = await AtributosSuscripcionAsync(s.SubscriptionArn, ct);
                JsonNode? filtro = atributos.TryGetValue("FilterPolicy", out var f) && !string.IsNullOrWhiteSpace(f)
                    ? JsonNode.Parse(f) : null;
                resultado.Add(new SuscripcionInfo(
                    NombreDeArn(arnTopico), NombreDeArn(s.Endpoint), s.SubscriptionArn,
                    atributos.TryGetValue("RawMessageDelivery", out var raw) && raw == "true",
                    filtro, false));
            }
        }
        return resultado;
    }

    public async Task SuscribirAsync(ConfiguracionSuscripcion s, CancellationToken ct)
    {
        var arnTopico = await ArnDeTopicoAsync(s.Topico, ct) ?? throw new RecursoNoEncontradoException("el topico", s.Topico);
        var urlCola = await UrlDeColaAsync(s.Cola, ct) ?? throw new RecursoNoEncontradoException("la cola", s.Cola);
        var arnCola = await ArnDeColaAsync(urlCola, ct);

        // 1. La cola tiene que aceptar al topico ANTES de suscribirla.
        var origenes = (await TopicosQueEnvianAAsync(arnCola, ct)).Append(arnTopico).ToHashSet();
        await AplicarPoliticaColaAsync(urlCola, arnCola, origenes, ct);

        // 2. La suscripcion (subscribe es idempotente para el mismo topico + endpoint).
        var existente = (await SuscripcionesDeTopicoAsync(arnTopico, ct)).FirstOrDefault(x => x.Endpoint == arnCola);
        var arnSub = existente?.SubscriptionArn;
        if (arnSub is null || arnSub == "PendingConfirmation")
        {
            var r = await Aws($"suscribir {s.Cola} a {s.Topico}", () => sns.SubscribeAsync(new Sns.SubscribeRequest
            {
                TopicArn = arnTopico,
                Protocol = "sqs",
                Endpoint = arnCola,
                ReturnSubscriptionArn = true
            }, ct));
            arnSub = r.SubscriptionArn;
        }

        // 3. Entrega cruda: la cola recibe el JSON del evento, no el sobre de SNS.
        await AtributoSuscripcionAsync(arnSub, "RawMessageDelivery", s.EntregaCruda ? "true" : "false", ct);

        // 4. El filtro se evalua sobre los ATRIBUTOS del mensaje. "{}" lo quita.
        await AtributoSuscripcionAsync(arnSub, "FilterPolicy", s.Filtro?.ToJsonString() ?? "{}", ct);
        log.LogInformation("Suscripcion {Topico} -> {Cola} (filtro: {Filtro})",
            s.Topico, s.Cola, s.Filtro?.ToJsonString() ?? "sin filtro");
    }

    public async Task EliminarSuscripcionAsync(string topico, string cola, CancellationToken ct)
    {
        var arnTopico = await ArnDeTopicoAsync(topico, ct) ?? throw new RecursoNoEncontradoException("el topico", topico);
        var urlCola = await UrlDeColaAsync(cola, ct);
        var sub = (await SuscripcionesDeTopicoAsync(arnTopico, ct))
            .FirstOrDefault(x => NombreDeArn(x.Endpoint) == cola)
            ?? throw new RecursoNoEncontradoException("la suscripcion", $"{topico} -> {cola}");

        await Aws($"eliminar la suscripcion {topico} -> {cola}",
            () => sns.UnsubscribeAsync(new Sns.UnsubscribeRequest { SubscriptionArn = sub.SubscriptionArn }, ct));

        // La cola deja de aceptar a ese topico (minimo privilegio).
        if (urlCola is not null)
        {
            var arnCola = await ArnDeColaAsync(urlCola, ct);
            var origenes = (await TopicosQueEnvianAAsync(arnCola, ct)).Where(a => a != arnTopico).ToHashSet();
            await AplicarPoliticaColaAsync(urlCola, arnCola, origenes, ct);
        }
        log.LogWarning("Suscripcion eliminada: {Topico} -> {Cola}", topico, cola);
    }

    // Una cola solo acepta mensajes de SNS si su politica lo permite, y solo de los
    // topicos que la tienen suscrita (aws:SourceArn). Misma politica que arma
    // tools/crear-mensajeria.py.
    private Task AplicarPoliticaColaAsync(string urlCola, string arnCola, ISet<string> origenes, CancellationToken ct)
    {
        string politica = origenes.Count == 0 ? "" : JsonSerializer.Serialize(new
        {
            Version = "2012-10-17",
            Statement = new[]
            {
                new
                {
                    Sid = "PermitirTopicosSuscritos",
                    Effect = "Allow",
                    Principal = new { Service = "sns.amazonaws.com" },
                    Action = "sqs:SendMessage",
                    Resource = arnCola,
                    Condition = new { ArnEquals = new Dictionary<string, string[]> { ["aws:SourceArn"] = [.. origenes.Order()] } }
                }
            }
        });
        return Aws($"ajustar la politica de {NombreDeArn(arnCola)}", () => sqs.SetQueueAttributesAsync(
            new Sqs.SetQueueAttributesRequest { QueueUrl = urlCola, Attributes = new() { ["Policy"] = politica } }, ct));
    }

    private async Task<IEnumerable<string>> TopicosQueEnvianAAsync(string arnCola, CancellationToken ct)
    {
        var topicos = new List<string>();
        foreach (var arnTopico in await ArnsDeTopicosAsync(ct))
            if ((await SuscripcionesDeTopicoAsync(arnTopico, ct)).Any(s => s.Endpoint == arnCola))
                topicos.Add(arnTopico);
        return topicos;
    }

    // =======================================================================
    // DLQ
    // =======================================================================
    public async Task<IReadOnlyList<MensajeDlq>> VerMensajesAsync(string cola, int maximo, CancellationToken ct)
    {
        var url = await UrlDeColaAsync(cola, ct) ?? throw new RecursoNoEncontradoException("la cola", cola);

        // VisibilityTimeout = 0: se leen y quedan visibles al tiro. Es una mirada,
        // no un consumo: nadie los borra.
        var r = await Aws($"leer los mensajes de {cola}", () => sqs.ReceiveMessageAsync(new Sqs.ReceiveMessageRequest
        {
            QueueUrl = url,
            MaxNumberOfMessages = Math.Clamp(maximo, 1, 10),
            VisibilityTimeout = 0,
            WaitTimeSeconds = 1,
            MessageAttributeNames = ["All"],
            MessageSystemAttributeNames = ["All"]
        }, ct));

        // Como nunca dejan de estar visibles, SQS puede devolver el MISMO mensaje
        // varias veces en una sola lectura (visto contra AWS el 18-10): se dejan
        // solo los distintos.
        return (r.Messages ?? []).DistinctBy(m => m.MessageId).Select(m => new MensajeDlq(
            m.MessageId,
            m.Body,
            (m.MessageAttributes ?? []).ToDictionary(kv => kv.Key, kv => kv.Value.StringValue ?? ""),
            Entero(m.Attributes ?? [], "ApproximateReceiveCount"),
            long.TryParse(Texto(m.Attributes ?? [], "SentTimestamp"), out var ms)
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null)).ToList();
    }

    public async Task<ResultadoReproceso> ReprocesarAsync(string dlq, string destino, CancellationToken ct)
    {
        var urlDlq = await UrlDeColaAsync(dlq, ct) ?? throw new RecursoNoEncontradoException("la cola", dlq);
        var urlDestino = await UrlDeColaAsync(destino, ct) ?? throw new RecursoNoEncontradoException("la cola", destino);

        // Solo se devuelven los mensajes que ya estaban en la DLQ al apretar redrive.
        // Visto en la EC2 el 18-10: con el consumidor corriendo, lo que se devuelve y
        // sigue siendo invalido vuelve a la DLQ al tiro como mensaje NUEVO; sin este
        // corte, el plan B lo volvia a mover en un ping-pong hasta el tope (1000
        // movimientos y 50 s, que el API Gateway corta a los 30 s con un 503).
        long inicio = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Cuantos habia ANTES de mover. El contador de la tarea nativa es
        // aproximado y puede terminar diciendo 0 aunque los haya movido todos; si
        // al final de la DLQ ya no queda ninguno de los que habia, se movieron
        // estos. Con eso el panel no dice "0 movidos" cuando si volvieron.
        int antes = await VisiblesEnAsync(urlDlq, ct);

        // 1. Lo nativo: StartMessageMoveTask (el "redrive" de la consola). El
        //    LabRole lo permite (comprobado el 18-10). Mueve lo que habia al
        //    empezar y termina solo, asi que no tiene ping-pong.
        string? tarea = null;
        try
        {
            string arnDlq = await ArnDeColaAsync(urlDlq, ct);
            var r = await sqs.StartMessageMoveTaskAsync(new Sqs.StartMessageMoveTaskRequest
            {
                SourceArn = arnDlq,
                DestinationArn = await ArnDeColaAsync(urlDestino, ct)
            }, ct);
            tarea = r.TaskHandle;
            int? movidosNativo = await EsperarTareaAsync(arnDlq, ct);
            log.LogInformation("Redrive {Dlq} -> {Destino}: {Movidos} mensajes (tarea {Tarea})",
                dlq, destino, movidosNativo?.ToString() ?? "en curso", tarea);

            // Sigue en curso: se responde ya (el API Gateway corta a los 30 s) y no
            // se mueve nada a mano, para no competir con la tarea.
            if (movidosNativo is null)
                return new ResultadoReproceso(dlq, destino, "redrive", null, tarea);

            // ⚠️ Visto contra AWS el 18-10: la tarea calcula cuantos mover con el
            //    contador APROXIMADO de la DLQ, que tarda unos segundos en ponerse al
            //    dia despues de que alguien la mira. Si el admin mira la DLQ y aprieta
            //    redrive al tiro, la tarea termina "COMPLETED" con 0 de 0. En ese caso
            //    se sigue con el plan B, que lee la cola de verdad.
            if (movidosNativo > 0)
                return new ResultadoReproceso(dlq, destino, "redrive", movidosNativo, tarea);
        }
        catch (AmazonServiceException e) when (EsPermisoDenegado(e))
        {
            log.LogWarning("StartMessageMoveTask denegado ({Codigo}); se mueve a mano", e.ErrorCode);
        }

        // 2. Plan B: recibir, reenviar y borrar, de a 10. El mensaje solo se borra
        //    de la DLQ despues de que el reenvio salio bien (mismo criterio de ACK).
        //    Con tope de tiempo, para responder siempre antes de los 30 s del borde.
        int movidos = 0;
        // true cuando el bucle termina porque ya no quedan mensajes de antes del
        // redrive (y no por el tope de tiempo).
        bool sinPendientes = false;
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < limite)
        {
            var lote = await Aws($"leer {dlq}", () => sqs.ReceiveMessageAsync(new Sqs.ReceiveMessageRequest
            {
                QueueUrl = urlDlq,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 1,
                VisibilityTimeout = 30,
                MessageAttributeNames = ["All"],
                MessageSystemAttributeNames = ["SentTimestamp"]
            }, ct));
            var mensajes = lote.Messages ?? [];
            if (mensajes.Count == 0) { sinPendientes = true; break; }

            bool soloNuevos = true;
            foreach (var m in mensajes)
            {
                // Llego a la DLQ despues de apretar redrive: es uno que se acaba de
                // devolver y volvio a fallar. Se deja visible y no se toca.
                if (long.TryParse(Texto(m.Attributes ?? [], "SentTimestamp"), out long llegada) && llegada >= inicio)
                {
                    await Aws($"liberar en {dlq}", () => sqs.ChangeMessageVisibilityAsync(urlDlq, m.ReceiptHandle, 0, ct));
                    continue;
                }

                soloNuevos = false;
                await Aws($"reenviar a {destino}", () => sqs.SendMessageAsync(new Sqs.SendMessageRequest
                {
                    QueueUrl = urlDestino,
                    MessageBody = m.Body,
                    // Se conservan los atributos de diagnostico: si vuelve a fallar,
                    // queda la historia del primer intento.
                    MessageAttributes = m.MessageAttributes
                }, ct));
                await Aws($"borrar de {dlq}", () => sqs.DeleteMessageAsync(urlDlq, m.ReceiptHandle, ct));
                movidos++;
            }
            if (soloNuevos) { sinPendientes = true; break; }
        }

        // Si la tarea nativa corrio y ya no queda ninguno de los que habia, se
        // movieron todos: la parte que no conto la tarea la movio ella igual.
        // Si se corto por tiempo, solo se informa lo que se conto de verdad.
        int total = tarea is not null && sinPendientes ? Math.Max(movidos, antes) : movidos;
        log.LogInformation("Reproceso {Dlq} -> {Destino}: {Total} mensajes ({Manual} a mano, {Antes} habia antes)",
            dlq, destino, total, movidos, antes);
        return new ResultadoReproceso(dlq, destino, tarea is null ? "manual" : "redrive+manual", total, tarea);
    }

    // Mensajes visibles en una cola (ApproximateNumberOfMessages). 0 si no se puede
    // leer: es solo para informar, no decide nada que se mueva.
    private async Task<int> VisiblesEnAsync(string url, CancellationToken ct)
    {
        try
        {
            var r = await sqs.GetQueueAttributesAsync(new Sqs.GetQueueAttributesRequest
            {
                QueueUrl = url,
                AttributeNames = ["ApproximateNumberOfMessages"]
            }, ct);
            return Entero(r.Attributes ?? [], "ApproximateNumberOfMessages");
        }
        catch (AmazonServiceException e)
        {
            log.LogWarning(e, "No se pudo contar los mensajes de {Cola}", NombreDeUrl(url));
            return 0;
        }
    }

    // Espera (hasta ~8 s) a que la tarea de movimiento mas reciente de la DLQ
    // termine, y devuelve cuantos mensajes movio. null si no alcanzo a terminar.
    private async Task<int?> EsperarTareaAsync(string arnDlq, CancellationToken ct)
    {
        for (int i = 0; i < 8; i++)
        {
            var r = await Aws("consultar el redrive", () => sqs.ListMessageMoveTasksAsync(
                new Sqs.ListMessageMoveTasksRequest { SourceArn = arnDlq, MaxResults = 1 }, ct));
            var t = (r.Results ?? []).FirstOrDefault();
            if (t is not null && t.Status is "COMPLETED" or "FAILED" or "CANCELLED")
                return (int?)t.ApproximateNumberOfMessagesMoved ?? 0;
            await Task.Delay(1000, ct);
        }
        return null;
    }

    // =======================================================================
    // Ayudantes
    // =======================================================================
    private async Task<string?> UrlDeColaAsync(string nombre, CancellationToken ct)
    {
        try
        {
            return (await sqs.GetQueueUrlAsync(new Sqs.GetQueueUrlRequest { QueueName = nombre }, ct)).QueueUrl;
        }
        catch (Sqs.QueueDoesNotExistException)
        {
            return null;
        }
        catch (AmazonServiceException e)
        {
            throw Traducir(e, $"buscar la cola {nombre}");
        }
    }

    private async Task<string> ArnDeColaAsync(string url, CancellationToken ct)
    {
        var r = await Aws("leer el ARN de una cola", () => sqs.GetQueueAttributesAsync(new Sqs.GetQueueAttributesRequest
        {
            QueueUrl = url,
            AttributeNames = ["QueueArn"]
        }, ct));
        return Texto(r.Attributes ?? [], "QueueArn") ?? throw new ErrorProveedorException($"SQS no devolvio el ARN de la cola {NombreDeUrl(url)}.", false);
    }

    private async Task<List<string>> ArnsDeTopicosAsync(CancellationToken ct)
    {
        var arns = new List<string>();
        string? siguiente = null;
        do
        {
            var r = await Aws("listar los topicos", () => sns.ListTopicsAsync(new Sns.ListTopicsRequest { NextToken = siguiente }, ct));
            arns.AddRange((r.Topics ?? []).Select(t => t.TopicArn).Where(a => NombreDeArn(a).StartsWith(prefijo, StringComparison.Ordinal)));
            siguiente = r.NextToken;
        } while (!string.IsNullOrEmpty(siguiente));
        return arns;
    }

    private async Task<string?> ArnDeTopicoAsync(string nombre, CancellationToken ct) =>
        (await ArnsDeTopicosAsync(ct)).FirstOrDefault(a => NombreDeArn(a) == nombre);

    private async Task<List<Sns.Subscription>> SuscripcionesDeTopicoAsync(string arnTopico, CancellationToken ct)
    {
        var subs = new List<Sns.Subscription>();
        string? siguiente = null;
        do
        {
            var r = await Aws("listar las suscripciones", () => sns.ListSubscriptionsByTopicAsync(
                new Sns.ListSubscriptionsByTopicRequest { TopicArn = arnTopico, NextToken = siguiente }, ct));
            subs.AddRange(r.Subscriptions ?? []);
            siguiente = r.NextToken;
        } while (!string.IsNullOrEmpty(siguiente));
        return subs;
    }

    private async Task<Dictionary<string, string>> AtributosSuscripcionAsync(string arn, CancellationToken ct)
    {
        if (arn == "PendingConfirmation") return [];
        var r = await Aws("leer una suscripcion", () => sns.GetSubscriptionAttributesAsync(
            new Sns.GetSubscriptionAttributesRequest { SubscriptionArn = arn }, ct));
        return r.Attributes ?? [];
    }

    private Task AtributoSuscripcionAsync(string arn, string nombre, string valor, CancellationToken ct) =>
        Aws($"ajustar {nombre} de la suscripcion", () => sns.SetSubscriptionAttributesAsync(
            new Sns.SetSubscriptionAttributesRequest { SubscriptionArn = arn, AttributeName = nombre, AttributeValue = valor }, ct));

    // Toda llamada a AWS pasa por aca: los errores salen traducidos a algo que
    // una persona entiende, y el permiso denegado se distingue del resto.
    public async Task<string> PublicarAsync(string topico, string mensaje, IReadOnlyList<AtributoMensaje> atributos, CancellationToken ct)
    {
        var arn = await ArnDeTopicoAsync(topico, ct) ?? throw new RecursoNoEncontradoException("el topico", topico);
        var r = await Aws($"publicar en el topico {topico}", () => sns.PublishAsync(new Sns.PublishRequest
        {
            TopicArn = arn,
            Message = mensaje,
            MessageAttributes = atributos.ToDictionary(
                a => a.Nombre,
                a => new Sns.MessageAttributeValue { DataType = a.Tipo, StringValue = a.Valor })
        }, ct));
        log.LogInformation("Publicado en {Topico} desde el panel: {MessageId}", topico, r.MessageId);
        return r.MessageId;
    }

    private static async Task<T> Aws<T>(string operacion, Func<Task<T>> llamada)
    {
        try
        {
            return await llamada();
        }
        catch (AmazonServiceException e)
        {
            throw Traducir(e, operacion);
        }
    }

    private static Exception Traducir(AmazonServiceException e, string operacion)
    {
        string codigo = e.ErrorCode ?? "";
        if (e is Sqs.QueueDoesNotExistException || codigo is "NotFound" or "AWS.SimpleQueueService.NonExistentQueue")
            return new RecursoNoEncontradoException("el recurso", operacion);
        if (e is Sqs.QueueDeletedRecentlyException)
            return new ConflictoException("SQS no deja recrear una cola con el mismo nombre hasta 60 segundos despues de borrarla. Espera un minuto y vuelve a intentar.");
        if (codigo.Contains("QueueAlreadyExists", StringComparison.Ordinal) || codigo == "QueueNameExists")
            return new ConflictoException("Ya existe una cola con ese nombre y con otros atributos.");
        if (EsPermisoDenegado(e))
            return new ErrorProveedorException($"AWS no permitio {operacion}: el rol de la cuenta (LabRole) no tiene ese permiso.", true, e);
        // El mensaje de AWS puede traer ARN y datos de la cuenta: va al log (con la
        // excepcion interna), no a la respuesta.
        return new ErrorProveedorException($"AWS fallo al {operacion} (codigo {codigo}).", false, e);
    }

    private static bool EsPermisoDenegado(AmazonServiceException e) =>
        e.StatusCode == System.Net.HttpStatusCode.Forbidden
        || (e.ErrorCode ?? "").Contains("AccessDenied", StringComparison.Ordinal)
        || e.ErrorCode == "AuthorizationError";

    private static string NombreDeUrl(string url) => url.TrimEnd('/').Split('/')[^1];
    private static string NombreDeArn(string arn) => arn.Split(':')[^1];
    private static string? Texto(Dictionary<string, string> a, string clave) => a.TryGetValue(clave, out var v) ? v : null;
    private static int Entero(Dictionary<string, string> a, string clave) => int.TryParse(Texto(a, clave), out var n) ? n : 0;
}
