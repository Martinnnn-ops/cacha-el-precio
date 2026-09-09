using Product_Service.Models.DTOs;
using Product_Service.Models.Entities;

namespace Product_Service.Models.Mappers;


public static class EntityToResponse
{
    public static ProductResponse ToResponse(this ProductEntity product)
    {
        return new ProductResponse
        {
            Id = product.ProductId,
            ExternalId = product.ExternalId,
            Store = product.Store,
            Name = product.ProductName,
            Brand = product.ProductBrand,
            Category = product.ProductCategory.GetStringValue(),
            Price = product.ProductPrice,
            Sizes = product.ProductSizes,
            Description = product.Description,
            Url = product.ProductUrl,
            Image = product.ProductImage,
            Active = product.Active,
            Visits = product.Visits,

            // Se marca explícitamente como UTC antes de serializar.
            //
            // El valor se guarda en UTC (`DateTime.UtcNow`), pero SQLite lo
            // devuelve con Kind = Unspecified, y entonces System.Text.Json lo
            // escribe SIN la «Z» final: "2026-09-09T03:49:04". Un navegador
            // interpreta una marca ISO sin zona como HORA LOCAL, así que en
            // Chile (UTC−3) un producto creado hace un minuto parecía creado
            // tres horas en el futuro, y la antigüedad salía negativa.
            //
            // El frontend lo detectaba y mostraba «sin dato» en vez de mentir,
            // que es lo correcto de su parte — pero el dato estaba mal aquí.
            CreatedAt = DateTime.SpecifyKind(product.CreatedAt, DateTimeKind.Utc)
        };
    }
}
