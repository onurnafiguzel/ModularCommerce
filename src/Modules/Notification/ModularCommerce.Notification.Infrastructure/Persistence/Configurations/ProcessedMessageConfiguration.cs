using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularCommerce.Notification.Domain.Inbox;

namespace ModularCommerce.Notification.Infrastructure.Persistence.Configurations;

public sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    /// <summary>Idempotency hakemi olan bileşik PK'nın adı — eşzamanlı çift teslimde 23505 buradan gelir.</summary>
    public const string PrimaryKeyName = "pk_processed_messages";

    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("processed_messages");

        // Bileşik PK (iş anahtarı + tüketici) = gerçek idempotency hakemi: iki eşzamanlı
        // teslimden yalnız biri insert edebilir, diğeri 23505 alır (idempotent skip).
        builder.HasKey(p => new { p.IdempotencyKey, p.ConsumerType })
            .HasName(PrimaryKeyName);

        builder.Property(p => p.IdempotencyKey).HasMaxLength(200);
        builder.Property(p => p.ConsumerType).HasMaxLength(200);
    }
}
