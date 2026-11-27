using Product_Service.Auditoria;
using Product_Service.Exceptions;
using Product_Service.Mensajeria;

namespace Product_Service.Tests;

/// <summary>
/// Lo que se guarda de cada cambio en la cola de auditoría (PLAN-EFT §4): sus
/// precios, el correlationId del barrido y si es sospechoso.
/// </summary>
public class AuditoriaTests
{
    private static SobreEntrante Sobre(string tipo, string data, string? cid = null) =>
        ProcesadorAuditoria.LeerSobre($$"""
            {"eventId":"e-1","type":"{{tipo}}","version":1,"source":"product-service",
             "occurredAt":"2026-11-28T00:00:00Z","data":{{data}}{{(cid is null ? "" : $",\"correlationId\":\"{cid}\"")}}}
            """);

    [Fact]
    public void Una_baja_normal_no_es_sospechosa()
    {
        var e = ProcesadorAuditoria.Armar(Sobre("precio.bajo",
            """{"productId":7,"store":"paris","previousPrice":20000,"newPrice":15000,"discountPct":25}"""));

        Assert.False(e.Sospechoso);
        Assert.Equal(20000, e.PrecioAnterior);
        Assert.Equal(15000, e.PrecioNuevo);
        Assert.Equal(25, e.DescuentoPct);
    }

    [Fact]
    public void Una_baja_de_70_por_ciento_o_mas_es_sospechosa()
    {
        var e = ProcesadorAuditoria.Armar(Sobre("precio.bajo",
            """{"productId":7,"store":"paris","previousPrice":29990,"newPrice":2999,"discountPct":90}"""));

        Assert.True(e.Sospechoso);
        Assert.Contains("90%", e.Motivo);
    }

    [Fact]
    public void Sin_descuento_declarado_se_calcula_de_los_precios()
    {
        var e = ProcesadorAuditoria.Armar(Sobre("precio.bajo",
            """{"productId":7,"store":"hites","previousPrice":10000,"newPrice":2000}"""));

        Assert.Equal(80, e.DescuentoPct);
        Assert.True(e.Sospechoso);
    }

    [Fact]
    public void Un_agotado_se_guarda_con_su_ultimo_precio_y_no_es_sospechoso()
    {
        var e = ProcesadorAuditoria.Armar(Sobre("oferta.agotada",
            """{"productId":7,"store":"paris","lastPrice":19990}"""));

        Assert.False(e.Sospechoso);
        Assert.Equal(19990, e.PrecioAnterior);
    }

    [Fact]
    public void Guarda_el_correlation_id_del_barrido()
    {
        var e = ProcesadorAuditoria.Armar(Sobre("oferta.repuesta",
            """{"productId":7,"store":"paris","price":19990}""", "barrido-paris-20261128T000245Z"));

        Assert.Equal("barrido-paris-20261128T000245Z", e.CorrelationId);
    }

    [Fact]
    public void Un_tipo_que_no_es_cambio_de_oferta_es_error_definitivo() =>
        Assert.Throws<ErrorDefinitivoException>(() => Sobre("oferta.capturada", "{}"));

    [Fact]
    public void Un_data_que_no_calza_es_error_definitivo()
    {
        Assert.Throws<ErrorDefinitivoException>(() => ProcesadorAuditoria.Armar(Sobre("precio.bajo",
            """{"productId":"no-es-numero"}""")));
    }
}
