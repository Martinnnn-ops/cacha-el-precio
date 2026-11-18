using Product_Service.Mensajeria;
using Product_Service.Models.Eventos;

namespace Product_Service.Tests;

/// <summary>
/// Prueba la decisión de qué eventos de cambio de oferta corresponden. Es una
/// función pura (sin EF ni AWS), así que estos tests corren sin base de datos ni
/// credenciales. Cubre los casos de la tarjeta T07: baja, subida, agotada,
/// repuesta, baja y agotada a la vez, y sin cambios.
/// </summary>
public class DecisionCambiosOfertaTests
{
    private static DatosCambioOferta Comunes() => new()
    {
        ProductId = 123,
        Slug = "polera-negra",
        Name = "Polera negra",
        Image = "https://tienda-ejemplo.cl/img.jpg",
        Store = "tienda-ejemplo",
        Url = "https://tienda-ejemplo.cl/polera-negra"
    };

    [Fact]
    public void Baja_de_precio_publica_precio_bajo_con_descuento()
    {
        var eventos = DecisionCambiosOferta.Evaluar(Comunes(), 20000, true, 15000, true);

        var (tipo, datos) = Assert.Single(eventos);
        Assert.Equal(TiposEvento.PrecioBajo, tipo);
        Assert.Equal(20000, datos.PreviousPrice);
        Assert.Equal(15000, datos.NewPrice);
        Assert.Equal(25, datos.DiscountPct);
        // No se cuelan los campos de los otros tipos.
        Assert.Null(datos.LastPrice);
        Assert.Null(datos.Price);
    }

    [Fact]
    public void Subida_de_precio_no_publica_nada()
    {
        var eventos = DecisionCambiosOferta.Evaluar(Comunes(), 15000, true, 20000, true);

        Assert.Empty(eventos);
    }

    [Fact]
    public void Se_agota_publica_oferta_agotada_con_last_price()
    {
        var eventos = DecisionCambiosOferta.Evaluar(Comunes(), 15000, true, 15000, false);

        var (tipo, datos) = Assert.Single(eventos);
        Assert.Equal(TiposEvento.OfertaAgotada, tipo);
        Assert.Equal(15000, datos.LastPrice);
        Assert.Null(datos.PreviousPrice);
        Assert.Null(datos.Price);
    }

    [Fact]
    public void Se_repone_publica_oferta_repuesta_con_price()
    {
        var eventos = DecisionCambiosOferta.Evaluar(Comunes(), 15000, false, 15000, true);

        var (tipo, datos) = Assert.Single(eventos);
        Assert.Equal(TiposEvento.OfertaRepuesta, tipo);
        Assert.Equal(15000, datos.Price);
        Assert.Null(datos.LastPrice);
        Assert.Null(datos.PreviousPrice);
    }

    [Fact]
    public void Baja_y_se_agota_a_la_vez_publica_los_dos_eventos()
    {
        var eventos = DecisionCambiosOferta.Evaluar(Comunes(), 20000, true, 15000, false);

        Assert.Equal(2, eventos.Count);

        var baja = Assert.Single(eventos, e => e.Tipo == TiposEvento.PrecioBajo);
        Assert.Equal(25, baja.Datos.DiscountPct);

        var agotada = Assert.Single(eventos, e => e.Tipo == TiposEvento.OfertaAgotada);
        Assert.Equal(15000, agotada.Datos.LastPrice);
    }

    [Fact]
    public void Sin_cambios_no_publica_nada()
    {
        var eventos = DecisionCambiosOferta.Evaluar(Comunes(), 15000, true, 15000, true);

        Assert.Empty(eventos);
    }

    [Theory]
    [InlineData(20000, 15000, 25)]   // 25% exacto
    [InlineData(3000, 2000, 33)]     // 33,33 -> 33 (redondea hacia abajo)
    [InlineData(1200, 1000, 17)]     // 16,66 -> 17 (redondea hacia arriba)
    public void DiscountPct_se_redondea_al_entero(int anterior, int nuevo, int esperado)
    {
        var eventos = DecisionCambiosOferta.Evaluar(Comunes(), anterior, true, nuevo, true);

        var (_, datos) = Assert.Single(eventos);
        Assert.Equal(esperado, datos.DiscountPct);
    }
}
