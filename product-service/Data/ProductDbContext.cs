using Microsoft.EntityFrameworkCore;
using Product_Service.Models.Entities;

namespace Product_Service.Data;


public class ProductDbContext : DbContext
{
    public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options) { }

    public DbSet<ProductEntity> Products { get; set; }
    public DbSet<ProductOfferEntity> ProductOffers { get; set; }
    public DbSet<PersonalItem> PersonalItems { get; set; }

    // Stream de precios (EA3/EFT): el precio habitual, el checkpoint del lector de
    // Kinesis y los eventId ya sumados (idempotencia). Ver docs/PLAN-EFT.md §5.
    public DbSet<EstadisticaPrecio> EstadisticasPrecio { get; set; }
    public DbSet<ObservacionPrecio> ObservacionesPrecio { get; set; }
    public DbSet<EventoAuditoria> EventosAuditoria { get; set; }
    public DbSet<CheckpointStream> CheckpointsStream { get; set; }
    public DbSet<EventoPrecioProcesado> EventosPrecioProcesados { get; set; }

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

        var estadistica = modelBuilder.Entity<EstadisticaPrecio>();
        estadistica.HasKey(e => new { e.Store, e.ExternalId });
        estadistica.Property(e => e.Store).HasMaxLength(60);
        estadistica.Property(e => e.ExternalId).HasMaxLength(120);
        // Precios en pesos (enteros): la suma de cuadrados no lleva decimales.
        estadistica.Property(e => e.SumaCuadrados).HasPrecision(29, 0);

        // La llave cubre la consulta de la ficha (tienda + id, por fecha).
        var observacion = modelBuilder.Entity<ObservacionPrecio>();
        observacion.HasKey(o => new { o.Store, o.ExternalId, o.CapturadoEn });
        observacion.Property(o => o.Store).HasMaxLength(60);
        observacion.Property(o => o.ExternalId).HasMaxLength(120);

        // EventId como llave: la idempotencia técnica de la cola de auditoría.
        var auditoria = modelBuilder.Entity<EventoAuditoria>();
        auditoria.HasKey(e => e.EventId);
        auditoria.Property(e => e.EventId).HasMaxLength(64);
        auditoria.Property(e => e.Tipo).HasMaxLength(40);
        auditoria.Property(e => e.Slug).HasMaxLength(200);
        auditoria.Property(e => e.Nombre).HasMaxLength(300);
        auditoria.Property(e => e.Store).HasMaxLength(60);
        auditoria.Property(e => e.Motivo).HasMaxLength(200);
        auditoria.Property(e => e.CorrelationId).HasMaxLength(128);
        auditoria.HasIndex(e => e.RecibidoEn);
        auditoria.HasIndex(e => e.CorrelationId);

        var checkpoint = modelBuilder.Entity<CheckpointStream>();
        checkpoint.HasKey(c => c.StreamShard);
        checkpoint.Property(c => c.StreamShard).HasMaxLength(128);
        checkpoint.Property(c => c.NumeroSecuencia).IsRequired().HasMaxLength(128);

        var procesado = modelBuilder.Entity<EventoPrecioProcesado>();
        procesado.HasKey(e => e.EventId);
        procesado.Property(e => e.EventId).HasMaxLength(64);
        // Se consulta por fecha para podar los viejos.
        procesado.HasIndex(e => e.ProcesadoEn);
    }
}
