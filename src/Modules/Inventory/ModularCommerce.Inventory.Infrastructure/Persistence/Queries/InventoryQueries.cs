using Microsoft.EntityFrameworkCore;
using ModularCommerce.Inventory.Application.Abstractions;
using ModularCommerce.Inventory.Application.Reservations.Common;
using ModularCommerce.Inventory.Application.Stock.GetStock;

namespace ModularCommerce.Inventory.Infrastructure.Persistence.Queries;

public sealed class InventoryQueries(InventoryDbContext context) : IInventoryQueries
{
    public Task<StockResponse?> GetStockAsync(
        Guid productId, 
        CancellationToken cancellationToken)
        => context.StockItems
            .AsNoTracking()
            .Where(s => s.ProductId == productId)
            .Select(s => new StockResponse(s.ProductId, s.OnHand, s.Reserved, s.OnHand - s.Reserved))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ReservationResponse?> GetReservationAsync(
        Guid reservationId, 
        CancellationToken cancellationToken)
        => context.Reservations
            .AsNoTracking()
            .Where(r => r.Id == reservationId)
            .Select(r => new ReservationResponse(
                r.Id, r.ProductId, r.Quantity, r.Status.ToString(), r.ExpiresAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
}
