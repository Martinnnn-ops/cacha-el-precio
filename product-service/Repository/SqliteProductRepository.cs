using Microsoft.EntityFrameworkCore;
using Product_Service.Data;
using Product_Service.Exceptions;
using Product_Service.Models.Domain;
using Product_Service.Models.Entities;

namespace Product_Service.Repository;


public class SqliteProductRepository : IProductRepository
{
    private readonly ProductDbContext _context;

    public SqliteProductRepository(ProductDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProductEntity>> GetAllProductsAsync()
    {
        return await _context.Products
            .AsNoTracking()
            .OrderBy(p => p.ProductId)
            .ToListAsync();
    }

    public async Task<ProductEntity?> GetProductByIdAsync(int id)
    {
        return await _context.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.ProductId == id);
    }

    public async Task<ProductEntity> AddProductAsync(ProductEntity product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return product;
    }

    public async Task UpdateProductAsync(ProductEntity product)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> DeleteProductByIdAsync(int id)
    {
        ProductEntity? product = await _context.Products.SingleOrDefaultAsync(p => p.ProductId == id);

        if (product == null)
        {
            return false;
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<ProductEntity>> GetProductsByCategoryAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("La categoria escrita no puede ser null o estar vacia.");
        }

        return await _context.Products
            .AsNoTracking()
            .Where(p => p.ProductCategory.Type == Category.From(category).Type)
            .OrderBy(p => p.ProductId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ProductEntity>> GetProductsByPriceAsync(int price)
    {
        if (price <= 0)
        {
            throw new PriceValueException("El precio no puede ser cero ni negativo.");
        }

        return await _context.Products
            .AsNoTracking()
            .Where(p => p.ProductPrice == price)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ProductEntity>> GetProductsBySizeAsync(string size)
    {
        if (string.IsNullOrWhiteSpace(size))
        {
            throw new ArgumentException(
                "La talla de filtrado no puede ser null ni estar vacía."
            );
        }

        if (!Enum.TryParse<Size>(size, true, out var parsedSize))
        {
            throw new ArgumentException(
                $"La talla '{size}' no es válida."
            );
        }

        IQueryable<ProductEntity> query =
            _context.Products.AsNoTracking();

        query = parsedSize switch
        {
            Size.XS => query.Where(p => p.ProductSizes.XS),
            Size.S => query.Where(p => p.ProductSizes.S),
            Size.M => query.Where(p => p.ProductSizes.M),
            Size.L => query.Where(p => p.ProductSizes.L),
            Size.XL => query.Where(p => p.ProductSizes.XL),
            Size.XXL => query.Where(p => p.ProductSizes.XXL),

            _ => throw new ArgumentException("Talla no soportada.")
        };

        return await query.ToListAsync();
    }
}
