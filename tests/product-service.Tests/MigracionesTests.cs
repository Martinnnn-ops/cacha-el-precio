using Microsoft.EntityFrameworkCore;
using Product_Service.Data;

namespace Product_Service.Tests;

/// <summary>
/// El modelo de EF y el snapshot de migraciones deben calzar. Si no, al arrancar
/// product-service, <c>Migrate()</c> se niega con "pending model changes" y el
/// servicio no levanta. No necesita base: compara el modelo contra el snapshot.
/// </summary>
public class MigracionesTests
{
    [Fact]
    public void El_modelo_no_tiene_cambios_sin_migracion()
    {
        var opciones = new DbContextOptionsBuilder<ProductDbContext>()
            .UseNpgsql("Host=localhost;Database=solo-modelo")
            .Options;
        using var db = new ProductDbContext(opciones);

        Assert.False(db.Database.HasPendingModelChanges());
    }
}
