using System.Text.Json.Nodes;

namespace CachaElPrecio.Gateway.Mensajeria;

// Las reglas de la administracion de mensajeria. Los endpoints llaman a este
// servicio; este valida, revisa el estado y recien ahi le pide a
// IAdministradorMensajeria que ejecute. Asi la regla es la misma con AWS o en
// memoria, y los endpoints no saben nada de SQS.
//
// Reglas de estado (las de forma estan en ValidadorSolicitudes):
//   · no se crea lo que ya existe (409)
//   · una DLQ o un topico referenciado tiene que existir (400 en el campo)
//   · lo declarado en topologia.json esta protegido: borrarlo exige ?forzar=true,
//     para no romper un flujo en produccion por un clic (409)
//   · no se borra una cola que es DLQ de otra, ni un topico o cola con
//     suscripciones vivas: primero se quita lo que depende (409)
public sealed class ServicioAdministracion(
    IAdministradorMensajeria mensajeria,
    TopologiaDeclarada topologia,
    ILogger<ServicioAdministracion> log)
{
    public const int VisibilidadPorDefecto = 30;
    public const int RetencionPorDefecto = 345600;   // 4 dias, el default de SQS
    public const int EsperaPorDefecto = 20;          // long polling

    public string Modo => mensajeria.Modo;
    public string Prefijo => topologia.Prefix;

    // =======================================================================
    // Colas
    // =======================================================================
    public async Task<IReadOnlyList<ColaInfo>> ListarColasAsync(CancellationToken ct)
    {
        var colas = await mensajeria.ListarColasAsync(ct);
        return colas.Select(c => Enriquecer(c, colas)).ToList();
    }

    // La existencia se pregunta directo (GetQueueUrl responde al tiro) y no al
    // listado: ListQueues es eventualmente consistente y una cola recien creada
    // puede tardar en aparecer. El listado solo se usa para EsDlqDe.
    public async Task<ColaInfo> ObtenerColaAsync(string nombre, CancellationToken ct)
    {
        Validar(ValidadorSolicitudes.ValidarNombre(nombre, Prefijo));
        var cola = await mensajeria.ObtenerColaAsync(nombre, ct)
            ?? throw new RecursoNoEncontradoException("la cola", nombre);
        return Enriquecer(cola, await mensajeria.ListarColasAsync(ct));
    }

    public async Task<ColaInfo> CrearColaAsync(CrearColaSolicitud s, CancellationToken ct)
    {
        Validar(ValidadorSolicitudes.ValidarCola(s, Prefijo));
        string nombre = s.Nombre!;

        if (await mensajeria.ObtenerColaAsync(nombre, ct) is not null)
            throw new ConflictoException($"La cola '{nombre}' ya existe.");

        ReglaDlq? dlq = null;
        if (!string.IsNullOrWhiteSpace(s.Dlq))
        {
            var colaDlq = await mensajeria.ObtenerColaAsync(s.Dlq, ct)
                ?? throw SolicitudInvalidaException.Campo("dlq", $"La DLQ '{s.Dlq}' no existe. Creala primero.");
            if (colaDlq.Dlq is not null)
                throw SolicitudInvalidaException.Campo("dlq",
                    $"'{s.Dlq}' ya manda sus fallos a '{colaDlq.Dlq.Cola}': una DLQ no deberia tener su propia DLQ.");
            dlq = new ReglaDlq(s.Dlq, s.MaxIntentos!.Value);
        }

        await mensajeria.CrearColaAsync(new ConfiguracionCola(
            nombre,
            s.VisibilidadSegundos ?? VisibilidadPorDefecto,
            s.RetencionSegundos ?? RetencionPorDefecto,
            s.EsperaSegundos ?? EsperaPorDefecto,
            dlq), ct);

        log.LogInformation("Administracion: cola {Cola} creada (DLQ: {Dlq})", nombre, dlq?.Cola ?? "ninguna");
        return await ObtenerColaAsync(nombre, ct);
    }

    public async Task EliminarColaAsync(string nombre, bool forzar, CancellationToken ct)
    {
        Validar(ValidadorSolicitudes.ValidarNombre(nombre, Prefijo));
        if (await mensajeria.ObtenerColaAsync(nombre, ct) is null)
            throw new RecursoNoEncontradoException("la cola", nombre);

        var colas = await mensajeria.ListarColasAsync(ct);
        var usuarias = colas.Where(c => c.Dlq?.Cola == nombre).Select(c => c.Nombre).ToList();
        if (usuarias.Count > 0)
            throw new ConflictoException(
                $"'{nombre}' es la DLQ de {string.Join(", ", usuarias)}. Quitale la DLQ a esas colas antes de borrarla.");

        var subs = (await mensajeria.ListarSuscripcionesAsync(ct)).Where(s => s.Cola == nombre).ToList();
        if (subs.Count > 0)
            throw new ConflictoException(
                $"'{nombre}' recibe de {string.Join(", ", subs.Select(s => s.Topico))}. Elimina esas suscripciones primero.");

        if (topologia.DeclaraCola(nombre) && !forzar)
            throw new ConflictoException(
                $"'{nombre}' esta declarada en topologia.json y la usa un flujo del sistema. " +
                "Si de verdad quieres borrarla, repite con ?forzar=true.");

        await mensajeria.EliminarColaAsync(nombre, ct);
        log.LogWarning("Administracion: cola {Cola} eliminada (forzar={Forzar})", nombre, forzar);
    }

    // =======================================================================
    // Topicos
    // =======================================================================
    public async Task<IReadOnlyList<TopicoInfo>> ListarTopicosAsync(CancellationToken ct)
    {
        var subs = await ListarSuscripcionesAsync(ct);
        return (await mensajeria.ListarTopicosAsync(ct))
            .Select(t => t with
            {
                DeclaradoEnTopologia = topologia.DeclaraTopico(t.Nombre),
                Descripcion = topologia.DescripcionTopico(t.Nombre),
                Suscripciones = subs.Where(x => x.Topico == t.Nombre).ToList()
            })
            .ToList();
    }

    public async Task<TopicoInfo> CrearTopicoAsync(CrearTopicoSolicitud s, CancellationToken ct)
    {
        Validar(ValidadorSolicitudes.ValidarTopico(s, Prefijo));
        string nombre = s.Nombre!;

        if ((await mensajeria.ListarTopicosAsync(ct)).Any(t => t.Nombre == nombre))
            throw new ConflictoException($"El topico '{nombre}' ya existe.");

        await mensajeria.CrearTopicoAsync(nombre, ct);
        log.LogInformation("Administracion: topico {Topico} creado", nombre);
        return (await ListarTopicosAsync(ct)).First(t => t.Nombre == nombre);
    }

    public async Task EliminarTopicoAsync(string nombre, bool forzar, CancellationToken ct)
    {
        Validar(ValidadorSolicitudes.ValidarNombre(nombre, Prefijo));
        var topico = (await ListarTopicosAsync(ct)).FirstOrDefault(t => t.Nombre == nombre)
            ?? throw new RecursoNoEncontradoException("el topico", nombre);

        if (topico.Suscripciones.Count > 0)
            throw new ConflictoException(
                $"'{nombre}' tiene {topico.Suscripciones.Count} suscripcion(es). Eliminalas primero: " +
                "borrar el topico dejaria a esas colas sin recibir nada, sin ningun error visible.");

        if (topologia.DeclaraTopico(nombre) && !forzar)
            throw new ConflictoException(
                $"'{nombre}' esta declarado en topologia.json y lo usa un flujo del sistema. " +
                "Si de verdad quieres borrarlo, repite con ?forzar=true.");

        await mensajeria.EliminarTopicoAsync(nombre, ct);
        log.LogWarning("Administracion: topico {Topico} eliminado (forzar={Forzar})", nombre, forzar);
    }

    // =======================================================================
    // Suscripciones
    // =======================================================================
    public async Task<IReadOnlyList<SuscripcionInfo>> ListarSuscripcionesAsync(CancellationToken ct) =>
        (await mensajeria.ListarSuscripcionesAsync(ct))
            .Select(s => s with { DeclaradaEnTopologia = topologia.DeclaraSuscripcion(s.Topico, s.Cola) })
            .ToList();

    public async Task<SuscripcionInfo> CrearSuscripcionAsync(CrearSuscripcionSolicitud s, CancellationToken ct)
    {
        Validar(ValidadorSolicitudes.ValidarSuscripcion(s, Prefijo));
        var (topico, cola) = (s.Topico!, s.Cola!);

        await RevisarReferenciasAsync(topico, cola, ct);
        if ((await mensajeria.ListarSuscripcionesAsync(ct)).Any(x => x.Topico == topico && x.Cola == cola))
            throw new ConflictoException(
                $"'{cola}' ya esta suscrita a '{topico}'. Para cambiarle el filtro usa PUT sobre esa suscripcion.");

        await mensajeria.SuscribirAsync(new ConfiguracionSuscripcion(topico, cola, s.Filtro, s.EntregaCruda ?? true), ct);
        log.LogInformation("Administracion: suscripcion {Topico} -> {Cola} creada", topico, cola);
        return await ObtenerSuscripcionAsync(topico, cola, ct);
    }

    public async Task<SuscripcionInfo> ActualizarSuscripcionAsync(
        string topico, string cola, CrearSuscripcionSolicitud s, CancellationToken ct)
    {
        Validar(ValidadorSolicitudes.ValidarSuscripcion(s with { Topico = topico, Cola = cola }, Prefijo));
        var actual = await ObtenerSuscripcionAsync(topico, cola, ct);

        await mensajeria.SuscribirAsync(
            new ConfiguracionSuscripcion(topico, cola, s.Filtro, s.EntregaCruda ?? actual.EntregaCruda), ct);
        log.LogInformation("Administracion: filtro de {Topico} -> {Cola} cambiado", topico, cola);
        return await ObtenerSuscripcionAsync(topico, cola, ct);
    }

    public async Task EliminarSuscripcionAsync(string topico, string cola, bool forzar, CancellationToken ct)
    {
        Validar(Unir(ValidadorSolicitudes.ValidarNombre(topico, Prefijo, "topico"),
                     ValidadorSolicitudes.ValidarNombre(cola, Prefijo, "cola")));
        await ObtenerSuscripcionAsync(topico, cola, ct);

        if (topologia.DeclaraSuscripcion(topico, cola) && !forzar)
            throw new ConflictoException(
                $"La regla '{topico} -> {cola}' esta declarada en topologia.json. " +
                "Si de verdad quieres quitarla, repite con ?forzar=true.");

        await mensajeria.EliminarSuscripcionAsync(topico, cola, ct);
        log.LogWarning("Administracion: suscripcion {Topico} -> {Cola} eliminada (forzar={Forzar})", topico, cola, forzar);
    }

    private async Task<SuscripcionInfo> ObtenerSuscripcionAsync(string topico, string cola, CancellationToken ct) =>
        (await ListarSuscripcionesAsync(ct)).FirstOrDefault(x => x.Topico == topico && x.Cola == cola)
        ?? throw new RecursoNoEncontradoException("la suscripcion", $"{topico} -> {cola}");

    private async Task RevisarReferenciasAsync(string topico, string cola, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        if ((await mensajeria.ListarTopicosAsync(ct)).All(t => t.Nombre != topico))
            errores["topico"] = [$"El topico '{topico}' no existe."];

        var colas = await mensajeria.ListarColasAsync(ct);
        if (await mensajeria.ObtenerColaAsync(cola, ct) is null)
            errores["cola"] = [$"La cola '{cola}' no existe."];
        else if (colas.Any(c => c.Dlq?.Cola == cola))
            errores["cola"] = [$"'{cola}' es una DLQ: no deberia recibir mensajes de un topico, solo los fallos de su cola."];

        Validar(errores);
    }

    // =======================================================================
    // DLQ
    // =======================================================================
    // Mirar mensajes sin borrarlos. Sirve para las DLQ (que fallo y por que) y
    // para cep-auditoria-precios (la copia sin filtro de todos los cambios).
    //
    // Solo colas SIN DLQ propia: para SQS, mirar ES una recepcion y suma al
    // contador de intentos. Mirar varias veces cep-ofertas-crudas empujaria
    // mensajes sanos a su DLQ sin que nadie haya fallado.
    public async Task<IReadOnlyList<MensajeDlq>> VerMensajesAsync(string cola, int maximo, CancellationToken ct)
    {
        if (maximo is < 1 or > 10)
            throw SolicitudInvalidaException.Campo("maximo", "Debe estar entre 1 y 10 (lo maximo que SQS entrega por lectura).");
        Validar(ValidadorSolicitudes.ValidarNombre(cola, Prefijo, "cola"));

        var info = await mensajeria.ObtenerColaAsync(cola, ct) ?? throw new RecursoNoEncontradoException("la cola", cola);
        if (info.Dlq is not null)
            throw new ConflictoException(
                $"'{cola}' tiene DLQ ({info.Dlq.Cola} tras {info.Dlq.MaxIntentos} intentos): mirarla cuenta como una " +
                "recepcion y podria mandar mensajes sanos a la DLQ. Revisa su DLQ o su consumidor.");
        return await mensajeria.VerMensajesAsync(cola, maximo, ct);
    }

    public async Task<ResultadoReproceso> ReprocesarDlqAsync(string dlq, string? destino, CancellationToken ct)
    {
        var origenes = await ColasQueUsanDlqAsync(dlq, ct);

        if (string.IsNullOrWhiteSpace(destino))
        {
            if (origenes.Count > 1)
                throw SolicitudInvalidaException.Campo("destino",
                    $"'{dlq}' es la DLQ de varias colas ({string.Join(", ", origenes)}). Indica ?destino=...");
            destino = origenes[0];
        }
        else if (!origenes.Contains(destino))
        {
            throw SolicitudInvalidaException.Campo("destino",
                $"'{destino}' no usa a '{dlq}' como DLQ. Destinos posibles: {string.Join(", ", origenes)}.");
        }

        var resultado = await mensajeria.ReprocesarAsync(dlq, destino, ct);
        log.LogInformation("Administracion: reproceso {Dlq} -> {Destino} ({Metodo})", dlq, destino, resultado.Metodo);
        return resultado;
    }

    private async Task<List<string>> ColasQueUsanDlqAsync(string dlq, CancellationToken ct)
    {
        Validar(ValidadorSolicitudes.ValidarNombre(dlq, Prefijo, "dlq"));
        if (await mensajeria.ObtenerColaAsync(dlq, ct) is null)
            throw new RecursoNoEncontradoException("la cola", dlq);

        var colas = await mensajeria.ListarColasAsync(ct);

        var origenes = colas.Where(c => c.Dlq?.Cola == dlq).Select(c => c.Nombre).ToList();
        if (origenes.Count == 0)
            throw new ConflictoException($"'{dlq}' no es la DLQ de ninguna cola.");
        return origenes;
    }

    // =======================================================================
    // Topologia: lo declarado contra lo que existe
    // =======================================================================
    public async Task<EstadoTopologia> CompararTopologiaAsync(CancellationToken ct)
    {
        var colas = (await mensajeria.ListarColasAsync(ct)).ToDictionary(c => c.Nombre);
        var topicos = (await mensajeria.ListarTopicosAsync(ct)).Select(t => t.Nombre).ToHashSet();
        var subs = (await mensajeria.ListarSuscripcionesAsync(ct)).ToDictionary(s => (s.Topico, s.Cola));
        var diferencias = new List<Diferencia>();

        foreach (var c in topologia.ColasEnOrden())
        {
            if (!colas.TryGetValue(c.Nombre, out var real))
            {
                diferencias.Add(new("cola", c.Nombre, "falta", "Declarada pero no existe."));
                continue;
            }
            var detalle = new List<string>();
            if (real.VisibilidadSegundos != c.VisibilidadSegundos) detalle.Add($"visibilidad {real.VisibilidadSegundos} s (declarada {c.VisibilidadSegundos} s)");
            if (real.RetencionSegundos != c.RetencionSegundos) detalle.Add($"retencion {real.RetencionSegundos} s (declarada {c.RetencionSegundos} s)");
            if (real.EsperaSegundos != c.EsperaSegundos) detalle.Add($"espera {real.EsperaSegundos} s (declarada {c.EsperaSegundos} s)");
            if (real.Dlq != c.Dlq) detalle.Add($"DLQ {Describir(real.Dlq)} (declarada {Describir(c.Dlq)})");
            if (detalle.Count > 0) diferencias.Add(new("cola", c.Nombre, "distinta", string.Join("; ", detalle)));
        }

        foreach (var t in topologia.Topics.Select(t => topologia.Completo(t.Name)))
            if (!topicos.Contains(t))
                diferencias.Add(new("topico", t, "falta", "Declarado pero no existe."));

        foreach (var s in topologia.SuscripcionesCompletas())
        {
            string nombre = $"{s.Topico} -> {s.Cola}";
            if (!subs.TryGetValue((s.Topico, s.Cola), out var real))
            {
                diferencias.Add(new("suscripcion", nombre, "falta", "Declarada pero no existe."));
                continue;
            }
            var detalle = new List<string>();
            if (!JsonNode.DeepEquals(real.Filtro, s.Filtro)) detalle.Add("el filtro no coincide con el declarado");
            if (real.EntregaCruda != s.EntregaCruda) detalle.Add($"entrega cruda {real.EntregaCruda} (declarada {s.EntregaCruda})");
            if (detalle.Count > 0) diferencias.Add(new("suscripcion", nombre, "distinta", string.Join("; ", detalle)));
        }

        // Lo que existe con el prefijo y no esta declarado: creado a mano o desde
        // este mismo panel. No es un error, pero conviene verlo.
        foreach (var c in colas.Keys.Where(n => !topologia.DeclaraCola(n)))
            diferencias.Add(new("cola", c, "extra", "Existe pero no esta en topologia.json."));
        foreach (var t in topicos.Where(n => !topologia.DeclaraTopico(n)))
            diferencias.Add(new("topico", t, "extra", "Existe pero no esta en topologia.json."));
        foreach (var k in subs.Keys.Where(k => !topologia.DeclaraSuscripcion(k.Topico, k.Cola)))
            diferencias.Add(new("suscripcion", $"{k.Topico} -> {k.Cola}", "extra", "Existe pero no esta en topologia.json."));

        bool sincronizada = diferencias.All(d => d.Estado == "extra");
        return new EstadoTopologia(Modo, topologia.Version, sincronizada, diferencias);
    }

    // Deja AWS igual que topologia.json (crea lo que falta y corrige lo distinto).
    // Hace lo mismo que tools/crear-mensajeria.py, pero desde la aplicacion. Es
    // idempotente: aplicarla dos veces seguidas no cambia nada la segunda vez.
    // No borra lo "extra": eso se decide a mano.
    public async Task<IReadOnlyList<Accion>> AplicarTopologiaAsync(CancellationToken ct)
    {
        var acciones = new List<Accion>();
        var colas = (await mensajeria.ListarColasAsync(ct)).ToDictionary(c => c.Nombre);

        foreach (var c in topologia.ColasEnOrden())
        {
            if (!colas.TryGetValue(c.Nombre, out var real))
            {
                await mensajeria.CrearColaAsync(c, ct);
                acciones.Add(new("cola", c.Nombre, "creada"));
            }
            else if (real.VisibilidadSegundos != c.VisibilidadSegundos || real.RetencionSegundos != c.RetencionSegundos
                     || real.EsperaSegundos != c.EsperaSegundos || real.Dlq != c.Dlq)
            {
                await mensajeria.ActualizarColaAsync(c, ct);
                acciones.Add(new("cola", c.Nombre, "actualizada"));
            }
            else acciones.Add(new("cola", c.Nombre, "sin cambios"));
        }

        var topicos = (await mensajeria.ListarTopicosAsync(ct)).Select(t => t.Nombre).ToHashSet();
        foreach (var t in topologia.Topics.Select(t => topologia.Completo(t.Name)))
        {
            if (topicos.Contains(t)) { acciones.Add(new("topico", t, "sin cambios")); continue; }
            await mensajeria.CrearTopicoAsync(t, ct);
            acciones.Add(new("topico", t, "creado"));
        }

        var subs = (await mensajeria.ListarSuscripcionesAsync(ct)).ToDictionary(s => (s.Topico, s.Cola));
        foreach (var s in topologia.SuscripcionesCompletas())
        {
            string nombre = $"{s.Topico} -> {s.Cola}";
            bool existe = subs.TryGetValue((s.Topico, s.Cola), out var real);
            if (existe && JsonNode.DeepEquals(real!.Filtro, s.Filtro) && real.EntregaCruda == s.EntregaCruda)
            {
                acciones.Add(new("suscripcion", nombre, "sin cambios"));
                continue;
            }
            await mensajeria.SuscribirAsync(s, ct);
            acciones.Add(new("suscripcion", nombre, existe ? "actualizada" : "creada"));
        }

        log.LogInformation("Administracion: topologia aplicada ({Cambios} cambios)",
            acciones.Count(a => a.Resultado != "sin cambios"));
        return acciones;
    }

    // =======================================================================
    // Ayudantes
    // =======================================================================
    private ColaInfo Enriquecer(ColaInfo c, IReadOnlyList<ColaInfo> todas) => c with
    {
        EsDlqDe = todas.Where(o => o.Dlq?.Cola == c.Nombre).Select(o => o.Nombre).ToList(),
        DeclaradaEnTopologia = topologia.DeclaraCola(c.Nombre),
        Descripcion = topologia.DescripcionCola(c.Nombre)
    };

    private static string Describir(ReglaDlq? r) => r is null ? "ninguna" : $"{r.Cola} tras {r.MaxIntentos}";

    private static void Validar(Dictionary<string, string[]> errores)
    {
        if (errores.Count > 0) throw new SolicitudInvalidaException(errores);
    }

    private static Dictionary<string, string[]> Unir(params Dictionary<string, string[]>[] partes) =>
        partes.SelectMany(p => p).ToDictionary(kv => kv.Key, kv => kv.Value);
}

public sealed record Diferencia(string Recurso, string Nombre, string Estado, string Detalle);

public sealed record EstadoTopologia(string Modo, int Version, bool Sincronizada, IReadOnlyList<Diferencia> Diferencias);

public sealed record Accion(string Recurso, string Nombre, string Resultado);
