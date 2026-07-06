using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Application.Abstractions;

public interface IReservationStrategy
{
    Task<Result<Reservation>> ReserveAsync(
        Guid productId,
        int quantity, 
        CancellationToken cancellationToken);
}
