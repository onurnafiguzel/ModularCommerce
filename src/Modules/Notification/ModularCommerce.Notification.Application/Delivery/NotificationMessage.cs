namespace ModularCommerce.Notification.Application.Delivery;
public sealed record NotificationMessage(string Recipient, string Subject, string Body);
