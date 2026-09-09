namespace Product_Service.Models.DTOs;

public sealed class ProductRequest
{
    public string? CanonicalKey { get; set; }
    public string ExternalId { get; set; } = null!;
    public string Store { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Category { get; set; } = null!;
    public int Price { get; set; }
    public string[] Sizes { get; set; } = [];
    public string? Description { get; set; }
    public string Url { get; set; } = null!;
    public string? Image { get; set; }
    public bool Active { get; set; } = true;
}
