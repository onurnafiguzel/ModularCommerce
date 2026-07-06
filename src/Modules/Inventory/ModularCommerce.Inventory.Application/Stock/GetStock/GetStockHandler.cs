using ModularCommerce.Inventory.Application.Abstractions;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Application.Stock.GetStock;

public sealed class GetStockHandler(IInventoryQueries queries)
{
    public async Task<Result<StockResponse>> HandleAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var stock = await queries.GetStockAsync(productId, cancellationToken);

        return stock is null
            ? Result.Failure<StockResponse>(InventoryErrors.StockItemNotFound(productId))
            : Result.Success(stock);
    }
}
