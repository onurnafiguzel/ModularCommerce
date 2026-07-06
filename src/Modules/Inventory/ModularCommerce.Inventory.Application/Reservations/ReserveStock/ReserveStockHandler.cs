using FluentValidation;
using ModularCommerce.Inventory.Application.Abstractions;
using ModularCommerce.Inventory.Application.Reservations.Common;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Application.Reservations.ReserveStock;

public sealed class ReserveStockHandler(
    IReservationStrategy strategy,
    IValidator<ReserveStockCommand> validator)
{
    public async Task<Result<ReservationResponse>> HandleAsync(
        ReserveStockCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ReservationResponse>(Error.Validation(
                "Inventory.Reservations.InvalidCommand",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var result = await strategy.ReserveAsync(command.ProductId, command.Quantity, cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<ReservationResponse>(result.Error);
        }

        var reservation = result.Value;
        return Result.Success(new ReservationResponse(
            reservation.Id,
            reservation.ProductId,
            reservation.Quantity,
            reservation.Status.ToString(),
            reservation.ExpiresAtUtc));
    }
}
