using ModularCommerce.Inventory.Application.Abstractions;
using ModularCommerce.Inventory.Application.Reservations.Common;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Application.Reservations.GetReservation;

public sealed class GetReservationHandler(IInventoryQueries queries)
{
    public async Task<Result<ReservationResponse>> HandleAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        var reservation = await queries.GetReservationAsync(reservationId, cancellationToken);

        return reservation is null
            ? Result.Failure<ReservationResponse>(InventoryErrors.ReservationNotFound(reservationId))
            : Result.Success(reservation);
    }
}
