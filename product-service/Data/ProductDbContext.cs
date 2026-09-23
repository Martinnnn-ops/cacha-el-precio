using Microsoft.EntityFrameworkCore;
using Product_Service.Models.Entities;

namespace Product_Service.Data;


public class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

    public DbSet<ProductEntity> Products { get; set; }
    public DbSet<ProductOfferEntity> ProductOffers { get; set; }
    public DbSet<PersonalItem> PersonalItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("product");

        var personal = modelBuilder.Entity<PersonalItem>();
        personal.HasKey(p => new { p.Owner, p.Kind, p.Key });
        personal.Property(p => p.Owner).HasMaxLength(128);
        personal.Property(p => p.Kind).HasMaxLength(20);
        personal.Property(p => p.Key).HasMaxLength(40);
        personal.Property(p => p.Payload).HasColumnType("jsonb");

        var product = modelBuilder.Entity<ProductEntity>();
        product.HasKey(p => p.ProductId);
        product.HasIndex(p => p.Slug).IsUnique();
        product.HasIndex(p => p.CanonicalKey).IsUnique();
        product.Property(p => p.Slug).IsRequired().HasMaxLength(180);
        product.Property(p => p.CanonicalKey).IsRequired().HasMaxLength(240);
        product.Property(p => p.ProductName).IsRequired().HasMaxLength(120);
        product.Property(p => p.ProductBrand).IsRequired().HasMaxLength(120);
        product.Property(p => p.ProductCategory).IsRequired().HasMaxLength(120);
        product.Property(p => p.BodyArea).IsRequired().HasMaxLength(40).HasDefaultValue("Cuerpo");
        product.Property(p => p.Gender).IsRequired().HasMaxLength(30).HasDefaultValue("Unisex");
        product.Property(p => p.Layer).IsRequired().HasMaxLength(40).HasDefaultValue("General");
        product.Property(p => p.Description).HasMaxLength(2000);
        product.Property(p => p.ProductImage).HasMaxLength(1000);
        product.Property(p => p.Visits).IsRequired().HasDefaultValue(0);
        product.Property(p => p.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        product.HasIndex(p => p.Visits);
        product.HasIndex(p => p.CreatedAt);
        product.HasMany(p => p.Offers)
            .WithOne(o => o.Product)
            .HasForeignKey(o => o.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        var offer = modelBuilder.Entity<ProductOfferEntity>();
        offer.HasKey(o => o.OfferId);
        offer.HasIndex(o => new { o.Store, o.ExternalId }).IsUnique();
        offer.Property(o => o.Store).IsRequired().HasMaxLength(60);
        offer.Property(o => o.ExternalId).IsRequired().HasMaxLength(120);
        offer.Property(o => o.Price).IsRequired();
        offer.Property(o => o.Sizes).HasColumnType("text[]");
        offer.Property(o => o.ProductUrl).IsRequired().HasMaxLength(1000);
        offer.Property(o => o.ProductImage).HasMaxLength(1000);
        offer.Property(o => o.Active).IsRequired().HasDefaultValue(true);
        offer.Property(o => o.UpdatedAt).IsRequired();
    }
}
