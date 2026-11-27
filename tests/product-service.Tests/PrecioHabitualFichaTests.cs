using Product_Service.Models.Entities;
using Product_Service.Models.Mappers;
using Product_Service.Streaming;

namespace Product_Service.Tests;

/// <summary>
/// Que el precio habitual llegue a la respuesta de la ficha: cada oferta
/// trae su rango y dónde cae su precio actual.
/// </summary>
public class PrecioHabitualFichaTests
{
    private static ProductEntity Producto(params (string Store, string Id, int Precio)[] ofertas) => new()
    {
        ProductId = 1,
        Slug = "polera-basica",
        CanonicalKey = "polera-basica",
        ProductName = "Polera básica",
        ProductBrand = "Marca",
        ProductCategory = "Poleras",
        Offers = ofertas.Select((o, i) => new ProductOfferEntity
        {
            OfferId = i + 1,
            ExternalId = o.Id,
            Store = o.Store,
            Price = o.Precio,
            ProductUrl = $"https://{o.Store}.cl/{o.Id}"
        }).ToList()
    };

    private static EstadisticaPrecio Estadistica(string store, string id, params int[] precios)
    {
        var est = new EstadisticaPrecio { Store = store, ExternalId = id };
        var t = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        foreach (int p in precios)
        {
            EstadisticasPrecioHabitual.Acumular(est, p, t);
            t = t.AddHours(3);
        }
        return est;
    }

    [Fact]
    public void Sin_estadisticas_la_oferta_no_trae_habitual()
    {
        var r = Producto(("paris", "A1", 10000)).ToResponse();

        Assert.Null(r.Offers.Single().Habitual);
    }

    [Fact]
    public void Con_muestras_suficientes_dice_si_el_precio_esta_bajo_lo_habitual()
    {
        var est = Estadistica("paris", "A1", 20000, 21000, 19000, 20000, 22000, 18000);
        var stats = new Dictionary<(string, string), EstadisticaPrecio>
        {
            [EntityToResponse.Clave("paris", "A1")] = est
        };

        var habitual = Producto(("paris", "A1", 12000)).ToResponse(stats).Offers.Single().Habitual;

        Assert.NotNull(habitual);
        Assert.True(habitual.Suficiente);
        Assert.Equal("bajo", habitual.Ubicacion);
        Assert.Equal(6, habitual.Muestras);
        Assert.True(habitual.Piso <= habitual.Techo);
    }

    [Fact]
    public void Con_pocas_muestras_queda_como_insuficiente()
    {
        var stats = new Dictionary<(string, string), EstadisticaPrecio>
        {
            [EntityToResponse.Clave("hites", "B2")] = Estadistica("hites", "B2", 15000, 16000)
        };

        var habitual = Producto(("hites", "B2", 15000)).ToResponse(stats).Offers.Single().Habitual;

        Assert.NotNull(habitual);
        Assert.False(habitual.Suficiente);
        Assert.Equal("insuficiente", habitual.Ubicacion);
    }

    [Fact]
    public void La_tienda_se_cruza_sin_importar_mayusculas()
    {
        var stats = new Dictionary<(string, string), EstadisticaPrecio>
        {
            [EntityToResponse.Clave("sparta", "C3")] = Estadistica("sparta", "C3", 1, 2, 3, 4, 5)
        };

        var habitual = Producto(("Sparta", "C3", 3)).ToResponse(stats).Offers.Single().Habitual;

        Assert.NotNull(habitual);
        Assert.Equal("dentro", habitual.Ubicacion);
    }
}

public class HistorialFichaTests
{
    private static ObservacionPrecio Obs(string store, string id, string cuando, int precio) => new()
    {
        Store = store,
        ExternalId = id,
        CapturadoEn = DateTimeOffset.Parse(cuando),
        Precio = precio
    };

    private static ProductEntity Producto() => new()
    {
        ProductId = 1,
        Slug = "p",
        CanonicalKey = "p",
        ProductName = "P",
        ProductBrand = "M",
        ProductCategory = "C",
        Offers =
        [
            new ProductOfferEntity { ExternalId = "A1", Store = "paris", Price = 1, ProductUrl = "u" },
            new ProductOfferEntity { ExternalId = "S9", Store = "sparta", Price = 1, ProductUrl = "u" }
        ]
    };

    [Fact]
    public void Un_punto_por_dia_con_el_ultimo_precio_de_cada_tienda()
    {
        var h = Product_Service.Service.ProductService.HistorialPorDia(Producto(),
        [
            Obs("paris", "A1", "2026-11-01T12:00:00-03:00", 20000),
            Obs("paris", "A1", "2026-11-01T20:00:00-03:00", 18000),
            Obs("sparta", "S9", "2026-11-01T15:00:00-03:00", 25000),
            Obs("paris", "A1", "2026-11-02T09:00:00-03:00", 19000),
        ]);

        Assert.Equal(2, h.Count);
        Assert.Equal("2026-11-01", h[0].Fecha);
        Assert.Equal(18000, h[0].Precios["paris"]);
        Assert.Equal(25000, h[0].Precios["sparta"]);
        Assert.False(h[1].Precios.ContainsKey("sparta"));
    }

    [Fact]
    public void Ignora_observaciones_de_otra_tienda_con_el_mismo_id()
    {
        var h = Product_Service.Service.ProductService.HistorialPorDia(Producto(),
        [
            Obs("hites", "A1", "2026-11-01T12:00:00-03:00", 1000),
        ]);

        Assert.Empty(h);
    }

    [Fact]
    public void El_dia_es_el_de_Chile_no_el_de_UTC()
    {
        // 22:00 en Chile del 1-nov = 01:00 UTC del 2-nov.
        var h = Product_Service.Service.ProductService.HistorialPorDia(Producto(),
        [
            Obs("paris", "A1", "2026-11-02T01:00:00Z", 20000),
        ]);

        Assert.Equal("2026-11-01", h.Single().Fecha);
    }
}
