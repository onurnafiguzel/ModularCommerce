using MassTransit;
using ModularCommerce.Notification.Application.Abstractions;
using ModularCommerce.Notification.Application.Delivery;
using ModularCommerce.Ordering.Contracts.IntegrationEvents;

namespace ModularCommerce.Notification.Api.Consumers;
public sealed class OrderPaidNotificationConsumer(INotificationProcessor processor)
    : IConsumer<OrderPaid>
{
    public async Task Consume(ConsumeContext<OrderPaid> context)
    {
        var message = context.Message;

        var instruction = new NotificationInstruction(
            IdempotencyKey: $"OrderPaid:{message.OrderId}",
            ConsumerType: nameof(OrderPaidNotificationConsumer),
            OrderId: message.OrderId,
            MessageId: context.MessageId,
            Message: new NotificationMessage(
                Recipient: $"customer-{message.CustomerId}@example.com",
                Subject: $"Siparişiniz onaylandı ({message.OrderId})",
                Body: $"Ödemeniz alındı. Tutar: {message.TotalAmount} {message.Currency}."));

        var result = await processor.ProcessAsync(instruction, context.CancellationToken);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Bildirim işlenemedi: {result.Error.Code} - {result.Error.Message}");
        }
    }
}
