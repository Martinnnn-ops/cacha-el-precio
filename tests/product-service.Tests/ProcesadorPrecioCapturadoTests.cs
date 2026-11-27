using Product_Service.Exceptions;
using Product_Service.Streaming;

namespace Product_Service.Tests;

/// <summary>
/// La validación del sobre precio.capturado antes de tocar la base: un sobre que
/// no sirve es un error definitivo (va a la DLQ), no un reintento.
/// </summary>
public class ProcesadorPrecioCapturadoTests
{
    private const string Valido = """
        {
          "eventId": "e-1",
          "type": "precio.capturado",
          "version": 1,
          "source": "scraper-service",
          "occurredAt": "2026-11-24T12:00:00Z",
          "data": { "store": "paris", "externalId": "SKU-1", "price": 19990, "currency": "CLP", "capturedAt": "2026-11-24T12:00:00Z" }
        }
        """;

    [Fact]
    public void Un_sobre_valido_se_lee()
    {
        var sobre = ProcesadorPrecioCapturado.LeerSobre(Valido);
        Assert.Equal("e-1", sobre.EventId);
        Assert.Equal("precio.capturado", sobre.Type);
    }

    [Theory]
    [InlineData("esto no es json", "JSON del sobre inválido")]
    [InlineData("""{"type":"precio.capturado","version":1,"data":{}}""", "eventId")]
    [InlineData("""{"eventId":"e1","type":"oferta.capturada","version":1,"data":{}}""", "type 'oferta.capturada' desconocido")]
    [InlineData("""{"eventId":"e1","type":"precio.capturado","version":2,"data":{}}""", "versión 2")]
    public void Un_sobre_invalido_es_definitivo(string cuerpo, string razon)
    {
        var e = Assert.Throws<ErrorDefinitivoException>(() => ProcesadorPrecioCapturado.LeerSobre(cuerpo));
        Assert.Contains(razon, e.Razon);
    }
}
