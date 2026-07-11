using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularCommerce.Notification.Domain.Notifications;

namespace ModularCommerce.Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("notification_logs");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Channel).HasMaxLength(50).IsRequired();
        builder.Property(n => n.Recipient).HasMaxLength(320).IsRequired();
        builder.Property(n => n.Subject).HasMaxLength(500).IsRequired();

        // Audit sorgusu sipariş/idempotency-anahtarı bazlı okunur (dev audit endpoint).
        builder.HasIndex(n => n.IdempotencyKey);
        builder.HasIndex(n => n.OrderId);
    }
}
