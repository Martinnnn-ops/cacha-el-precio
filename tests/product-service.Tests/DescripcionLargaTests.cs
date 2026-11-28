using Microsoft.EntityFrameworkCore;
using Npgsql;
using Product_Service.Mensajeria;
using Product_Service.Service;

namespace Product_Service.Tests;

/// <summary>
/// Lo que destapó la trazabilidad (BITACORA 28-11): fichas de Paris con ~2.450
/// caracteres de descripción hacían que Postgres rechazara la oferta entera
/// (22001) y, como se trataba como recuperable, gastaba 5 reintentos y caía a la
/// DLQ. Ahora la descripción se recorta y, si otro texto no cabe, va directo a la
/// DLQ como error definitivo.
/// </summary>
public class DescripcionLargaTests
{
    private const int Tope = ProductService.LargoMaximoDescripcion;

    [Fact]
    public void Una_descripcion_corta_queda_igual()
    {
        Assert.Equal("Polera de algodón.", ProductService.RecortarDescripcion("  Polera de algodón.  "));
        Assert.Null(ProductService.RecortarDescripcion("   "));
        Assert.Null(ProductService.RecortarDescripcion(null));
    }

    [Fact]
    public void Una_de_exactamente_el_tope_no_se_toca()
    {
        var justa = new string('a', Tope);
        Assert.Equal(justa, ProductService.RecortarDescripcion(justa));
    }

    [Fact]
    public void Una_de_2460_caracteres_se_corta_en_el_ultimo_punto()
    {
        // Como la de "Zapato Casual Mujer 16 Hrs - J075": frases de marketing seguidas.
        var frase = "Estos zapatos destacan por su diseño moderno y versátil. ";
        var larga = string.Concat(Enumerable.Repeat(frase, 44)).Trim(); // ~2.500

        var recortada = ProductService.RecortarDescripcion(larga)!;

        Assert.True(larga.Length > Tope);
        Assert.True(recortada.Length <= Tope);
        Assert.EndsWith("versátil. …", recortada);
        Assert.StartsWith("Estos zapatos", recortada);
    }

    [Fact]
    public void Sin_puntos_se_corta_en_un_espacio()
    {
        var larga = string.Join(' ', Enumerable.Repeat("palabra", 400)); // 3.199

        var recortada = ProductService.RecortarDescripcion(larga)!;

        Assert.True(recortada.Length <= Tope);
        Assert.EndsWith("palabra…", recortada);
    }

    [Fact]
    public void Un_solo_bloque_sin_espacios_igual_respeta_el_tope()
    {
        var recortada = ProductService.RecortarDescripcion(new string('x', 5000))!;
        Assert.Equal(Tope, recortada.Length);
    }

    [Fact]
    public void El_22001_de_postgres_se_reconoce_aunque_venga_envuelto_por_EF()
    {
        var pg = new PostgresException("value too long for type character varying(2000)",
            "ERROR", "ERROR", PostgresErrorCodes.StringDataRightTruncation);
        var ef = new DbUpdateException("An error occurred while saving the entity changes.", pg);

        Assert.True(ProcesadorOfertaCapturada.EsTextoDemasiadoLargo(ef));
    }

    [Fact]
    public void Otros_errores_de_bd_siguen_siendo_recuperables()
    {
        var caida = new PostgresException("connection lost", "FATAL", "FATAL",
            PostgresErrorCodes.AdminShutdown);

        Assert.False(ProcesadorOfertaCapturada.EsTextoDemasiadoLargo(new DbUpdateException("x", caida)));
        Assert.False(ProcesadorOfertaCapturada.EsTextoDemasiadoLargo(new TimeoutException()));
    }
}
