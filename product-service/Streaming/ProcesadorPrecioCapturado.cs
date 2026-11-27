using Product_Service.Observabilidad;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Product_Service.Data;
using Product_Service.Exceptions;
using Product_Service.Mensajeria;
using Product_Service.Models.Entities;

namespace Product_Service.Streaming;

/// <summary>El <c>data</c> del evento precio.capturado v1 (lo escribe el scraper).</summary>
public sealed record PrecioCapturado(
    string? Store,
    string? ExternalId,
    int Price,
    string? Currency,
    DateTimeOffset CapturedAt);

/// <summary>
/// Lo que el lector necesita para procesar un registro. Es una costura: el lector
/// no depende de la base, y las pruebas le dan un doble que lanza los errores a
/// voluntad (misma idea que <c>IColaMensajes</c>).
/// </summary>
public interface IProcesadorPrecio
{
    Task<string> ProcesarAsync(string cuerpo, CancellationToken ct);
}

/// <summary>
/// El trabajo de un registro de cep-precios: validar el sobre, descartar
/// duplicados por eventId (idempotencia técnica) y sumar el precio a las
/// estadísticas de esa oferta. No sabe de Kinesis: recibe el cuerpo y lanza
/// <see cref="ErrorDefinitivoException"/> cuando el registro no sirve; cualquier
/// otra excepción (BD caída) es recuperable y el lector decide qué hacer.
///
/// Idempotente: un eventId ya visto no vuelve a sumar (la tabla de procesados),
/// así que un registro entregado dos veces no infla el promedio.
/// </summary>
public sealed class ProcesadorPrecioCapturado(ProductDbContext db, ILogger<ProcesadorPrecioCapturado> log)
    : IProcesadorPrecio
{
    public const string TipoPrecioCapturado = "precio.capturado";
    public const int VersionSoportada = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Procesa el registro y devuelve su eventId. Repetirlo no cambia nada.</summary>
    public async Task<string> ProcesarAsync(string cuerpo, CancellationToken ct)
    {
        SobreEntrante sobre = LeerSobre(cuerpo);
        PrecioCapturado datos = LeerDatos(sobre);

        using var _ = log.BeginScope("eventId={EventId}", sobre.EventId);
        using var __ = Correlacion.Iniciar(log, Correlacion.Validar(sobre.CorrelationId));

        if (await db.EventosPrecioProcesados.AnyAsync(e => e.EventId == sobre.EventId, ct))
        {
            log.LogDebug("Precio {EventId} ya estaba procesado; se ignora", sobre.EventId);
            return sobre.EventId!;
        }

        EstadisticaPrecio? est = await db.EstadisticasPrecio.FindAsync(
            [datos.Store, datos.ExternalId], ct);
        bool nueva = est is null;
        est ??= new EstadisticaPrecio { Store = datos.Store!, ExternalId = datos.ExternalId! };

        EstadisticasPrecioHabitual.Acumular(est, datos.Price, datos.CapturedAt);
        if (nueva) db.EstadisticasPrecio.Add(est);

        // La observación suelta, para el gráfico de la ficha. Si ya existe una
        // con la misma fecha exacta (otro eventId del mismo instante) no se
        // repite: la llave la rechazaría y el registro quedaría reintentándose.
        bool yaObservado = await db.ObservacionesPrecio.AnyAsync(o =>
            o.Store == datos.Store && o.ExternalId == datos.ExternalId && o.CapturadoEn == datos.CapturedAt, ct);
        if (!yaObservado)
        {
            db.ObservacionesPrecio.Add(new ObservacionPrecio
            {
                Store = datos.Store!,
                ExternalId = datos.ExternalId!,
                CapturadoEn = datos.CapturedAt,
                Precio = datos.Price
            });
        }

        db.EventosPrecioProcesados.Add(new EventoPrecioProcesado
        {
            EventId = sobre.EventId!,
            ProcesadoEn = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
        log.LogInformation("Precio {Store}/{ExternalId} = {Precio} sumado ({Muestras} muestras)",
            est.Store, est.ExternalId, datos.Price, est.Muestras);
        return sobre.EventId!;
    }

    public static SobreEntrante LeerSobre(string cuerpo)
    {
        SobreEntrante? sobre;
        try
        {
            sobre = JsonSerializer.Deserialize<SobreEntrante>(cuerpo, Json);
        }
        catch (JsonException e)
        {
            throw new ErrorDefinitivoException("JSON del sobre inválido", e);
        }

        if (sobre is null)
            throw new ErrorDefinitivoException("el sobre viene vacío");
        if (string.IsNullOrWhiteSpace(sobre.EventId))
            throw new ErrorDefinitivoException("el sobre no trae eventId");
        if (sobre.Type != TipoPrecioCapturado)
            throw new ErrorDefinitivoException($"type '{sobre.Type}' desconocido; este stream solo recibe {TipoPrecioCapturado}");
        if (sobre.Version != VersionSoportada)
            throw new ErrorDefinitivoException($"versión {sobre.Version} de {TipoPrecioCapturado} no soportada (se espera {VersionSoportada})");
        return sobre;
    }

    private static PrecioCapturado LeerDatos(SobreEntrante sobre)
    {
        if (sobre.Data.ValueKind != JsonValueKind.Object)
            throw new ErrorDefinitivoException("data debe ser un objeto con el precio capturado");

        PrecioCapturado datos;
        try
        {
            datos = sobre.Data.Deserialize<PrecioCapturado>(Json)
                ?? throw new ErrorDefinitivoException("data viene vacío");
        }
        catch (JsonException e)
        {
            throw new ErrorDefinitivoException($"data no calza con precio.capturado: {e.Message}", e);
        }

        if (string.IsNullOrWhiteSpace(datos.Store) || string.IsNullOrWhiteSpace(datos.ExternalId))
            throw new ErrorDefinitivoException("el precio no trae store o externalId");
        if (datos.Price <= 0)
            throw new ErrorDefinitivoException($"precio inválido: {datos.Price}");
        return datos;
    }
}
