namespace Product_Service.Mensajeria;

// Lee cep-ofertas-crudas (flujo 1 del EP3) y guarda cada oferta con
// ProcesadorOfertaCapturada. El bucle, el ACK, la DLQ y los reintentos estan
// en ConsumidorSqs. Mismo patron que ConsumidorNotificaciones.
public sealed class ConsumidorOfertas(
    IColaMensajes cola,
    IServiceScopeFactory scopes,
    ILogger<ConsumidorOfertas> log) : ConsumidorSqs(cola, scopes, log, "cep-ofertas-crudas")
{
    protected override Task<string> ProcesarAsync(IServiceProvider servicios, string cuerpo, CancellationToken ct) =>
        servicios.GetRequiredService<ProcesadorOfertaCapturada>().ProcesarAsync(cuerpo, ct);
}
