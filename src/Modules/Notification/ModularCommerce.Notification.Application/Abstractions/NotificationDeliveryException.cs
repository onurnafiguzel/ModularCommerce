namespace ModularCommerce.Notification.Application.Abstractions;
public sealed class NotificationDeliveryException(string message) : Exception(message);
