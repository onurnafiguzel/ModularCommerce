using MassTransit;

namespace ModularCommerce.Notification.Api.Consumers;
public sealed class OrderPaidNotificationConsumerDefinition
    : ConsumerDefinition<OrderPaidNotificationConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OrderPaidNotificationConsumer> consumerConfigurator,
        IRegistrationContext context)
        => endpointConfigurator.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
}
