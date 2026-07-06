using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularCommerce.Inventory.Application.Stock.GetStock;
using ModularCommerce.Inventory.Application.Stock.SetStock;
using ModularCommerce.Shared.Infrastructure.Endpoints;

namespace ModularCommerce.Inventory.Api.Endpoints;

/// <summary>Stok sorgu endpoint'i + yalnızca Development'ta açılan reset endpoint'i.</summary>
public static class StockEndpoints
{
    public static void MapStockEndpoints(this IEndpointRouteBuilder group, bool isDevelopment)
    {
        group.MapGet("/stock/{productId:guid}", async (
            Guid productId,
            GetStockHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(productId, cancellationToken);
            return result.ToHttpResult();
        });

        if (!isDevelopment)
        {
            return;
        }

        // Dev-only: deterministik test başlangıcı için stok reset'i (rezervasyonlar dahil silinir).
        group.MapPut("/dev/stock/{productId:guid}", async (
            Guid productId,
            SetStockRequest request,
            SetStockHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new SetStockCommand(productId, request.OnHand), cancellationToken);
            return result.ToHttpResult();
        });
    }

    /// <summary>Dev reset isteğinin gövdesi.</summary>
    public sealed record SetStockRequest(int OnHand);
}
