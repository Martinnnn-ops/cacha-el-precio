using Microsoft.EntityFrameworkCore;
using Product_Service.Data;
using Product_Service.Exceptions;
using Product_Service.Models.Domain;
using Product_Service.Models.Entities;

namespace Product_Service.Repository;

public sealed class ProductRepository(ProductDbContext context) : IProductRepository
{
    public async Task<IReadOnlyList<ProductEntity>> GetAllProductsAsync()
    {
        return await Query().AsNoTracking().OrderBy(product => product.ProductId).ToListAsync();
    }

    public async Task<ProductEntity?> GetProductByIdAsync(int id)
    {
        return await Query().SingleOrDefaultAsync(product => product.ProductId == id);
    }

    public async Task<ProductEntity?> GetProductByCanonicalKeyAsync(string canonicalKey)
    {
        return await Query().SingleOrDefaultAsync(product => product.CanonicalKey == canonicalKey);
    }

    public async Task<ProductEntity> AddProductAsync(ProductEntity product)
    {
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    public Task SaveChangesAsync() => context.SaveChangesAsync();

    public async Task<bool> DeleteProductByIdAsync(int id)
    {
        ProductEntity? product = await context.Products.SingleOrDefaultAsync(p => p.ProductId == id);

        if (product is null)
        {
            return false;
        }

        context.Products.Remove(product);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IncrementVisitsAsync(int id)
    {
        int updated = await context.Products
            .Where(product => product.ProductId == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(product => product.Visits, product => product.Visits + 1));
        return updated == 1;
    }

    public async Task<IReadOnlyList<ProductEntity>> GetProductsByCategoryAsync(string category)
    {
        string value = ProductIdentity.Required(category, "category");
        return await Query()
            .AsNoTracking()
            .Where(product => EF.Functions.ILike(product.ProductCategory, value))
            .OrderBy(product => product.ProductId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ProductEntity>> GetProductsByPriceAsync(int price)
    {
        if (price <= 0)
        {
            throw new PriceValueException("El precio no puede ser cero ni negativo.");
        }

        return await Query()
            .AsNoTracking()
            .Where(product => product.Offers.Any(offer => offer.Price == price && offer.Active))
            .OrderBy(product => product.ProductId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ProductEntity>> GetProductsBySizeAsync(string size)
    {
        string normalized = ProductIdentity.NormalizeSizes([size]).SingleOrDefault()
            ?? throw new ArgumentException("La talla no puede estar vacía.");

        return await Query()
            .AsNoTracking()
            .Where(product => product.Offers.Any(offer => offer.Active && offer.Sizes.Contains(normalized)))
            .OrderBy(product => product.ProductId)
            .ToListAsync();
    }

    private IQueryable<ProductEntity> Query()
    {
        return context.Products.Include(product => product.Offers);
    }
}
