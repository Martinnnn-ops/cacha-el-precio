using System.Text.Json;
using System.Text.Json.Nodes;

namespace CachaElPrecio.Gateway.Mensajeria;

// infra/mensajeria/topologia.json leido tal cual. Es el UNICO lugar donde se
// declaran colas, topicos y reglas (ADR-029): este servicio y
// tools/crear-mensajeria.py leen el mismo archivo y no inventan nombres.
public sealed class TopologiaDeclarada
{
    public int Version { get; init; }
    public string Prefix { get; init; } = "cep-";
    public List<DlqDeclarada> DeadLetterQueues { get; init; } = [];
    public List<ColaDeclarada> Queues { get; init; } = [];
    public List<TopicoDeclarado> Topics { get; init; } = [];
    public List<SuscripcionDeclarada> Subscriptions { get; init; } = [];

    public string Completo(string nombre) => Prefix + nombre;

    // Las DLQ primero: tienen que existir antes que las colas que las usan.
    public IEnumerable<ConfiguracionCola> ColasEnOrden()
    {
        foreach (var d in DeadLetterQueues)
            yield return new ConfiguracionCola(Completo(d.Name), 30, d.MessageRetentionSeconds, 0, null);

        foreach (var q in Queues)
            yield return new ConfiguracionCola(
                Completo(q.Name),
                q.VisibilityTimeoutSeconds,
                q.MessageRetentionSeconds,
                q.ReceiveWaitTimeSeconds,
                q.DeadLetter is null ? null : new ReglaDlq(Completo(q.DeadLetter.Queue), q.DeadLetter.MaxReceiveCount));
    }

    public IEnumerable<ConfiguracionSuscripcion> SuscripcionesCompletas() =>
        Subscriptions.Select(s => new ConfiguracionSuscripcion(
            Completo(s.Topic), Completo(s.Queue), s.FilterPolicy, s.RawMessageDelivery));

    public bool DeclaraCola(string nombre) =>
        DeadLetterQueues.Any(d => Completo(d.Name) == nombre) || Queues.Any(q => Completo(q.Name) == nombre);

    public bool DeclaraTopico(string nombre) => Topics.Any(t => Completo(t.Name) == nombre);

    public bool DeclaraSuscripcion(string topico, string cola) =>
        Subscriptions.Any(s => Completo(s.Topic) == topico && Completo(s.Queue) == cola);

    public string? DescripcionCola(string nombre) =>
        DeadLetterQueues.FirstOrDefault(d => Completo(d.Name) == nombre)?.Description
        ?? Queues.FirstOrDefault(q => Completo(q.Name) == nombre)?.Description;

    public string? DescripcionTopico(string nombre) =>
        Topics.FirstOrDefault(t => Completo(t.Name) == nombre)?.Description;

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static TopologiaDeclarada Cargar(string ruta)
    {
        if (!File.Exists(ruta))
            throw new InvalidOperationException(
                $"No existe la topologia en '{ruta}'. Revisa TOPOLOGIA_RUTA (por defecto infra/mensajeria/topologia.json).");

        return JsonSerializer.Deserialize<TopologiaDeclarada>(File.ReadAllText(ruta), Opciones)
            ?? throw new InvalidOperationException($"La topologia en '{ruta}' esta vacia.");
    }
}

public sealed class DlqDeclarada
{
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public int MessageRetentionSeconds { get; init; } = 1209600;
}

public sealed class ColaDeclarada
{
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? Producer { get; init; }
    public string? Consumer { get; init; }
    public int VisibilityTimeoutSeconds { get; init; } = 30;
    public int MessageRetentionSeconds { get; init; } = 345600;
    public int ReceiveWaitTimeSeconds { get; init; }
    public DeadLetterDeclarada? DeadLetter { get; init; }
}

public sealed class DeadLetterDeclarada
{
    public string Queue { get; init; } = "";
    public int MaxReceiveCount { get; init; } = 5;
}

public sealed class TopicoDeclarado
{
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? Producer { get; init; }
}

public sealed class SuscripcionDeclarada
{
    public string Topic { get; init; } = "";
    public string Queue { get; init; } = "";
    public string? Description { get; init; }
    public bool RawMessageDelivery { get; init; }
    public JsonNode? FilterPolicy { get; init; }
}
