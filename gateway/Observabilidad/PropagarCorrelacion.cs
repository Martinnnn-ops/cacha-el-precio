namespace CachaElPrecio.Gateway.Observabilidad;

/// <summary>
/// Pasa el correlationId del pedido a cada llamada que el gateway hace hacia
/// atrás (product-service, notification-service): así sus logs llevan el mismo
/// número que los del gateway.
/// </summary>
public sealed class PropagarCorrelacion : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
    {
        if (Correlacion.Actual is { } id && !pedido.Headers.Contains(Correlacion.Encabezado))
            pedido.Headers.Add(Correlacion.Encabezado, id);
        return base.SendAsync(pedido, ct);
    }
}
