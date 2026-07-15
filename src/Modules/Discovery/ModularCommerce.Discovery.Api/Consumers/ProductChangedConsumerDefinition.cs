using MassTransit;

namespace ModularCommerce.Discovery.Api.Consumers;

/// <summary>
/// Retry politikası (Notification deseni): 3× 2sn aralık. Tükenirse mesaj MassTransit'in otomatik
/// `_error` (DLQ) kuyruğuna gider. Embedding sağlayıcısının geçici kesintisi burada absorbe edilir.
/// </summary>
public sealed class ProductChangedConsumerDefinition
    : ConsumerDefinition<ProductChangedConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ProductChangedConsumer> consumerConfigurator,
        IRegistrationContext context)
        => endpointConfigurator.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)));
}
