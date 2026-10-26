using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SQS;

namespace CachaElPrecio.Gateway.Mensajeria;

// La interfaz HTTP de la administración de mensajería (T11). Los endpoints solo
// traducen HTTP a métodos de ServicioAdministracion y sus excepciones a
// respuestas; no saben nada de SQS ni de SNS.
//
// Todo cuelga de /api/admin/mensajeria y exige el grupo admin de Cognito,
// igual que /api/admin/diagnostico. El API Gateway, antes, exige un token
// válido (ruta jwt en tools/crear-api-gateway.sh).
public static class EndpointsMensajeria
{
    public const string Ruta = "/api/admin/mensajeria";

    // ── Registro de servicios ────────────────────────────────────────────
    public static string AddAdministracionMensajeria(this IServiceCollection services, IConfiguration config)
    {
        // La topología viaja dentro de la imagen (/app/topologia.json, ver
        // Dockerfile); en local se lee directo desde infra/mensajeria.
        string ruta = config["TOPOLOGIA_RUTA"]
            ?? new[] { "topologia.json", "../infra/mensajeria/topologia.json", "infra/mensajeria/topologia.json" }
                .FirstOrDefault(File.Exists)
            ?? "infra/mensajeria/topologia.json";
        var topologia = TopologiaDeclarada.Cargar(ruta);
        services.AddSingleton(topologia);

        string modo = (config["MENSAJERIA_MODO"] ?? "memoria").ToLowerInvariant();
        switch (modo)
        {
            case "aws":
                // Credenciales por la cadena por defecto del SDK: en la EC2, el LabRole.
                var region = RegionEndpoint.GetBySystemName(config["AWS_REGION"] ?? config["MENSAJERIA_REGION"] ?? "us-east-1");
                services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(region));
                services.AddSingleton<IAmazonSimpleNotificationService>(_ => new AmazonSimpleNotificationServiceClient(region));
                services.AddSingleton<IAdministradorMensajeria>(sp => new AdministradorMensajeriaAws(
                    sp.GetRequiredService<IAmazonSQS>(),
                    sp.GetRequiredService<IAmazonSimpleNotificationService>(),
                    topologia.Prefix,
                    sp.GetRequiredService<ILogger<AdministradorMensajeriaAws>>()));
                break;
            case "memoria":
                services.AddSingleton<AdministradorMensajeriaMemoria>();
                services.AddSingleton<IAdministradorMensajeria>(sp => sp.GetRequiredService<AdministradorMensajeriaMemoria>());
                break;
            default:
                throw new InvalidOperationException($"MENSAJERIA_MODO='{modo}' no existe. Usa 'aws' o 'memoria'.");
        }

