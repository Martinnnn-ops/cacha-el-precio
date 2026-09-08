using Product_Service.Models.Domain;

namespace Product_Service.Models.Entities;


public class ProductEntity
{
    public int ProductId { get; set; }
    public string? ExternalId { get; set; }
    public string? Store { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductBrand { get; set; } = null!;
    public Category ProductCategory { get; set; } = null!;
    public int ProductPrice { get; set; }
    public ProductSizes ProductSizes { get; set; } = null!;
    public string? Description { get; set; }
    public string? ProductUrl { get; set; }
    public string? ProductImage { get; set; }
    public bool Active { get; set; } = true;
}
