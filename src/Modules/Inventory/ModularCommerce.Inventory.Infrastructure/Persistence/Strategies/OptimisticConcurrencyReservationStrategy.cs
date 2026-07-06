using Microsoft.EntityFrameworkCore;
using ModularCommerce.Inventory.Application.Abstractions;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Infrastructure.Persistence.Strategies;

public sealed class OptimisticConcurrencyReservationStrategy(InventoryDbContext context)
    : IReservationStrategy
{
    public async Task<Result<Reservation>> ReserveAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var stockItem = await context.StockItems
            .FirstOrDefaultAsync(s => s.ProductId == productId, cancellationToken);

        if (stockItem is null)
        {
            return Result.Failure<Reservation>(InventoryErrors.StockItemNotFound(productId));
        }

        var reserved = stockItem.Reserve(quantity);
        if (reserved.IsFailure)
        {
            return Result.Failure<Reservation>(reserved.Error);
        }

        context.Reservations.Add(reserved.Value);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Kesin CP (NFR-3.4): belirsizlikte reddet, asla iyimser onay verme.
            return Result.Failure<Reservation>(InventoryErrors.ConcurrencyConflict);
        }

        return Result.Success(reserved.Value);
    }
}
