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

    // Cuántas veces se abrió la ficha. El frontend lo cuenta una vez por día y
    // por navegador, así que no es una métrica exacta de personas: es la señal
    // que ordena «Lo más visto» en la portada.
    //
    // Se incrementa con una sentencia UPDATE ... SET Visits = Visits + 1, no
    // leyendo el valor y volviéndolo a escribir. Con lectura y escritura por
    // separado, dos visitas simultáneas leen el mismo número y guardan el
    // mismo resultado: una de las dos se pierde.
    public int Visits { get; set; }

    // Cuándo entró el producto al catálogo. Alimenta «Lo más reciente» y la
    // antigüedad que muestra la ficha.
    //
    // La pone el servicio al crear, y NO viene en ProductRequest a propósito:
    // si el cliente pudiera mandarla, el scraper —o cualquiera con el scope de
    // ingesta— podría fechar un producto en el futuro y quedarse para siempre
    // el primer puesto de «Lo más reciente».
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
