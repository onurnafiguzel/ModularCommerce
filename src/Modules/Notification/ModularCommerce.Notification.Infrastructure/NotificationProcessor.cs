using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModularCommerce.Notification.Application.Abstractions;
using ModularCommerce.Notification.Application.Delivery;
using ModularCommerce.Notification.Domain.Inbox;
using ModularCommerce.Notification.Domain.Notifications;
using ModularCommerce.Notification.Infrastructure.Persistence;
using ModularCommerce.Notification.Infrastructure.Persistence.Configurations;
using ModularCommerce.Shared.Kernel;
using Npgsql;

namespace ModularCommerce.Notification.Infrastructure;

public sealed class NotificationProcessor(
    NotificationDbContext context,
    IEnumerable<INotificationChannel> channels,
    ILogger<NotificationProcessor> logger) : INotificationProcessor
{
    public async Task<Result> ProcessAsync(
        NotificationInstruction instruction,
        CancellationToken cancellationToken)
    {
        // Hızlı-yol: zaten işlendiyse hiç gönderme, hiç yazma (idempotent no-op).
        var alreadyProcessed = await context.ProcessedMessages.AnyAsync(
            p => p.IdempotencyKey == instruction.IdempotencyKey
                && p.ConsumerType == instruction.ConsumerType,
            cancellationToken);

        if (alreadyProcessed)
        {
            logger.LogInformation(
                "Bildirim zaten işlendi, atlanıyor: {Key}", instruction.IdempotencyKey);
            return Result.Success();
        }

        // Gönderim (SİM): knob'la fırlayabilir → aşağıdaki SaveChanges'e hiç ulaşılmaz →
        // processed satırı yazılmaz → MassTransit mesajı retry eder (at-least-once).
        foreach (var channel in channels)
        {
            await channel.SendAsync(instruction.Message, cancellationToken);

            context.NotificationLogs.Add(new NotificationLog
            {
                IdempotencyKey = instruction.IdempotencyKey,
                Channel = channel.Name,
                Recipient = instruction.Message.Recipient,
                Subject = instruction.Message.Subject,
                OrderId = instruction.OrderId,
                SentAtUtc = DateTime.UtcNow,
            });
        }

        context.ProcessedMessages.Add(new ProcessedMessage
        {
            IdempotencyKey = instruction.IdempotencyKey,
            ConsumerType = instruction.ConsumerType,
            ProcessedOnUtc = DateTime.UtcNow,
            MessageId = instruction.MessageId,
        });

        try
        {
            // Audit satırları + processed-marker TEK transaction (atomik).
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: ProcessedMessageConfiguration.PrimaryKeyName,
            })
        {
            // Eşzamanlı çift teslim: PK hakemi bu denemeyi eledi → diğer teslim işi bitirdi.
            logger.LogInformation(
                "Eşzamanlı çift teslim elendi, atlanıyor: {Key}", instruction.IdempotencyKey);
            return Result.Success();
        }

        return Result.Success();
    }
}
