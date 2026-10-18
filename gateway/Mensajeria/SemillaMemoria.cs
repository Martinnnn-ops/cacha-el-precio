
namespace CachaElPrecio.Gateway.Mensajeria;

// Datos de arranque del modo memoria: la topologia aplicada y dos fallos de
// ejemplo en cada DLQ, con los mismos atributos de diagnostico que dejan los
// consumidores reales (docs/MENSAJERIA.md, "Confirmacion, reintentos y errores").
public static class SemillaMemoria
{
    public static async Task SembrarAsync(IServiceProvider servicios)
    {
        using var scope = servicios.CreateScope();
        var servicio = scope.ServiceProvider.GetRequiredService<ServicioAdministracion>();
        var memoria = scope.ServiceProvider.GetRequiredService<AdministradorMensajeriaMemoria>();

        await servicio.AplicarTopologiaAsync(CancellationToken.None);

        string ahora = DateTimeOffset.UtcNow.ToString("O");
        memoria.Sembrar("cep-ofertas-crudas-dlq",
            """{"eventId":"5f0c2d1e-0000-4000-8000-000000000001","type":"oferta.capturada","version":1,"source":"scraper-service","data":{"store":"Paris","externalId":"P-123","price":-1}}""",
            new Dictionary<string, string>
            {
                ["error-type"] = "definitivo",
                ["error-reason"] = "validacion: price debe ser > 0",
                ["error-exception"] = "ArgumentException",
                ["failed-by"] = "product-service",
                ["failed-at"] = ahora,
                ["original-message-id"] = "semilla-1"
            });
        memoria.Sembrar("cep-ofertas-crudas-dlq",
            """{"eventId":"5f0c2d1e-0000-4000-8000-000000000002","type":"oferta.capturada","version":2,"source":"scraper-service","data":{}}""",
            new Dictionary<string, string>
            {
                ["error-type"] = "definitivo",
                ["error-reason"] = "version 2 de oferta.capturada no soportada",
                ["error-exception"] = "ErrorDefinitivoException",
                ["failed-by"] = "product-service",
                ["failed-at"] = ahora,
                ["original-message-id"] = "semilla-2"
            });
        memoria.Sembrar("cep-notificaciones-dlq",
            """{"eventId":"5f0c2d1e-0000-4000-8000-000000000003","type":"precio.bajo","version":1,"data":{"productId":42,"discountPct":25}}""",
            new Dictionary<string, string>
            {
                ["error-type"] = "definitivo",
                ["error-reason"] = "correo de destino invalido",
                ["error-exception"] = "FormatException",
                ["failed-by"] = "notification-service",
                ["failed-at"] = ahora,
                ["original-message-id"] = "semilla-3"
            });
    }
}
