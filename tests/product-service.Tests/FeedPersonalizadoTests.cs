using Product_Service.Feed;
using Product_Service.Models.DTOs;

namespace Product_Service.Tests;

/// <summary>
/// El feed «Para ti» es una función pura: se prueba sin base de datos, armando
/// un catálogo y la lista de lo que la persona guardó.
/// </summary>
public class FeedPersonalizadoTests
{
    [Fact]
    public void Prioriza_el_genero_que_la_persona_guarda()
    {
        // Guarda una prenda de hombre. Dos candidatos de la misma categoría
        // (para aislar el género): uno de hombre, otro de mujer.
        var catalogo = new[]
        {
            P(1, genero: "Hombre", categoria: "Poleras"),
            P(10, genero: "Hombre", categoria: "Chaquetas"),
            P(20, genero: "Mujer", categoria: "Chaquetas"),
        };

        var feed = FeedPersonalizado.Rankear(catalogo, [1]).Select(p => p.Id).ToList();

        Assert.True(feed.IndexOf(10) < feed.IndexOf(20));
    }

    [Fact]
    public void Sube_la_categoria_que_la_persona_suele_guardar()
    {
        var catalogo = new[]
        {
            P(1, genero: "Unisex", categoria: "Zapatillas"),
            P(10, genero: "Unisex", categoria: "Zapatillas"),
            P(20, genero: "Unisex", categoria: "Poleras"),
        };

        var feed = FeedPersonalizado.Rankear(catalogo, [1]).Select(p => p.Id).ToList();

        Assert.True(feed.IndexOf(10) < feed.IndexOf(20));
    }

    [Fact]
    public void No_recomienda_lo_que_ya_esta_guardado()
    {
        var catalogo = new[] { P(1), P(2), P(3) };

        var feed = FeedPersonalizado.Rankear(catalogo, [1]);

        Assert.DoesNotContain(feed, p => p.Id == 1);
    }

    [Fact]
    public void Sin_senales_no_queda_vacio_y_prefiere_lo_comparable()
    {
        // Cuenta nueva, sin nada guardado: igual devuelve catálogo, y el que
        // está en dos tiendas va por delante del que no tiene oferta activa.
        var catalogo = new[]
        {
            P(10, tiendas: 2),
            P(20, tiendas: 1, activo: false),
        };

        var feed = FeedPersonalizado.Rankear(catalogo, []).Select(p => p.Id).ToList();

        Assert.NotEmpty(feed);
        Assert.True(feed.IndexOf(10) < feed.IndexOf(20));
    }

    [Fact]
    public void Respeta_el_limite()
    {
        var catalogo = Enumerable.Range(1, 5).Select(i => P(i)).ToArray();

        var feed = FeedPersonalizado.Rankear(catalogo, [], limite: 2);

        Assert.Equal(2, feed.Count);
    }

    private static ProductResponse P(
        int id, string genero = "Unisex", string categoria = "Poleras",
        string marca = "Marca", int precio = 20000, int tiendas = 1,
        int visitas = 0, bool activo = true) => new()
        {
            Id = id,
            Slug = $"p{id}",
            CanonicalKey = $"k{id}",
            Name = $"Producto {id}",
            Brand = marca,
            Category = categoria,
            BodyArea = "Torso",
            Gender = genero,
            Layer = "Base",
            Visits = visitas,
            CreatedAt = DateTimeOffset.UtcNow,
            Offers = Enumerable.Range(0, tiendas).Select(i => new ProductOfferResponse
            {
                Id = id * 10 + i,
                ExternalId = $"SKU{id}-{i}",
                Store = $"tienda{i}",
                Price = precio + i * 1000,
                Url = "https://tienda.cl/p",
                Active = activo,
                UpdatedAt = DateTimeOffset.UtcNow,
            }).ToList(),
        };
}
