using Product_Service.Mensajeria;
using Product_Service.Models.DTOs;
using Product_Service.Models.Entities;
using Product_Service.Models.Eventos;
using Product_Service.Repository;
using Product_Service.Service;

namespace Product_Service.Tests;

/// <summary>
/// Prueba el borde que la función pura no cubre: product-service solo publica
/// cuando la oferta <b>ya existía</b>. Una oferta nueva no anuncia nada. Usa un
/// repositorio y un publicador falsos, sin base de datos ni AWS.
/// </summary>
public class ProductServiceEventosTests
{
    [Fact]
    public async Task Oferta_nueva_no_publica_ningun_evento()
    {
        // Repositorio sin coincidencias => UpsertProductAsync crea el producto.
        var repo = new RepositorioFalso();
        var publicador = new PublicadorGrabador();
        var service = new ProductService(repo, publicador);

        await service.UpsertProductAsync(NuevaOferta(precio: 15000));

        Assert.Empty(publicador.Publicados);
    }

    [Fact]
    public async Task Oferta_existente_que_baja_publica_precio_bajo()
    {
        var existente = ProductoConOferta(productId: 123, precio: 20000, activa: true);
        var repo = new RepositorioFalso { PorOferta = existente };
        var publicador = new PublicadorGrabador();
        var service = new ProductService(repo, publicador);

        await service.UpsertProductAsync(NuevaOferta(precio: 15000));

        var (tipo, datos) = Assert.Single(publicador.Publicados);
        Assert.Equal(TiposEvento.PrecioBajo, tipo);
        Assert.Equal(123, datos.ProductId);
        Assert.Equal(25, datos.DiscountPct);
    }

    private static ProductRequest NuevaOferta(int precio) => new()
    {
        ExternalId = "SKU-1",
        Store = "tienda-ejemplo",
        Name = "Polera basica negra",
        Brand = "Marca",
        Category = "poleras",
        Gender = "unisex",
        Price = precio,
        Url = "https://tienda-ejemplo.cl/polera-basica-negra",
        Image = "https://tienda-ejemplo.cl/img.jpg",
        Active = true
    };

    private static ProductEntity ProductoConOferta(int productId, int precio, bool activa) => new()
    {
        ProductId = productId,
        Slug = "polera-basica-negra",
        CanonicalKey = "marca|polera-basica-negra",
        ProductName = "Polera basica negra",
        ProductBrand = "Marca",
        ProductCategory = "poleras",
        Gender = "unisex",
        Offers =
        [
            new ProductOfferEntity
            {
                OfferId = 1,
                ExternalId = "SKU-1",
                Store = "tienda-ejemplo",
                Price = precio,
                ProductUrl = "https://tienda-ejemplo.cl/polera-basica-negra",
                Active = activa
            }
        ]
    };

    /// <summary>Repositorio en memoria: devuelve lo que se le configure y no toca BD.</summary>
    private sealed class RepositorioFalso : IProductRepository
    {
        public ProductEntity? PorOferta { get; init; }
        public ProductEntity? PorClave { get; init; }

        public Task<ProductEntity?> GetProductByOfferAsync(string store, string externalId) =>
            Task.FromResult(PorOferta);
        public Task<ProductEntity?> GetProductByCanonicalKeyAsync(string canonicalKey) =>
            Task.FromResult(PorClave);
        public Task<bool> SlugExistsAsync(string slug) => Task.FromResult(false);
        public Task<ProductEntity> AddProductAsync(ProductEntity product)
        {
            product.ProductId = 777;
            return Task.FromResult(product);
        }
        public Task SaveChangesAsync() => Task.CompletedTask;

        // No se usan en estos tests.
        public Task<IReadOnlyList<ProductEntity>> GetAllProductsAsync() => throw new NotImplementedException();
        public Task<ProductEntity?> GetProductByIdAsync(int id) => throw new NotImplementedException();
        public Task<ProductEntity?> GetProductBySlugAsync(string slug) => throw new NotImplementedException();
        public Task<bool> DeleteProductByIdAsync(int id) => throw new NotImplementedException();
        public Task<bool> IncrementVisitsAsync(int id) => throw new NotImplementedException();
        public Task<IReadOnlyList<ProductEntity>> GetProductsByCategoryAsync(string category) => throw new NotImplementedException();
        public Task<IReadOnlyList<ProductEntity>> GetProductsByPriceAsync(int price) => throw new NotImplementedException();
        public Task<IReadOnlyList<ProductEntity>> GetProductsBySizeAsync(string size) => throw new NotImplementedException();
    }

    /// <summary>Publicador que guarda lo que se le pide publicar en vez de ir a SNS.</summary>
    private sealed class PublicadorGrabador : IPublicadorEventos
    {
        public List<(string Tipo, DatosCambioOferta Datos)> Publicados { get; } = [];
        public bool Habilitado => true;

        public Task PublicarCambioOfertaAsync(string tipo, DatosCambioOferta datos, CancellationToken ct = default)
        {
            Publicados.Add((tipo, datos));
            return Task.CompletedTask;
        }
    }
}
