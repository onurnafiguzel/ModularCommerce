using MassTransit;
using Microsoft.Extensions.Logging;
using ModularCommerce.Ordering.Contracts.IntegrationEvents;

namespace ModularCommerce.Notification.Api.Consumers;

public sealed class OrderPaidLoggingConsumer(ILogger<OrderPaidLoggingConsumer> logger)
    : IConsumer<OrderPaid>
{
    public Task Consume(ConsumeContext<OrderPaid> context)
    {
        var message = context.Message;
        logger.LogInformation(
            "[W8 log-only] OrderPaid alındı: OrderId={OrderId} CustomerId={CustomerId} Tutar={Total} {Currency}",
            message.OrderId, message.CustomerId, message.TotalAmount, message.Currency);
        return Task.CompletedTask;
    }
}
