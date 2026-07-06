using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Inventory.IntegrationTests.Fixtures;
using Xunit;

namespace ModularCommerce.Inventory.IntegrationTests;

[Collection("Postgres")]
[Trait("Category", "Integration")]
public class MigrationTests(PostgresContainerFixture fixture)
{
    [Fact(DisplayName = "Migration temiz uygulanır; stok kaydı yazılıp okunabilir")]
    public async Task Migration_applies_cleanly_and_roundtrips()
    {
        await using var context = fixture.CreateContext();

        var pending = await context.Database.GetPendingMigrationsAsync();
        pending.Should().BeEmpty("fixture migrate etti, bekleyen migration kalmamalı");

        var productId = Guid.NewGuid();
        var item = StockItem.Create(productId, 42).Value;
        item.ClearDomainEvents();
        context.StockItems.Add(item);
        await context.SaveChangesAsync();

        await using var readContext = fixture.CreateContext();
        var loaded = await readContext.StockItems
            .AsNoTracking()
            .SingleAsync(s => s.ProductId == productId);

        loaded.OnHand.Should().Be(42);
        loaded.Reserved.Should().Be(0);
        loaded.Available.Should().Be(42);
    }
}
