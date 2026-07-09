using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularCommerce.Payment.Domain.Payments;
using PaymentAggregate = ModularCommerce.Payment.Domain.Payments.Payment;

namespace ModularCommerce.Payment.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentAggregate>
{
    public const string IdempotencyIndexName = "ix_payments_customer_id_idempotency_key";

    public void Configure(EntityTypeBuilder<PaymentAggregate> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.IdempotencyKey)
            .HasMaxLength(PaymentAggregate.IdempotencyKeyMaxLength)
            .IsRequired();

        // Double-charge'ın nihai hakemi (FR-6.2): aynı (müşteri, key) ikinci satır olamaz.
        builder.HasIndex(p => new { p.CustomerId, p.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName(IdempotencyIndexName);

        builder.HasIndex(p => p.OrderId);

        // Bayat-Pending takeover yarışının hakemi (Inventory'nin xmin deseni).
        builder.Property<uint>("xmin").IsRowVersion();

        builder.Property(p => p.Amount).HasColumnType("numeric(18,2)");
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();

        // Enum'lar string: audit tablosu insan gözüyle okunur olmalı (NFR-6.4).
        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(p => p.PspTransactionId).HasMaxLength(100);
        builder.Property(p => p.FailureCode).HasMaxLength(100);
        builder.Property(p => p.RefundTransactionId).HasMaxLength(100);

        // Denemeler owned + append-only: aggregate dışında yaşamları yok, aynı
        // SaveChanges'te atomik yazılır ve asla güncellenmez (NFR-6.4).
        builder.OwnsMany(p => p.Attempts, attempts =>
        {
            attempts.ToTable("payment_attempts");
            attempts.WithOwner().HasForeignKey("payment_id");
            attempts.Property<int>("id").ValueGeneratedOnAdd();
            attempts.HasKey("id");

            attempts.Property(a => a.Outcome).HasConversion<string>().HasMaxLength(20);
            attempts.Property(a => a.PspTransactionId).HasMaxLength(100);
            attempts.Property(a => a.ErrorCode).HasMaxLength(100);
        });

        builder.Ignore(p => p.LastActivityAtUtc);
        builder.Ignore(p => p.DomainEvents);
    }
}
