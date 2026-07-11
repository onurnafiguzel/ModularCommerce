namespace ModularCommerce.Notification.Domain.Notifications;
public sealed class NotificationLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string IdempotencyKey { get; init; }
    public required string Channel { get; init; }          // "email" | "webhook"
    public required string Recipient { get; init; }
    public required string Subject { get; init; }
    public Guid OrderId { get; init; }
    public DateTime SentAtUtc { get; init; }
}
