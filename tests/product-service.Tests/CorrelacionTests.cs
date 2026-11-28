using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Product_Service.Mensajeria;
using Product_Service.Models.Eventos;
using Product_Service.Observabilidad;

namespace Product_Service.Tests;

/// <summary>
/// El correlationId del barrido entra con la oferta y sale con el cambio que
/// se publica en SNS (PLAN-EFT §3).
/// </summary>
public class CorrelacionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void El_sobre_entrante_trae_el_correlation_id()
    {
        var sobre = ProcesadorOfertaCapturada.LeerSobre("""
            {"eventId":"e-1","type":"oferta.capturada","version":1,"source":"scraper",
             "occurredAt":"2026-11-28T00:00:00Z","data":{},"correlationId":"barrido-paris-1"}
            """);

        Assert.Equal("barrido-paris-1", sobre.CorrelationId);
    }

    [Fact]
    public void Un_sobre_viejo_sin_correlation_id_sigue_sirviendo()
    {
        var sobre = ProcesadorOfertaCapturada.LeerSobre("""
            {"eventId":"e-1","type":"oferta.capturada","version":1,"source":"scraper",
             "occurredAt":"2026-11-28T00:00:00Z","data":{}}
            """);

        Assert.Null(sobre.CorrelationId);
    }

    [Fact]
    public void El_sobre_que_sale_lleva_el_id_solo_si_hay_uno()
    {
        var con = new SobreEvento("e-2", "precio.bajo", 1, "product-service", DateTimeOffset.UnixEpoch, new { }, "barrido-hites-1");
        var sin = new SobreEvento("e-3", "precio.bajo", 1, "product-service", DateTimeOffset.UnixEpoch, new { });

        Assert.Contains("\"correlationId\":\"barrido-hites-1\"", JsonSerializer.Serialize(con, Json));
        Assert.DoesNotContain("correlationId", JsonSerializer.Serialize(sin, Json));
    }

    [Fact]
    public void Iniciar_deja_el_id_como_actual_y_lo_restaura_al_soltarlo()
    {
        Assert.Null(Correlacion.Actual);
        using (Correlacion.Iniciar(NullLogger.Instance, "barrido-paris-2"))
        {
            Assert.Equal("barrido-paris-2", Correlacion.Actual);
        }
        Assert.Null(Correlacion.Actual);
    }
}
