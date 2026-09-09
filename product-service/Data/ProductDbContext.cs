using Microsoft.EntityFrameworkCore;
using Product_Service.Models.Entities;

namespace Product_Service.Data;


public class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

    public DbSet<ProductEntity> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var product = modelBuilder.Entity<ProductEntity>();

        product.HasKey(p => p.ProductId);
        product.HasIndex(p => new { p.Store, p.ExternalId })
            .IsUnique()
            .HasFilter("\"Store\" IS NOT NULL AND \"ExternalId\" IS NOT NULL");
        product.Property(p => p.ExternalId).HasMaxLength(100);
        product.Property(p => p.Store).HasMaxLength(60);
        product.Property(p => p.ProductName).IsRequired().HasMaxLength(120);
        product.Property(p => p.ProductBrand).IsRequired().HasMaxLength(120);
        product.ComplexProperty(p => p.ProductCategory, category =>
        {
            category.Property(c => c.Type).HasConversion<string>();
            category.Property(c => c.OriginalValue);
        });
        product.Property(p => p.ProductPrice).IsRequired();
        product.Property(p => p.Description).HasMaxLength(2000);
        product.Property(p => p.ProductUrl).HasMaxLength(500);
        product.Property(p => p.ProductImage).HasMaxLength(500);
        product.Property(p => p.Active).IsRequired().HasDefaultValue(true);
        product.Property(p => p.Visits).IsRequired().HasDefaultValue(0);

        // La fecha la pone la APLICACIÓN al crear el producto, no la base.
        //
        // Lo natural sería un DEFAULT CURRENT_TIMESTAMP, pero SQLite no acepta
        // añadir una columna con un valor por defecto no constante: falla con
        // «Cannot add a column with non-constant default» y la migración no
        // corre. Es una limitación real del motor, no de EF Core, y un ejemplo
        // concreto de lo que cuesta SQLite frente a PostgreSQL —donde esto
        // habría funcionado tal cual—.
        //
        // Ponerlo en la aplicación tiene además una ventaja: el valor es
        // explícito y se puede probar sin base de datos. Y como todos los
        // productos entran por el mismo servicio, siguen fechándose con el
        // mismo reloj, que era lo que se buscaba.
        product.Property(p => p.CreatedAt).IsRequired();

        // «Lo más reciente» y «Lo más visto» ordenan por estas dos columnas.
        // Sin índice, cada consulta recorre la tabla entera; con 2.088
        // productos hoy no duele, pero el índice cuesta poco y la alternativa
        // es descubrirlo cuando el catálogo crezca.
        product.HasIndex(p => p.CreatedAt);
        product.HasIndex(p => p.Visits);
        product.OwnsOne(p => p.ProductSizes, sizes =>
        {
            sizes.Property(s => s.XS)
            .HasColumnName("XS");

            sizes.Property(s => s.S)
            .HasColumnName("S");

            sizes.Property(s => s.M)
            .HasColumnName("M");

            sizes.Property(s => s.L)
            .HasColumnName("L");

            sizes.Property(s => s.XL)
            .HasColumnName("XL");

            sizes.Property(s => s.XXL)
            .HasColumnName("XXL");
        });
    }
}
