using Product_Service.Models.Entities;

namespace Product_Service.Repository;


public interface IProductRepository
{
    Task<IReadOnlyList<ProductEntity>> GetAllProductsAsync();

    Task<ProductEntity?> GetProductByIdAsync(int id);

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
}
