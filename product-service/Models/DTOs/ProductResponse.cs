using Product_Service.Models.Domain;

namespace Product_Service.Models.DTOs;


public class ProductResponse
{
    public int Id { get; set; }
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
    public bool Active { get; set; }

    // Los consume la portada: `Visits` ordena «Lo más visto» y `CreatedAt`
    // ordena «Lo más reciente» y calcula la antigüedad de la ficha. Hasta el
    // 09-09 ninguno de los dos existía y las dos secciones ordenaban por nada.
    public int Visits { get; set; }
    public DateTime CreatedAt { get; set; }
}