        services.AddScoped<ServicioAdministracion>();
        BandejaCorreosProxy.Registrar(services, config);
        return modo;
    }

    // En memoria arranca con la topología aplicada y un par de fallos en las DLQ,
    // para que la vista de administración (T12) tenga algo que mostrar sin AWS.
    public static async Task PrepararMensajeriaAsync(this WebApplication app, string modo)
    {
        var topologia = app.Services.GetRequiredService<TopologiaDeclarada>();
        app.Logger.LogInformation("Administración de mensajería en modo {Modo} (topología v{Version})", modo, topologia.Version);

        if (modo == "memoria" && app.Configuration.GetValue("MENSAJERIA_SEMILLA", true))
            await SemillaMemoria.SembrarAsync(app.Services);
    }

    // ── Rutas ────────────────────────────────────────────────────────────
    public static void MapAdministracionMensajeria(this WebApplication app)
    {
        var admin = app.MapGroup(Ruta)
            .RequireAuthorization(policy => policy.RequireRole("admin"))
            .AddEndpointFilter<FiltroErrores>();

        admin.MapGet("/", (ServicioAdministracion s) => Results.Ok(new
        {
            modo = s.Modo,
            prefijo = s.Prefijo,
            recursos = new[] { "colas", "topicos", "suscripciones", "topologia", "correos" }
        }));

        // ── Colas ──────────────────────────────────────────────────────────
        admin.MapGet("/colas", async (ServicioAdministracion s, CancellationToken ct) =>
            Results.Ok(await s.ListarColasAsync(ct)));

        admin.MapGet("/colas/{nombre}", async (string nombre, ServicioAdministracion s, CancellationToken ct) =>
            Results.Ok(await s.ObtenerColaAsync(nombre, ct)));

        admin.MapPost("/colas", async (CrearColaSolicitud solicitud, ServicioAdministracion s, CancellationToken ct) =>
        {
            var cola = await s.CrearColaAsync(solicitud, ct);
            return Results.Created($"{Ruta}/colas/{cola.Nombre}", cola);
        });

        admin.MapDelete("/colas/{nombre}", async (string nombre, bool? forzar, ServicioAdministracion s, CancellationToken ct) =>
        {
            await s.EliminarColaAsync(nombre, forzar ?? false, ct);
            return Results.NoContent();
        });

        // Ver lo que hay en una DLQ (o en la auditoria) sin borrarlo, y devolver una DLQ a su cola.
        admin.MapGet("/colas/{nombre}/mensajes", async (string nombre, int? maximo, ServicioAdministracion s, CancellationToken ct) =>
            Results.Ok(await s.VerMensajesAsync(nombre, maximo ?? 10, ct)));

        admin.MapPost("/colas/{nombre}/redrive", async (string nombre, string? destino, ServicioAdministracion s, CancellationToken ct) =>
            Results.Ok(await s.ReprocesarDlqAsync(nombre, destino, ct)));

        // ── Tópicos ────────────────────────────────────────────────────────
        admin.MapGet("/topicos", async (ServicioAdministracion s, CancellationToken ct) =>
            Results.Ok(await s.ListarTopicosAsync(ct)));

        admin.MapPost("/topicos", async (CrearTopicoSolicitud solicitud, ServicioAdministracion s, CancellationToken ct) =>
        {
            var topico = await s.CrearTopicoAsync(solicitud, ct);
            return Results.Created($"{Ruta}/topicos/{topico.Nombre}", topico);
        });

        admin.MapDelete("/topicos/{nombre}", async (string nombre, bool? forzar, ServicioAdministracion s, CancellationToken ct) =>
        {
            await s.EliminarTopicoAsync(nombre, forzar ?? false, ct);
            return Results.NoContent();
        });

        // ── Suscripciones (reglas de distribución) ─────────────────────────
        admin.MapGet("/suscripciones", async (ServicioAdministracion s, CancellationToken ct) =>
            Results.Ok(await s.ListarSuscripcionesAsync(ct)));

        admin.MapPost("/suscripciones", async (CrearSuscripcionSolicitud solicitud, ServicioAdministracion s, CancellationToken ct) =>
        {
            var sub = await s.CrearSuscripcionAsync(solicitud, ct);
            return Results.Created($"{Ruta}/suscripciones/{sub.Topico}/{sub.Cola}", sub);
        });

        admin.MapPut("/suscripciones/{topico}/{cola}",
            async (string topico, string cola, CrearSuscripcionSolicitud solicitud, ServicioAdministracion s, CancellationToken ct) =>
                Results.Ok(await s.ActualizarSuscripcionAsync(topico, cola, solicitud, ct)));

        admin.MapDelete("/suscripciones/{topico}/{cola}",
            async (string topico, string cola, bool? forzar, ServicioAdministracion s, CancellationToken ct) =>
            {
                await s.EliminarSuscripcionAsync(topico, cola, forzar ?? false, ct);
                return Results.NoContent();
            });

        // ── Topología: lo declarado contra lo que existe ───────────────────
        admin.MapGet("/topologia", async (ServicioAdministracion s, CancellationToken ct) =>
            Results.Ok(await s.CompararTopologiaAsync(ct)));

        admin.MapPost("/topologia/aplicar", async (ServicioAdministracion s, CancellationToken ct) =>
            Results.Ok(new { acciones = await s.AplicarTopologiaAsync(ct), estado = await s.CompararTopologiaAsync(ct) }));

        // ── Correos generados por notification-service (la bandeja) ────────
        BandejaCorreosProxy.Mapear(admin);
    }

    // Traduce las excepciones del negocio a respuestas con la misma forma que
    // el resto del gateway ({ estado, error, mensaje }). El 400 trae además la
    // lista { campo, error } para saber qué corregir. Nunca sale un ARN ni un
    // stack trace: eso queda en el log.
    private sealed class FiltroErrores(ILogger<FiltroErrores> log) : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
        {
            try
            {
                return await siguiente(contexto);
            }
            catch (SolicitudInvalidaException e)
            {
                var errores = e.Errores
                    .SelectMany(kv => kv.Value.Select(error => new { campo = kv.Key, error }))
                    .ToList();
                return Results.BadRequest(new
                {
                    estado = 400,
                    error = "Solicitud inválida",
                    mensaje = "Revisa los campos y vuelve a intentar.",
                    errores
                });
            }
            catch (RecursoNoEncontradoException e)
            {
                return Results.NotFound(new { estado = 404, error = "No existe", mensaje = e.Message });
            }
            catch (ConflictoException e)
            {
                return Results.Conflict(new { estado = 409, error = "Choca con el estado actual", mensaje = e.Message });
            }
            catch (ErrorProveedorException e)
            {
                log.LogError(e, "Error de AWS en {Ruta}", contexto.HttpContext.Request.Path);
                return Results.Json(new
                {
                    estado = 502,
                    error = e.PermisoDenegado ? "Permiso denegado por AWS" : "AWS no respondió como se esperaba",
                    mensaje = e.Message
                }, statusCode: StatusCodes.Status502BadGateway);
            }
        }
    }
}
