using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularCommerce.Ordering.Domain.Orders;

namespace ModularCommerce.Ordering.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public const string IdempotencyIndexName = "ix_orders_customer_id_idempotency_key";

    /// <summary>
    /// Order.Id artık (CustomerId, IdempotencyKey)'den DETERMİNİSTİK üretiliyor
    /// (<see cref="Order.DeterministicOrderId"/>'nin dolaylı sonucu) — aynı key için yarışan
    /// paralel denemeler AYNI Id'yi hesapladığından, bir yarış kaybı artık PK ihlali olarak da
    /// gelebilir (önceden SADECE <see cref="IdempotencyIndexName"/> ihlal edilirdi). İsim adı
    /// açıkça sabitlenir ki EF'in konvansiyonu değişse bile <see cref="OrderRepository"/>'nin
    /// exception-translation filtresi kırılmasın.
    /// </summary>
    public const string PrimaryKeyName = "PK_orders";

    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id).HasName(PrimaryKeyName);

        builder.Property(o => o.IdempotencyKey)
            .HasMaxLength(Order.IdempotencyKeyMaxLength)
            .IsRequired();

        // Nullable: migration-öncesi kayıtlarda NULL kalır (geriye dönük uyumluluk, backfill yok).
        builder.Property(o => o.PayloadHash)
            .HasMaxLength(80); // "v1:" + 64 hex karakter

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

            // Money value object → aynı iki kolon (UnitPrice/Currency): kolon adı/türü
            // korunduğu için migration üretmez. Owned koleksiyon içinde ComplexProperty
            // desteklenmediğinden Money iç owned tip (OwnsOne) olarak aynı tabloya eşlenir.
            lines.OwnsOne(l => l.UnitPrice, money =>
            {
                money.Property(m => m.Amount).HasColumnName("UnitPrice").HasColumnType("numeric(18,2)");
                money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired();
            });
            lines.Navigation(l => l.UnitPrice).IsRequired();

            // Türetilmiş satır toplamı kolon olarak tutulmaz.
            lines.Ignore(l => l.LineTotal);
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
