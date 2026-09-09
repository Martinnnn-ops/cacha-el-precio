namespace Product_Service.Models.DTOs;

public sealed class ProductResponse
{
    public int Id { get; set; }
    public string CanonicalKey { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string? Description { get; set; }
    public string? Image { get; set; }
    public int Visits { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public IReadOnlyList<ProductOfferResponse> Offers { get; set; } = [];
}

public sealed class ProductOfferResponse
{
    public int Id { get; set; }
    public string ExternalId { get; set; } = null!;
    public string Store { get; set; } = null!;
    public int Price { get; set; }
    public IReadOnlyList<string> Sizes { get; set; } = [];
    public string Url { get; set; } = null!;
    public string? Image { get; set; }
    public bool Active { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
