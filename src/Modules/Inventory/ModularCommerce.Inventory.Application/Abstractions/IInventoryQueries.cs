using ModularCommerce.Inventory.Application.Reservations.Common;
using ModularCommerce.Inventory.Application.Stock.GetStock;

namespace ModularCommerce.Inventory.Application.Abstractions;

public interface IInventoryQueries
{
    Task<StockResponse?> GetStockAsync(Guid productId, CancellationToken cancellationToken);

    Task<ReservationResponse?> GetReservationAsync(Guid reservationId, CancellationToken cancellationToken);
}
