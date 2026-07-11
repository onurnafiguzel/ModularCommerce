using Microsoft.Extensions.Logging;
using ModularCommerce.Notification.Application.Abstractions;
using ModularCommerce.Notification.Application.Delivery;

namespace ModularCommerce.Notification.Infrastructure.Channels;

public sealed class WebhookNotificationChannel(
    NotificationOptions options,
    ILogger<WebhookNotificationChannel> logger) : INotificationChannel
{
    public string Name => "webhook";

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        if (options.LatencyMs > 0)
        {
            await Task.Delay(options.LatencyMs, cancellationToken);
        }

        logger.LogInformation(
            "[SİM] Webhook gönderildi → {Recipient}: {Subject}",
            message.Recipient, message.Subject);
    }
}
