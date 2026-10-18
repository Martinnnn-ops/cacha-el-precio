using System.Text.Json;
using Product_Service.Exceptions;
using Product_Service.Models.DTOs;
using Product_Service.Models.Domain;
using Product_Service.Service;

namespace Product_Service.Mensajeria;

// El sobre tal como llega de la cola (docs/MENSAJERIA.md). `data` se deja como
// JsonElement para leerlo segun el `type` recien despues de validarlo.
public sealed record SobreEntrante(
    string? EventId,
    string? Type,
    int Version,
    string? Source,
    DateTimeOffset? OccurredAt,
    JsonElement Data);

// El trabajo de un mensaje de cep-ofertas-crudas: validar el sobre, leer la
// oferta y guardarla con UpsertProductAsync, el mismo camino que POST
// /api/products. No sabe de SQS: recibe el cuerpo y lanza
// ErrorDefinitivoException cuando el mensaje no sirve. Cualquier otra excepcion
// (BD caida, timeout) es recuperable y el consumidor no borra el mensaje.
//
// Idempotente: UpsertProductAsync + los UNIQUE de (Store, ExternalId) y
// CanonicalKey hacen que un duplicado de SQS no cambie nada.
public sealed class ProcesadorOfertaCapturada(ProductService productos, ILogger<ProcesadorOfertaCapturada> log)
{
    public const string TipoOfertaCapturada = "oferta.capturada";
    public const int VersionSoportada = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<string> ProcesarAsync(string cuerpo, CancellationToken ct)
    {
        SobreEntrante sobre = LeerSobre(cuerpo);
        ProductRequest oferta = LeerOferta(sobre);

        using var _ = log.BeginScope("eventId={EventId}", sobre.EventId);

        // Las mismas reglas del POST: el controlador revisa la politica de edad
        // antes de llamar al servicio, asi que aca tambien.
        if (CatalogPolicy.IsChild(oferta.Name ?? "", $"{oferta.Category} {oferta.Gender}"))
            throw new ErrorDefinitivoException("politica: el catalogo es para adolescentes y adultos");

        try
        {
            var producto = await productos.UpsertProductAsync(oferta);
            log.LogInformation("Oferta {Store}/{ExternalId} guardada en el producto {ProductId}",
                oferta.Store, oferta.ExternalId, producto.Id);
            return sobre.EventId!;
        }
        catch (Exception e) when (e is ArgumentException or PriceValueException)
        {
            // Validacion del catalogo: reintentar no va a cambiar el precio ni el nombre.
            throw new ErrorDefinitivoException($"validacion: {e.Message}", e);
        }
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
            throw new ErrorDefinitivoException("JSON del sobre invalido", e);
        }

        if (sobre is null)
            throw new ErrorDefinitivoException("el sobre viene vacio");
        if (string.IsNullOrWhiteSpace(sobre.EventId))
            throw new ErrorDefinitivoException("el sobre no trae eventId");
        if (sobre.Type != TipoOfertaCapturada)
            throw new ErrorDefinitivoException($"type '{sobre.Type}' desconocido; esta cola solo recibe {TipoOfertaCapturada}");
        if (sobre.Version != VersionSoportada)
            throw new ErrorDefinitivoException($"version {sobre.Version} de {TipoOfertaCapturada} no soportada (se espera {VersionSoportada})");
        return sobre;
    }

    private static ProductRequest LeerOferta(SobreEntrante sobre)
    {
        if (sobre.Data.ValueKind != JsonValueKind.Object)
            throw new ErrorDefinitivoException("data debe ser un objeto con la oferta");

        try
        {
            return sobre.Data.Deserialize<ProductRequest>(Json)
                ?? throw new ErrorDefinitivoException("data viene vacio");
        }
        catch (JsonException e)
        {
            throw new ErrorDefinitivoException($"data no calza con ProductRequest: {e.Message}", e);
        }
    }
}
