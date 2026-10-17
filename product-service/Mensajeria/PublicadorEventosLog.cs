using System.Text.Json;
using Product_Service.Models.Eventos;

namespace Product_Service.Mensajeria;

/// <summary>
/// Publicador de desarrollo: no toca AWS, solo escribe en el log el evento que
/// habría publicado. Se usa cuando no hay <c>SNS_CAMBIOS_OFERTA_ARN</c>, para
/// poder trabajar en local y ver el flujo 2 sin cuenta de AWS.
/// </summary>
public sealed class PublicadorEventosLog(ILogger<PublicadorEventosLog> logger) : IPublicadorEventos
{
    private static readonly JsonSerializerOptions JsonOpciones = new(JsonSerializerDefaults.Web);

    public bool Habilitado => false;

    public Task PublicarCambioOfertaAsync(string tipo, DatosCambioOferta datos, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[sin SNS] Publicaría {Tipo} del producto {ProductId}: {Data}",
            tipo, datos.ProductId, JsonSerializer.Serialize(datos, JsonOpciones));
        return Task.CompletedTask;
    }
}
