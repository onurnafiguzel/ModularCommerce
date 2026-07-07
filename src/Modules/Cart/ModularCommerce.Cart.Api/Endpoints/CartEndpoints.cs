using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularCommerce.Cart.Application.Carts.AddItem;
using ModularCommerce.Cart.Application.Carts.GetCart;
using ModularCommerce.Cart.Application.Carts.RemoveItem;
using ModularCommerce.Cart.Application.Carts.UpdateItemQuantity;
using ModularCommerce.Shared.Infrastructure.Auth;
using ModularCommerce.Shared.Infrastructure.Endpoints;

namespace ModularCommerce.Cart.Api.Endpoints;

/// <summary>İstek gövdeleri; CustomerId gövdeden değil JWT'nin sub claim'inden gelir.</summary>
public sealed record AddItemRequest(Guid ProductId, int Quantity);
public sealed record UpdateItemQuantityRequest(int Quantity);

public static class CartEndpoints
{    
    public static void MapCartEndpoints(this IEndpointRouteBuilder group)
    {
        var secured = ((RouteGroupBuilder)group).MapGroup("")
            .RequireAuthorization();

        secured.MapGet("", async (
            ClaimsPrincipal user,
            GetCartHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(user.GetUserId(), cancellationToken);
            return result.ToHttpResult();
        });

        secured.MapPost("/items", async (
            AddItemRequest request,
            ClaimsPrincipal user,
            AddItemHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AddItemCommand(user.GetUserId(), request.ProductId, request.Quantity);
            var result = await handler.HandleAsync(command, cancellationToken);
            return result.ToHttpResult();
        });

        secured.MapPut("/items/{productId:guid}", async (
            Guid productId,
            UpdateItemQuantityRequest request,
            ClaimsPrincipal user,
            UpdateItemQuantityHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateItemQuantityCommand(user.GetUserId(), productId, request.Quantity);
            var result = await handler.HandleAsync(command, cancellationToken);
            return result.ToHttpResult();
        });

        secured.MapDelete("/items/{productId:guid}", async (
            Guid productId,
            ClaimsPrincipal user,
            RemoveItemHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(user.GetUserId(), productId, cancellationToken);
            return result.ToHttpResult();
        });
    }
}
