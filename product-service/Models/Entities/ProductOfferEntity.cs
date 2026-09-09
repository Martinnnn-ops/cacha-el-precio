namespace Product_Service.Models.Entities;

public sealed class ProductOfferEntity
{
    public int OfferId { get; set; }
    public int ProductId { get; set; }
    public ProductEntity Product { get; set; } = null!;
    public string ExternalId { get; set; } = null!;
    public string Store { get; set; } = null!;
    public int Price { get; set; }
    public string[] Sizes { get; set; } = [];
    public string ProductUrl { get; set; } = null!;
    public string? ProductImage { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
