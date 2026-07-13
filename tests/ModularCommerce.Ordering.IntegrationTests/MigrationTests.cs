using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Ordering.IntegrationTests.Fixtures;
using Xunit;

namespace ModularCommerce.Ordering.IntegrationTests;

[Collection("OrderingPostgres")]
public sealed class MigrationTests(PostgresContainerFixture fixture)
{
    [Fact(DisplayName = "Migration uygulanır: ordering şemasında sipariş round-trip'i (lines + history dahil) çalışır")]
    public async Task Migration_CreatesOrderingSchema_AndOrderRoundTrips()
    {
        var customerId = Guid.NewGuid();
        Guid orderId;

        await using (var context = fixture.CreateContext())
        {
            (await context.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();

            var order = Order.Create(
                customerId,
                "migrasyon-kaniti",
                [new OrderLineDraft(Guid.NewGuid(), "Ürün", 99.90m, "TRY", 2, Guid.NewGuid())],
                "test").Value;
            order.MarkStockReserved("test");

            context.Orders.Add(order);
            await context.SaveChangesAsync();
            orderId = order.Id;
        }

        await using (var verify = fixture.CreateContext())
        {
            var loaded = await verify.Orders.SingleAsync(o => o.Id == orderId);

            loaded.Status.Should().Be(OrderStatus.StockReserved);
            loaded.Lines.Should().ContainSingle(l => l.ProductName == "Ürün" && l.Quantity == 2);
            loaded.TotalAmount.Amount.Should().Be(199.80m);
            loaded.TotalAmount.Currency.Should().Be("TRY");
            loaded.StatusHistory.Should().HaveCount(2, "∅→Created ve Created→StockReserved izleri");
        }
    }
}
