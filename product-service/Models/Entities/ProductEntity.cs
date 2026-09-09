namespace Product_Service.Models.Entities;

public sealed class ProductEntity
{
    public int ProductId { get; set; }
    public string CanonicalKey { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string ProductBrand { get; set; } = null!;
    public string ProductCategory { get; set; } = null!;
    public string? Description { get; set; }
    public string? ProductImage { get; set; }
    public List<ProductOfferEntity> Offers { get; set; } = [];
}
