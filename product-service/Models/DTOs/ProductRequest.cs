using Product_Service.Models.Domain;

namespace Product_Service.Models.DTOs;


public class ProductRequest
{
    public string? ExternalId { get; set; }
    public string? Store { get; set; }
    public string Name { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Category { get; set; } = null!;
    public int Price { get; set; }
    public ProductSizes Sizes { get; set; } = null!;
    public string? Description { get; set; }
    public string? Url { get; set; }
    public string? Image { get; set; }
    public bool Active { get; set; } = true;
}
