using Microsoft.EntityFrameworkCore;
using ModularCommerce.Inventory.Domain.Stock;

namespace ModularCommerce.Inventory.Infrastructure.Persistence.Repositories;

public sealed class StockItemRepository(InventoryDbContext context) : IStockItemRepository
{
    public Task<StockItem?> GetByProductIdAsync(
        Guid productId, 
        CancellationToken cancellationToken)
        => context.StockItems.FirstOrDefaultAsync(s => s.ProductId == productId, cancellationToken);

    public void Add(StockItem stockItem)
        => context.StockItems.Add(stockItem);

    public async Task RemoveByProductIdAsync(
        Guid productId, 
        CancellationToken cancellationToken)
    {
        // Dev reset: önce rezervasyonlar, sonra stok kaydı (FK yok ama mantıksal sıra bu).
        await context.Reservations
            .Where(r => r.ProductId == productId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.StockItems
            .Where(s => s.ProductId == productId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> AnyAsync(CancellationToken cancellationToken)
        => context.StockItems.AnyAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);
}
