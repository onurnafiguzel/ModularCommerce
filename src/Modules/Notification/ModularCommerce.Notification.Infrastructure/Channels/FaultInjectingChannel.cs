using ModularCommerce.Notification.Application.Abstractions;
using ModularCommerce.Notification.Application.Delivery;

namespace ModularCommerce.Notification.Infrastructure.Channels;

public sealed class FaultInjectingChannel(
    INotificationChannel inner,
    NotificationOptions options) : INotificationChannel
{
    public string Name => inner.Name;

    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        if (Random.Shared.NextDouble() < options.FailureRate)
        {
            throw new NotificationDeliveryException(
                $"[SİM] '{inner.Name}' kanalı teslim edemedi (FailureRate={options.FailureRate}).");
        }

        return inner.SendAsync(message, cancellationToken);
    }
}
