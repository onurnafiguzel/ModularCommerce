using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularCommerce.Notification.Infrastructure.Persistence;

namespace ModularCommerce.Notification.Api.Endpoints;

public static class NotificationDevEndpoints
{
    public static void MapNotificationDevEndpoints(this IEndpointRouteBuilder group, bool isDevelopment)
    {
        if (!isDevelopment)
        {
            return;
        }

        group.MapGet("/dev/logs/{orderId:guid}", async (
            Guid orderId,
            NotificationDbContext context,
            CancellationToken cancellationToken) =>
        {
            var logs = await context.NotificationLogs
                .AsNoTracking()
                .Where(n => n.OrderId == orderId)
                .OrderBy(n => n.SentAtUtc)
                .Select(n => new
                {
                    n.Channel,
                    n.Recipient,
                    n.Subject,
                    n.SentAtUtc,
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new { orderId, count = logs.Count, logs });
        });
    }
}
