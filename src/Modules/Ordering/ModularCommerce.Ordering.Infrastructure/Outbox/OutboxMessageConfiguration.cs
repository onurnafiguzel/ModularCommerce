using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModularCommerce.Ordering.Infrastructure.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Content).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.Error).HasMaxLength(2000);

        // Dispatcher'ın sıcak sorgusu bekleyenleri FIFO çeker; kısmi index yalnız
        // işlenmemiş satırları kapsar (tablo büyüse de sorgu ucuz kalır).
        builder.HasIndex(m => m.OccurredOnUtc)
            .HasDatabaseName("ix_outbox_unprocessed")
            .HasFilter("\"ProcessedOnUtc\" IS NULL");
    }
}
