using Product_Service.Exceptions;
using Product_Service.Models.Domain;
using Product_Service.Models.DTOs;
using Product_Service.Models.Entities;
using Product_Service.Models.Mappers;
using Product_Service.Repository;

namespace Product_Service.Service;

public sealed class ProductService(IProductRepository repository)
{
    public async Task<IReadOnlyList<ProductResponse>> GetAllProductsAsync()
    {
        var products = await repository.GetAllProductsAsync();
        return products.Select(product => product.ToResponse()).ToList();
    }

    public async Task<ProductResponse?> GetProductByIdAsync(int id)
    {
        ProductEntity? product = await repository.GetProductByIdAsync(id);
        return product?.ToResponse();
    }

    public async Task<string?> GetProductSlugByIdAsync(int id)
    {
        ProductEntity? product = await repository.GetProductByIdAsync(id);
        return product?.Slug;
    }

    public async Task<ProductResponse?> GetProductBySlugAsync(string slug)
    {
        string normalizedSlug = ProductIdentity.CreateSlug(slug);
        ProductEntity? product = await repository.GetProductBySlugAsync(normalizedSlug);
        return product?.ToResponse();
    }

    public async Task<ProductResponse> UpsertProductAsync(ProductRequest request)
    {
        ValidatedProduct input = Validate(request);
        ProductEntity? product = await repository.GetProductByCanonicalKeyAsync(input.CanonicalKey);

        if (product is null)
        {
            product = new ProductEntity
            {
                Slug = await CreateUniqueSlugAsync(input.Name),
                CanonicalKey = input.CanonicalKey,
                ProductName = input.Name,
                ProductBrand = input.Brand,
                ProductCategory = input.Category,
                BodyArea = input.BodyArea,
                Gender = input.Gender,
                Layer = input.Layer,
                Description = input.Description,
                ProductImage = input.Image,
                CreatedAt = DateTimeOffset.UtcNow,
                Offers = [CreateOffer(input)]
            };

            ProductEntity saved = await repository.AddProductAsync(product);
            return saved.ToResponse();
        }

        ProductOfferEntity? offer = FindOffer(product, input);

        if (offer is null)
        {
            product.Offers.Add(CreateOffer(input));
        }
        else
        {
            ApplyOffer(offer, input);
        }

        product.ProductCategory = input.Category;
        product.BodyArea = input.BodyArea;
        product.Gender = input.Gender;
        product.Layer = input.Layer;
        product.Description = Prefer(product.Description, input.Description);
        product.ProductImage = Prefer(product.ProductImage, input.Image);
        await repository.SaveChangesAsync();
        return product.ToResponse();
    }

    public async Task<bool> UpdateProductAsync(int id, ProductRequest request)
    {
        ProductEntity? product = await repository.GetProductByIdAsync(id);

        if (product is null)
        {
            return false;
        }

        ValidatedProduct input = Validate(request);
        ProductOfferEntity? offer = FindOffer(product, input);

        product.CanonicalKey = input.CanonicalKey;
        product.ProductName = input.Name;
        product.ProductBrand = input.Brand;
        product.ProductCategory = input.Category;
        product.BodyArea = input.BodyArea;
        product.Gender = input.Gender;
        product.Layer = input.Layer;
        product.Description = input.Description;
        product.ProductImage = input.Image;

        if (offer is null)
        {
            product.Offers.Add(CreateOffer(input));
        }
        else
        {
            ApplyOffer(offer, input);
        }

        await repository.SaveChangesAsync();
        return true;
    }

    public Task<bool> DeleteProductById(int id) => repository.DeleteProductByIdAsync(id);

    /// <summary>
    /// Suma una visita. Es anónimo a propósito: el contador vale para todos,
    /// con o sin sesión, y pedir login para contar una vista sería cobrar por
    /// algo que no se le da a nadie.
    /// </summary>
    public async Task<bool> RegisterVisitAsync(int id)
    {
        return await repository.IncrementVisitsAsync(id);
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsByCategoryAsync(string category)
    {
        var products = await repository.GetProductsByCategoryAsync(category);
        return products.Select(product => product.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsByPriceAsync(int price)
    {
        var products = await repository.GetProductsByPriceAsync(price);
        return products.Select(product => product.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsBySizeAsync(string size)
    {
        var products = await repository.GetProductsBySizeAsync(size);
        return products.Select(product => product.ToResponse()).ToList();
    }

    private static ProductOfferEntity? FindOffer(ProductEntity product, ValidatedProduct input)
    {
        return product.Offers.SingleOrDefault(existing =>
            string.Equals(existing.Store, input.Store, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.ExternalId, input.ExternalId, StringComparison.OrdinalIgnoreCase));
    }

    private static ValidatedProduct Validate(ProductRequest request)
    {
        string name = ProductIdentity.Required(request.Name, "name");
        string brand = ProductIdentity.Required(request.Brand, "brand");

        if (request.Price <= 0)
        {
            throw new PriceValueException("El precio no puede ser cero ni negativo.");
        }

        return new ValidatedProduct(
            string.IsNullOrWhiteSpace(request.CanonicalKey)
                ? ProductIdentity.CreateKey(brand, name)
                : ProductIdentity.NormalizeKey(request.CanonicalKey),
            ProductIdentity.Required(request.ExternalId, "externalId"),
            ProductIdentity.Required(request.Store, "store").ToLowerInvariant(),
            name,
            brand,
            ProductIdentity.Required(request.Category, "category"),
            string.IsNullOrWhiteSpace(request.BodyArea) ? "Cuerpo" : request.BodyArea.Trim(),
            string.IsNullOrWhiteSpace(request.Gender) ? "Unisex" : request.Gender.Trim(),
            string.IsNullOrWhiteSpace(request.Layer) ? "General" : request.Layer.Trim(),
            request.Price,
            ProductIdentity.NormalizeSizes(request.Sizes),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ProductIdentity.Required(request.Url, "url"),
            string.IsNullOrWhiteSpace(request.Image) ? null : request.Image.Trim(),
            request.Active
        );
    }

    private static ProductOfferEntity CreateOffer(ValidatedProduct input)
    {
        var offer = new ProductOfferEntity();
        ApplyOffer(offer, input);
        return offer;
    }

    private static void ApplyOffer(ProductOfferEntity offer, ValidatedProduct input)
    {
        offer.ExternalId = input.ExternalId;
        offer.Store = input.Store;
        offer.Price = input.Price;
        offer.Sizes = input.Sizes;
        offer.ProductUrl = input.Url;
        offer.ProductImage = input.Image;
        offer.Active = input.Active;
        offer.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? Prefer(string? current, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(current)) return candidate;
        if (string.IsNullOrWhiteSpace(candidate)) return current;
        return candidate.Length > current.Length ? candidate : current;
    }

    private async Task<string> CreateUniqueSlugAsync(string name)
    {
        const int maxLength = 180;
        string root = ProductIdentity.CreateSlug(name);
        string candidate = root;
        int suffix = 2;

        while (await repository.SlugExistsAsync(candidate))
        {
            string ending = $"-{suffix++}";
            candidate = $"{root[..Math.Min(root.Length, maxLength - ending.Length)].TrimEnd('-')}{ending}";
        }

        return candidate;
    }

    private sealed record ValidatedProduct(
        string CanonicalKey,
        string ExternalId,
        string Store,
        string Name,
        string Brand,
        string Category,
        string BodyArea,
        string Gender,
        string Layer,
        int Price,
        string[] Sizes,
        string? Description,
        string Url,
        string? Image,
        bool Active
    );
}
