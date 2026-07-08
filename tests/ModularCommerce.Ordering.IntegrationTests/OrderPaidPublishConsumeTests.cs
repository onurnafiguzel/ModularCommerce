using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Ordering.Contracts.IntegrationEvents;
using Xunit;

namespace ModularCommerce.Ordering.IntegrationTests;

/// <summary>
/// OrderPaid integration event'inin MassTransit pipeline'ında gerçekten publish edilip
/// tüketilebildiğini kanıtlar (in-memory Test Harness — RabbitMQ container'ı gerektirmez;
/// broker'a fiziksel teslim manuel/UI kanıtında gösterilir). Contracts POCO'sunun
/// serileştirme/routing sözleşmesini doğrular.
/// </summary>
public sealed class OrderPaidPublishConsumeTests
{
    /// <summary>Yerel test tüketicisi — üretimdeki OrderPaidLoggingConsumer ile aynı şekilde OrderPaid dinler.</summary>
    private sealed class RecordingConsumer : IConsumer<OrderPaid>
    {
        public Task Consume(ConsumeContext<OrderPaid> context) => Task.CompletedTask;
    }

    [Fact(DisplayName = "OrderPaid publish edilir ve consumer tarafından tüketilir (contract akışı)")]
    public async Task OrderPaid_IsPublishedAndConsumed()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x => x.AddConsumer<RecordingConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var message = new OrderPaid(Guid.NewGuid(), Guid.NewGuid(), 250m, "TRY", DateTime.UtcNow);
        await harness.Bus.Publish(message);

        (await harness.Published.Any<OrderPaid>()).Should().BeTrue("event publish edilmeli");
        var consumerHarness = harness.GetConsumerHarness<RecordingConsumer>();
        (await consumerHarness.Consumed.Any<OrderPaid>()).Should().BeTrue("consumer OrderPaid'i tüketmeli");
    }
}
