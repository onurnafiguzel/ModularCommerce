using ModularCommerce.Notification.Application.Delivery;

namespace ModularCommerce.Notification.Application.Abstractions;
public interface INotificationChannel
{
    string Name { get; }   // "email" | "webhook"
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}
