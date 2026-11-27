using Product_Service.Models.Entities;

namespace Product_Service.Repository;


public interface IProductRepository
{
    Task<ProductEntity?> GetProductByOfferAsync(string store, string externalId);
    Task<IReadOnlyList<ProductEntity>> GetAllProductsAsync();

    Task<ProductEntity?> GetProductByIdAsync(int id);

    Task<ProductEntity?> GetProductBySlugAsync(string slug);

    Task<bool> SlugExistsAsync(string slug);

    Task<ProductEntity?> GetProductByCanonicalKeyAsync(string canonicalKey);

    Task<ProductEntity> AddProductAsync(ProductEntity product);

    Task SaveChangesAsync();

    Task<bool> DeleteProductByIdAsync(int id);

    /// <summary>
    /// Suma una visita al producto. Devuelve false si el producto no existe.
    /// </summary>
    Task<bool> IncrementVisitsAsync(int id);

    Task<IReadOnlyList<ProductEntity>> GetProductsByCategoryAsync(string category);

    Task<IReadOnlyList<ProductEntity>> GetProductsByPriceAsync(int price);

    Task<IReadOnlyList<ProductEntity>> GetProductsBySizeAsync(string size);

    /// <summary>Estadísticas de precio (precio habitual) de estos ids externos, en todas las tiendas.</summary>
    Task<IReadOnlyList<EstadisticaPrecio>> GetEstadisticasPrecioAsync(IReadOnlyCollection<string> externalIds);

    /// <summary>Precios capturados de estos ids externos desde una fecha, en todas las tiendas.</summary>
    Task<IReadOnlyList<ObservacionPrecio>> GetObservacionesPrecioAsync(IReadOnlyCollection<string> externalIds, DateTimeOffset desde);
}
