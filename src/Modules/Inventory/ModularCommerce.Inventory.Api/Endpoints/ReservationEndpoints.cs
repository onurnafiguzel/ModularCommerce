using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularCommerce.Inventory.Application.Reservations.GetReservation;
using ModularCommerce.Inventory.Application.Reservations.ReserveStock;
using ModularCommerce.Shared.Infrastructure.Endpoints;

namespace ModularCommerce.Inventory.Api.Endpoints;

public static class ReservationEndpoints
{
    public static void MapReservationEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapPost("/reservations", async (
            ReserveStockCommand command,
            ReserveStockHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/api/inventory/reservations/{result.Value.ReservationId}", result.Value)
                : result.ToHttpResult();
        });

        group.MapGet("/reservations/{id:guid}", async (
            Guid id,
            GetReservationHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, cancellationToken);
            return result.ToHttpResult();
        });
    }
}
