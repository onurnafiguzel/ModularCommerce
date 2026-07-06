using ModularCommerce.Inventory.Application.Stock.GetStock;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Application.Stock.SetStock;

/// <summary>
/// Dev-only stok reset use case'i. DDD kuralı burada da geçerli: yeni kayıt
/// </summary>
public sealed class SetStockHandler(IStockItemRepository repository)
{
    public async Task<Result<StockResponse>> HandleAsync(
        SetStockCommand command,
        CancellationToken cancellationToken)
    {
        var created = StockItem.Create(command.ProductId, command.OnHand);
        if (created.IsFailure)
        {
            return Result.Failure<StockResponse>(created.Error);
        }

        await repository.RemoveByProductIdAsync(command.ProductId, cancellationToken);

        // Reset geçmiş kurulumdur; event'ler dispatch edilmez (outbox Hafta 7).
        created.Value.ClearDomainEvents();
        repository.Add(created.Value);
        await repository.SaveChangesAsync(cancellationToken);

        var item = created.Value;
        return Result.Success(new StockResponse(item.ProductId, item.OnHand, item.Reserved, item.Available));
    }
}
