namespace ModularCommerce.Inventory.Domain.Stock;

public interface IStockItemRepository
{
    Task<StockItem?> GetByProductIdAsync(
        Guid productId, 
        CancellationToken cancellationToken);

    void Add(StockItem stockItem);

    Task RemoveByProductIdAsync(
        Guid productId, 
        CancellationToken cancellationToken);

    Task<bool> AnyAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
