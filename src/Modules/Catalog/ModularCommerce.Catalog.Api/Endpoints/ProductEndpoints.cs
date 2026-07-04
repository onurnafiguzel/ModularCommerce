using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularCommerce.Catalog.Application.Products.GetProductById;
using ModularCommerce.Catalog.Application.Products.GetProducts;
using ModularCommerce.Shared.Infrastructure.Endpoints;

namespace ModularCommerce.Catalog.Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/products", async (
            [AsParameters] GetProductsQuery query,
            GetProductsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(query, cancellationToken);
            return result.ToHttpResult();
        });

        group.MapGet("/products/{id:guid}", async (
            Guid id,
            GetProductByIdHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(id, cancellationToken);
            return result.ToHttpResult();
        });
    }
}
