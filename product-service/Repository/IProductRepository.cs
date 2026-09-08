using Product_Service.Models.Entities;

namespace Product_Service.Repository;


public interface IProductRepository
{
    Task<IReadOnlyList<ProductEntity>> GetAllProductsAsync();

    Task<ProductEntity?> GetProductByIdAsync(int id);

    Task<ProductEntity> AddProductAsync(ProductEntity product);

    Task UpdateProductAsync(ProductEntity newProduct);

    Task<bool> DeleteProductByIdAsync(int id);

    Task<IReadOnlyList<ProductEntity>> GetProductsByCategoryAsync(string category);

    Task<IReadOnlyList<ProductEntity>> GetProductsByPriceAsync(int price);

    Task<IReadOnlyList<ProductEntity>> GetProductsBySizeAsync(string size);
}
