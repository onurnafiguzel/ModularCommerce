using Microsoft.EntityFrameworkCore;
using ModularCommerce.Inventory.Application.Abstractions;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Infrastructure.Persistence.Strategies;

public sealed class NaiveReservationStrategy(InventoryDbContext context) : IReservationStrategy
{
    public async Task<Result<Reservation>> ReserveAsync(
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        // Bayat snapshot: track edilmez, token bağlanmaz.
        var stockItem = await context.StockItems
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProductId == productId, cancellationToken);

        if (stockItem is null)
        {
            return Result.Failure<Reservation>(InventoryErrors.StockItemNotFound(productId));
        }

        // Invariant bayat kopya üzerinde çalışır — check-then-act'in "check" kısmı.
        var reserved = stockItem.Reserve(quantity);
        if (reserved.IsFailure)
        {
            return Result.Failure<Reservation>(reserved.Error);
        }

        var utcNow = DateTime.UtcNow;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // "Act" kısmı: korumasız artış — WHERE'de xmin de stok kontrolü de YOK (bilerek).
        await context.Database.ExecuteSqlAsync(
            $"""
             UPDATE inventory.stock_items
             SET "Reserved" = "Reserved" + {quantity}, "UpdatedAtUtc" = {utcNow}
             WHERE "ProductId" = {productId}
             """,
            cancellationToken);

        context.Reservations.Add(reserved.Value);
        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success(reserved.Value);
    }
}
