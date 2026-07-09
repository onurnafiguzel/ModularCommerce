using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using ModularCommerce.Ordering.Application.Orders.Cancel;
using ModularCommerce.Ordering.Application.Orders.Checkout;
using ModularCommerce.Ordering.Application.Orders.GetMyOrders;
using ModularCommerce.Ordering.Application.Orders.GetOrder;
using ModularCommerce.Shared.Infrastructure.Auth;
using ModularCommerce.Shared.Infrastructure.Endpoints;

namespace ModularCommerce.Ordering.Api.Endpoints;

public static class OrderEndpoints
{  
    public static void MapOrderEndpoints(this IEndpointRouteBuilder group)
    {
        var secured = ((RouteGroupBuilder)group).MapGroup("")
            .RequireAuthorization();

        secured.MapPost("/checkout", async (
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            ClaimsPrincipal user,
            CheckoutHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CheckoutCommand(user.GetUserId(), idempotencyKey ?? string.Empty);
            var result = await handler.HandleAsync(command, cancellationToken);

            if (result.IsFailure)
            {
                return result.ToHttpResult();
            }

            // Yeni sipariş 201 + Location; idempotency replay'i 200 (FR-5.4).
            return result.Value.IsExisting
                ? Results.Ok(result.Value.Order)
                : Results.Created($"/api/ordering/orders/{result.Value.Order.Id}", result.Value.Order);
        });

        secured.MapGet("/orders/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            GetOrderHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, user.GetUserId(), cancellationToken);
            return result.ToHttpResult();
        });

        secured.MapGet("/orders", async (
            ClaimsPrincipal user,
            GetMyOrdersHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(user.GetUserId(), cancellationToken);
            return result.ToHttpResult();
        });

        secured.MapPost("/orders/{id:guid}/cancel", async (
            Guid id,
            ClaimsPrincipal user,
            CancelOrderHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, user.GetUserId(), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult();
        });
    }
}
