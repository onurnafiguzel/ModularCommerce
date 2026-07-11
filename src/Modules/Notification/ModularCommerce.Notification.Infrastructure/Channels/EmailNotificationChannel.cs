using Microsoft.Extensions.Logging;
using ModularCommerce.Notification.Application.Abstractions;
using ModularCommerce.Notification.Application.Delivery;

namespace ModularCommerce.Notification.Infrastructure.Channels;
public sealed class EmailNotificationChannel(
    NotificationOptions options,
    ILogger<EmailNotificationChannel> logger) : INotificationChannel
{
    public string Name => "email";

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        if (options.LatencyMs > 0)
        {
            await Task.Delay(options.LatencyMs, cancellationToken);
        }

        logger.LogInformation(
            "[SİM] E-posta gönderildi → {Recipient}: {Subject}",
            message.Recipient, message.Subject);
    }
}
