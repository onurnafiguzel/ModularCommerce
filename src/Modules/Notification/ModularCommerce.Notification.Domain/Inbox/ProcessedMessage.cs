namespace ModularCommerce.Notification.Domain.Inbox;
public sealed class ProcessedMessage
{
    public required string IdempotencyKey { get; init; }   // "OrderPaid:{OrderId}"
    public required string ConsumerType { get; init; }     // nameof(OrderPaidNotificationConsumer)
    public DateTime ProcessedOnUtc { get; init; }
    public Guid? MessageId { get; init; }                  // MassTransit MessageId — yalnız audit
}
