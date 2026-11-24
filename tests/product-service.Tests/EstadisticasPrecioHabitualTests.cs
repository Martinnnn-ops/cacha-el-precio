using Product_Service.Models.Entities;
using Product_Service.Streaming;

namespace Product_Service.Tests;

/// <summary>
/// El cálculo del precio habitual (EA3/EFT), en funciones puras. Sin base ni
/// Kinesis: solo aritmética sobre las muestras acumuladas.
/// </summary>
public class EstadisticasPrecioHabitualTests
{
    private static EstadisticaPrecio ConMuestras(params int[] precios)
    {
        var est = new EstadisticaPrecio { Store = "paris", ExternalId = "SKU-1" };
        var t = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        foreach (int p in precios)
        {
            EstadisticasPrecioHabitual.Acumular(est, p, t);
            t = t.AddHours(3);
        }
        return est;
    }

    [Fact]
    public void Acumular_lleva_cuenta_suma_minimo_y_maximo()
    {
        var est = ConMuestras(10000, 20000, 15000);

        Assert.Equal(3, est.Muestras);
        Assert.Equal(45000, est.Suma);
        Assert.Equal(10000, est.Minimo);
        Assert.Equal(20000, est.Maximo);
    }

    [Fact]
    public void El_ultimo_precio_es_el_de_la_captura_mas_reciente_no_el_ultimo_en_llegar()
    {
        var est = new EstadisticaPrecio { Store = "paris", ExternalId = "SKU-1" };
        var hoy = new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero);
        var ayer = hoy.AddDays(-1);

        EstadisticasPrecioHabitual.Acumular(est, 19990, hoy);
        EstadisticasPrecioHabitual.Acumular(est, 25000, ayer); // llega después, pero es más viejo

        Assert.Equal(19990, est.UltimoPrecio);
        Assert.Equal(hoy, est.UltimaCaptura);
    }

    [Fact]
    public void Con_pocas_muestras_el_rango_es_insuficiente()
    {
        var est = ConMuestras(10000, 11000, 12000); // 3 < MuestrasMinimas (5)

        Assert.False(EstadisticasPrecioHabitual.Rango(est).Suficiente);
        Assert.Equal(UbicacionPrecio.Insuficiente, EstadisticasPrecioHabitual.Clasificar(est, 1));
    }

    [Fact]
    public void Un_precio_bajo_el_piso_es_una_buena_oferta()
    {
        // Habitual ~20000; una captura muy barata cae bajo el piso.
        var est = ConMuestras(20000, 20000, 20000, 20000, 19000, 21000);

        Assert.Equal(UbicacionPrecio.Bajo, EstadisticasPrecioHabitual.Clasificar(est, 12000));
    }

    [Fact]
    public void Un_precio_sobre_el_techo_es_caro()
    {
        var est = ConMuestras(20000, 20000, 20000, 20000, 19000, 21000);

        Assert.Equal(UbicacionPrecio.Sobre, EstadisticasPrecioHabitual.Clasificar(est, 30000));
    }

    [Fact]
    public void Un_precio_en_el_medio_esta_dentro_de_lo_habitual()
    {
        var est = ConMuestras(18000, 19000, 20000, 21000, 22000, 20000);

        Assert.Equal(UbicacionPrecio.Dentro, EstadisticasPrecioHabitual.Clasificar(est, 20000));
    }

    [Fact]
    public void El_rango_nunca_se_sale_de_lo_visto()
    {
        var est = ConMuestras(18000, 19000, 20000, 21000, 22000);
        RangoHabitual rango = EstadisticasPrecioHabitual.Rango(est);

        Assert.True(rango.Piso >= est.Minimo);
        Assert.True(rango.Techo <= est.Maximo);
    }
}
