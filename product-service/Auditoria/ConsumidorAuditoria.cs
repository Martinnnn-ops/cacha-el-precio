using Product_Service.Mensajeria;

namespace Product_Service.Auditoria;

// Lee cep-auditoria-precios (flujo 2): la copia de TODOS los cambios de oferta
// que reparte SNS. Hasta el 28-11 nadie la leia y SQS la vaciaba a los 4 dias.
// El bucle, el ACK, la DLQ y los reintentos estan en ConsumidorSqs.
public sealed class ConsumidorAuditoria(
    [FromKeyedServices(ConsumidorAuditoria.Cola)] IColaMensajes cola,
    IServiceScopeFactory scopes,
    ILogger<ConsumidorAuditoria> log) : ConsumidorSqs(cola, scopes, log, "cep-auditoria-precios")
{
    public const string Cola = "auditoria";

    protected override Task<string> ProcesarAsync(IServiceProvider servicios, string cuerpo, CancellationToken ct) =>
        servicios.GetRequiredService<ProcesadorAuditoria>().ProcesarAsync(cuerpo, ct);
}
