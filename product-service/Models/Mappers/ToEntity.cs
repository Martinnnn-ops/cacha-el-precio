using Product_Service.Models.Domain;
using Product_Service.Models.Entities;

namespace Product_Service.Models.Mappers;


public static class ProductToEntity
{
    public static ProductEntity ToEntity(this Product product)
    {
        return new ProductEntity
        {
            ProductName = product.ProductName,
            ProductBrand = product.ProductBrand,
            ProductCategory = Category.From(product.ProductCategory),
            ProductPrice = product.ProductPrice,
            ProductSizes = product.ProductSizes,
            Active = true
        };
    }
}
