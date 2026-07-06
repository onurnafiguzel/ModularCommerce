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

        // Hafta 9'daki TTL süpürme job'ı için: ürün + durum bazlı tarama.
        builder.HasIndex(r => new { r.ProductId, r.Status });

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.Ignore(r => r.DomainEvents);
    }
}
