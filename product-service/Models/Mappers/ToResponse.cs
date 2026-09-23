using Product_Service.Models.DTOs;
using Product_Service.Models.Entities;

namespace Product_Service.Models.Mappers;


public static class EntityToResponse
{
    public static ProductResponse ToResponse(this ProductEntity product)
    {
        return new ProductResponse
        {
            Id = product.ProductId,
            Slug = product.Slug,
            CanonicalKey = product.CanonicalKey,
            Name = product.ProductName,
            Brand = Product_Service.Models.Domain.CatalogPolicy.Brand(product.ProductBrand),
            Category = product.ProductCategory,
            BodyArea = product.BodyArea,
            Gender = product.Gender,
            Layer = product.Layer,
            Description = product.Description,
            Image = product.ProductImage,
            Visits = product.Visits,
            CreatedAt = product.CreatedAt,
            Offers = product.Offers
                .GroupBy(offer => offer.Store, StringComparer.OrdinalIgnoreCase)
                .Select(ToStoreOffer)
                .OrderBy(offer => offer.Price)
                .ToList()
        };
    }

    private static ProductOfferResponse ToStoreOffer(IGrouping<string, ProductOfferEntity> group)
    {
        List<ProductOfferEntity> offers = group.ToList();
        ProductOfferEntity representative = offers
            .Where(offer => offer.Active)
            .DefaultIfEmpty(offers.OrderBy(offer => offer.Price).First())
            .OrderBy(offer => offer.Price)
            .First();
        List<ProductOfferEntity> available = offers.Where(offer => offer.Active).ToList();
        IReadOnlyList<ProductOfferEntity> sizeSources = available.Count > 0 ? available : offers;

        return new ProductOfferResponse
        {
            Id = representative.OfferId,
            ExternalId = representative.ExternalId,
            Store = representative.Store,
            Price = representative.Price,
            Sizes = sizeSources
                .SelectMany(offer => offer.Sizes ?? [])
                .Where(size => !string.IsNullOrWhiteSpace(size))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(size => size)
                .ToList(),
            Url = representative.ProductUrl,
            Image = representative.ProductImage
                ?? offers.Select(offer => offer.ProductImage).FirstOrDefault(image => image is not null),
            Active = available.Count > 0,
            UpdatedAt = offers.Max(offer => offer.UpdatedAt)
        };
    }
}
