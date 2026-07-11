namespace ModularCommerce.Notification.Application.Delivery;
public sealed record NotificationInstruction(
    string IdempotencyKey,
    string ConsumerType,
    Guid OrderId,
    Guid? MessageId,
    NotificationMessage Message);
