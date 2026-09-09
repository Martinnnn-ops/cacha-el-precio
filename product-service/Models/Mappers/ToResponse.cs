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
            CanonicalKey = product.CanonicalKey,
            Name = product.ProductName,
            Brand = product.ProductBrand,
            Category = product.ProductCategory,
            Description = product.Description,
            Image = product.ProductImage,
            Visits = product.Visits,
            CreatedAt = product.CreatedAt,
            Offers = product.Offers
                .OrderBy(offer => offer.Price)
                .Select(offer => new ProductOfferResponse
                {
                    Id = offer.OfferId,
                    ExternalId = offer.ExternalId,
                    Store = offer.Store,
                    Price = offer.Price,
                    Sizes = offer.Sizes,
                    Url = offer.ProductUrl,
                    Image = offer.ProductImage,
                    Active = offer.Active,
                    UpdatedAt = offer.UpdatedAt
                })
                .ToList()
        };
    }
}
