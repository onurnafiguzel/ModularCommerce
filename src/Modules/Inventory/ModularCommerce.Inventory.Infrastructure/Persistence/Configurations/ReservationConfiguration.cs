using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularCommerce.Inventory.Domain.Stock;

namespace ModularCommerce.Inventory.Infrastructure.Persistence.Configurations;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");

        builder.HasKey(r => r.Id);

        // Ürün + durum bazlı tarama (genel).
        builder.HasIndex(r => new { r.ProductId, r.Status });

        // TTL süpürücüsünün hot-key sorgusu (Active + süresi geçmiş): kısmi index yalnız
        // Active satırları kapsar → süpürme taraması, dev/committed satırlar büyüse de ucuz kalır.
        builder.HasIndex(r => r.ExpiresAtUtc)
            .HasDatabaseName("ix_reservations_active_expiry")
            .HasFilter("\"Status\" = 'Active'");

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.Ignore(r => r.DomainEvents);
    }
}
