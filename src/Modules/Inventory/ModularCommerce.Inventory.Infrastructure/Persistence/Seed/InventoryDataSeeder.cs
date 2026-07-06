using Microsoft.EntityFrameworkCore;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Shared.Infrastructure.Persistence;

namespace ModularCommerce.Inventory.Infrastructure.Persistence.Seed;

public sealed class InventoryDataSeeder : IDataSeeder<InventoryDbContext>
{
    /// <summary>Oversell demo hedefi — OnHand 10 (NFR-3.1 senaryosu).</summary>
    public static readonly Guid OversellTargetProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Yük/smoke hedefi — OnHand 1.000.000 (çakışmasız p95 ölçümü için).</summary>
    public static readonly Guid LoadTargetProductId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Manuel test hedefi — OnHand 100.</summary>
    public static readonly Guid ManualTargetProductId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public async Task SeedAsync(InventoryDbContext context, CancellationToken cancellationToken)
    {
        if (await context.StockItems.AnyAsync(cancellationToken))
        {
            return;
        }

        (Guid ProductId, int OnHand)[] seedData =
        [
            (OversellTargetProductId, 10),
            (LoadTargetProductId, 1_000_000),
            (ManualTargetProductId, 100),
        ];

        foreach (var (productId, onHand) in seedData)
        {
            var item = StockItem.Create(productId, onHand);
            if (item.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Seed verisi domain kurallarını ihlal ediyor ({productId}): {item.Error.Message}");
            }

            item.Value.ClearDomainEvents();
            context.StockItems.Add(item.Value);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
