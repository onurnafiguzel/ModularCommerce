using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularCommerce.Ordering.Domain.Orders;

namespace ModularCommerce.Ordering.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public const string IdempotencyIndexName = "ix_orders_customer_id_idempotency_key";

    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.IdempotencyKey)
            .HasMaxLength(Order.IdempotencyKeyMaxLength)
            .IsRequired();

        builder.HasIndex(o => new { o.CustomerId, o.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName(IdempotencyIndexName);

        builder.HasIndex(o => o.CustomerId);

        // Satırlar ve durum geçmişi owned: aggregate dışında yaşamları yok,
        // Order ile aynı SaveChanges'te atomik yazılırlar (NFR-5.4).
        builder.OwnsMany(o => o.Lines, lines =>
        {
            lines.ToTable("order_lines");
            lines.WithOwner().HasForeignKey("order_id");
            lines.Property<int>("id").ValueGeneratedOnAdd();
            lines.HasKey("id");

            lines.Property(l => l.ProductName).HasMaxLength(200).IsRequired();
            lines.Property(l => l.UnitPrice).HasColumnType("numeric(18,2)");
            lines.Property(l => l.Currency).HasMaxLength(3).IsRequired();
        });

        builder.OwnsMany(o => o.StatusHistory, history =>
        {
            history.ToTable("order_status_history");
            history.WithOwner().HasForeignKey("order_id");
            history.Property<int>("id").ValueGeneratedOnAdd();
            history.HasKey("id");

            history.Property(h => h.TriggeredBy).HasMaxLength(50).IsRequired();
        });

        // Türetilmiş değerler kolon olarak tutulmaz.
        builder.Ignore(o => o.TotalAmount);
        builder.Ignore(o => o.Currency);

        builder.Ignore(o => o.DomainEvents);
    }
}
