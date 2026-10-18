using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace CachaElPrecio.Gateway.Mensajeria;

// Mensajeria simulada en memoria (MENSAJERIA_MODO=memoria). Sirve para programar
// la vista de administracion (T12) y para los tests sin una cuenta de AWS. Se
// pierde al reiniciar el servicio, y eso esta bien: no reemplaza a SQS, solo
// imita lo que el componente de administracion necesita ver.
public sealed class AdministradorMensajeriaMemoria : IAdministradorMensajeria
{
    private sealed class Cola(ConfiguracionCola config)
    {
        public ConfiguracionCola Config { get; set; } = config;
        public List<MensajeDlq> Mensajes { get; } = [];
    }

    private readonly ConcurrentDictionary<string, Cola> colas = new();
    private readonly ConcurrentDictionary<string, byte> topicos = new();
    private readonly ConcurrentDictionary<(string Topico, string Cola), ConfiguracionSuscripcion> suscripciones = new();
    private readonly object candado = new();

    public string Modo => "memoria";

    // ── Colas ──────────────────────────────────────────────────────────────
    public Task<IReadOnlyList<ColaInfo>> ListarColasAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ColaInfo>>(
            colas.Values.Select(Vista).OrderBy(c => c.Nombre, StringComparer.Ordinal).ToList());

    public Task<ColaInfo?> ObtenerColaAsync(string nombre, CancellationToken ct) =>
        Task.FromResult(colas.TryGetValue(nombre, out var c) ? Vista(c) : null);

    public Task CrearColaAsync(ConfiguracionCola cola, CancellationToken ct)
    {
        if (!colas.TryAdd(cola.Nombre, new Cola(cola)))
            throw new ConflictoException($"La cola '{cola.Nombre}' ya existe.");
        return Task.CompletedTask;
    }

    public Task ActualizarColaAsync(ConfiguracionCola cola, CancellationToken ct)
    {
        if (!colas.TryGetValue(cola.Nombre, out var actual))
            throw new RecursoNoEncontradoException("la cola", cola.Nombre);
        actual.Config = cola;
        return Task.CompletedTask;
    }

    public Task EliminarColaAsync(string nombre, CancellationToken ct)
    {
        if (!colas.TryRemove(nombre, out _))
            throw new RecursoNoEncontradoException("la cola", nombre);
        // Igual que en AWS: borrar la cola deja sus suscripciones sin destino.
        foreach (var clave in suscripciones.Keys.Where(k => k.Cola == nombre).ToList())
            suscripciones.TryRemove(clave, out _);
        return Task.CompletedTask;
    }

    // ── Topicos ────────────────────────────────────────────────────────────
    public Task<IReadOnlyList<TopicoInfo>> ListarTopicosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TopicoInfo>>(topicos.Keys
            .OrderBy(t => t, StringComparer.Ordinal)
            .Select(t => new TopicoInfo(t, ArnTopico(t), false, null, []))
            .ToList());

    public Task CrearTopicoAsync(string nombre, CancellationToken ct)
    {
        if (!topicos.TryAdd(nombre, 0))
            throw new ConflictoException($"El topico '{nombre}' ya existe.");
        return Task.CompletedTask;
    }

    public Task EliminarTopicoAsync(string nombre, CancellationToken ct)
    {
        if (!topicos.TryRemove(nombre, out _))
            throw new RecursoNoEncontradoException("el topico", nombre);
        foreach (var clave in suscripciones.Keys.Where(k => k.Topico == nombre).ToList())
            suscripciones.TryRemove(clave, out _);
        return Task.CompletedTask;
    }

    // ── Suscripciones ──────────────────────────────────────────────────────
    public Task<IReadOnlyList<SuscripcionInfo>> ListarSuscripcionesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SuscripcionInfo>>(suscripciones.Values
            .OrderBy(s => s.Topico, StringComparer.Ordinal).ThenBy(s => s.Cola, StringComparer.Ordinal)
            .Select(s => new SuscripcionInfo(s.Topico, s.Cola, $"{ArnTopico(s.Topico)}:{s.Cola}",
                s.EntregaCruda, s.Filtro?.DeepClone(), false))
            .ToList());

    public Task SuscribirAsync(ConfiguracionSuscripcion s, CancellationToken ct)
    {
        if (!topicos.ContainsKey(s.Topico)) throw new RecursoNoEncontradoException("el topico", s.Topico);
        if (!colas.ContainsKey(s.Cola)) throw new RecursoNoEncontradoException("la cola", s.Cola);
        suscripciones[(s.Topico, s.Cola)] = s with { Filtro = s.Filtro?.DeepClone() };
        return Task.CompletedTask;
    }

    public Task EliminarSuscripcionAsync(string topico, string cola, CancellationToken ct)
    {
        if (!suscripciones.TryRemove((topico, cola), out _))
            throw new RecursoNoEncontradoException("la suscripcion", $"{topico} -> {cola}");
        return Task.CompletedTask;
    }

    // ── DLQ ────────────────────────────────────────────────────────────────
    public Task<IReadOnlyList<MensajeDlq>> VerMensajesAsync(string cola, int maximo, CancellationToken ct)
    {
        if (!colas.TryGetValue(cola, out var c)) throw new RecursoNoEncontradoException("la cola", cola);
        lock (candado)
            return Task.FromResult<IReadOnlyList<MensajeDlq>>(c.Mensajes.Take(maximo).ToList());
    }

    public Task<ResultadoReproceso> ReprocesarAsync(string dlq, string destino, CancellationToken ct)
    {
        if (!colas.TryGetValue(dlq, out var origen)) throw new RecursoNoEncontradoException("la cola", dlq);
        if (!colas.TryGetValue(destino, out var cola)) throw new RecursoNoEncontradoException("la cola", destino);

        int movidos;
        lock (candado)
        {
            movidos = origen.Mensajes.Count;
            cola.Mensajes.AddRange(origen.Mensajes);
            origen.Mensajes.Clear();
        }
        return Task.FromResult(new ResultadoReproceso(dlq, destino, "memoria", movidos, null));
    }

    // Solo para la demo y los tests: deja un mensaje en una cola como si un
    // consumidor lo hubiera apartado con sus atributos de diagnostico.
    public void Sembrar(string cola, string cuerpo, IReadOnlyDictionary<string, string> diagnostico)
    {
        if (!colas.TryGetValue(cola, out var c)) throw new RecursoNoEncontradoException("la cola", cola);
        lock (candado)
            c.Mensajes.Add(new MensajeDlq(Guid.NewGuid().ToString(), cuerpo, diagnostico, 1, DateTimeOffset.UtcNow));
    }

    private ColaInfo Vista(Cola c)
    {
        int mensajes;
        lock (candado) mensajes = c.Mensajes.Count;
        return new ColaInfo(
            c.Config.Nombre,
            $"memoria://{c.Config.Nombre}",
            $"arn:memoria:sqs:local:000000000000:{c.Config.Nombre}",
            c.Config.VisibilidadSegundos,
            c.Config.RetencionSegundos,
            c.Config.EsperaSegundos,
            mensajes, 0, 0,
            c.Config.Dlq,
            [], false, null);
    }

    private static string ArnTopico(string nombre) => $"arn:memoria:sns:local:000000000000:{nombre}";
}
