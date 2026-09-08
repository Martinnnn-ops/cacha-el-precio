using Product_Service.Models.Domain;
using Product_Service.Models.DTOs;
using Product_Service.Models.Entities;
using Product_Service.Models.Mappers;
using Product_Service.Repository;

namespace Product_Service.Service;


public class ProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ProductResponse>> GetAllProductsAsync()
    {
        var products = await _repository.GetAllProductsAsync();
        return products.Select(p => p.ToResponse()).ToList();
    }

    public async Task<ProductResponse?> GetProductByIdAsync(int id)
    {
        ProductEntity? product = await _repository.GetProductByIdAsync(id);

        if (product == null) { return null; }

        return product.ToResponse();
    }

    public async Task<ProductResponse> CreateProductAsync(ProductRequest request)
    {
        Product productModel = new(
            request.Name, request.Brand, request.Category, request.Price, request.Sizes
        );

        ProductEntity productEntity = new()
        {
            ExternalId = request.ExternalId,
            Store = request.Store,
            ProductName = productModel.ProductName,
            ProductBrand = productModel.ProductBrand,
            ProductCategory = Category.From(productModel.ProductCategory),
            ProductPrice = productModel.ProductPrice,
            ProductSizes = productModel.ProductSizes,
            Description = request.Description,
            ProductUrl = request.Url,
            ProductImage = request.Image,
            Active = request.Active
        };

        ProductEntity saved = await _repository.AddProductAsync(productEntity);
        return saved.ToResponse();
    }

    public async Task<bool> UpdateProductAsync(int id, ProductRequest request)
    {
        ProductEntity? entity = await _repository.GetProductByIdAsync(id);

        if (entity is null) { return false; }

        Product updated = new(request.Name, request.Brand, request.Category, request.Price, request.Sizes);
        entity.ExternalId = request.ExternalId;
        entity.Store = request.Store;
        entity.ProductName = updated.ProductName;
        entity.ProductBrand = updated.ProductBrand;
        entity.ProductCategory = Category.From(updated.ProductCategory);
        entity.ProductPrice = updated.ProductPrice;
        entity.ProductSizes = updated.ProductSizes;
        entity.Description = request.Description;
        entity.ProductUrl = request.Url;
        entity.ProductImage = request.Image;
        entity.Active = request.Active;

        await _repository.UpdateProductAsync(entity);
        return true;
    }

    public async Task<bool> DeleteProductById(int id)
    {
        return await _repository.DeleteProductByIdAsync(id);
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsByCategoryAsync(string category)
    {
        var products = await _repository.GetProductsByCategoryAsync(category);

        return products.Select(p => p.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsByPriceAsync(int price)
    {
        var products = await _repository.GetProductsByPriceAsync(price);

        return products.Select(p => p.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsBySizeAsync(string size)
    {
        var products = await _repository.GetProductsBySizeAsync(size);

        return products.Select(p => p.ToResponse()).ToList();
    }
}
