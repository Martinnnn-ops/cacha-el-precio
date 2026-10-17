using Microsoft.EntityFrameworkCore;
using Notification_Service.Models.Entities;

namespace Notification_Service.Data;

// Esquema propio del servicio. No toca el esquema `product`: los seguidores se
// piden por HTTP a product-service (ver ADR-030).
public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<NotificacionEnviada> NotificacionesEnviadas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("notification");

        var enviada = modelBuilder.Entity<NotificacionEnviada>();
        enviada.ToTable("notificaciones_enviadas");
        enviada.HasKey(e => new { e.EventId, e.Owner });
        enviada.Property(e => e.EventId).HasColumnName("event_id");
        enviada.Property(e => e.Owner).HasColumnName("owner").HasMaxLength(128);
        enviada.Property(e => e.Tipo).HasColumnName("tipo").HasMaxLength(40).IsRequired();
        enviada.Property(e => e.EnviadoEn).HasColumnName("enviado_en").IsRequired();
    }
}
