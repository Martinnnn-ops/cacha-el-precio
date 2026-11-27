namespace Product_Service.Models.DTOs;

public sealed class ProductResponse
{
    public int Id { get; set; }
    public string Slug { get; set; } = null!;
    public string CanonicalKey { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string BodyArea { get; set; } = null!;
    public string Gender { get; set; } = null!;
    public string Layer { get; set; } = null!;
    public string? Description { get; set; }
    public string? Image { get; set; }
    public int Visits { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public IReadOnlyList<ProductOfferResponse> Offers { get; set; } = [];

    /// <summary>
    /// Un punto por día con el último precio de cada tienda (la llave es la
    /// tienda en minúsculas). Solo viene en la ficha; en el listado va vacío.
    /// </summary>
    public IReadOnlyList<HistorialDiaResponse> Historial { get; set; } = [];
}

public sealed class HistorialDiaResponse
{
    /// <summary>Día en hora de Chile, "aaaa-mm-dd".</summary>
    public string Fecha { get; set; } = null!;
    public IReadOnlyDictionary<string, int> Precios { get; set; } = new Dictionary<string, int>();
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

    /// <summary>
    /// Rango habitual del precio en esta tienda (lo calcula el lector de
    /// Kinesis). Solo viene en la ficha; null si aún no hay ninguna muestra.
    /// </summary>
    public PrecioHabitualResponse? Habitual { get; set; }
}

/// <summary>
/// Dónde cae el precio actual respecto de lo habitual. Con menos de
/// 5 muestras, <c>Suficiente</c> es false y <c>Ubicacion</c> es "insuficiente":
/// la ficha debe decir "historial insuficiente" en vez de mostrar un rango.
/// </summary>
public sealed class PrecioHabitualResponse
{
    public int Piso { get; set; }
    public int Techo { get; set; }
    public bool Suficiente { get; set; }

    /// <summary>"bajo", "dentro", "sobre" o "insuficiente".</summary>
    public string Ubicacion { get; set; } = null!;

    public long Muestras { get; set; }
    public DateTimeOffset UltimaCaptura { get; set; }
}
