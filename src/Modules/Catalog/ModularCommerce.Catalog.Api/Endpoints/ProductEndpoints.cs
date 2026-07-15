using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularCommerce.Catalog.Application.Products.CreateProduct;
using ModularCommerce.Catalog.Application.Products.GetProductById;
using ModularCommerce.Catalog.Application.Products.GetProducts;
using ModularCommerce.Catalog.Application.Products.UpdateProduct;
using ModularCommerce.Shared.Infrastructure.Endpoints;

namespace ModularCommerce.Catalog.Api.Endpoints;

/// <summary>PUT gövdesi — Id rotadan gelir, gövdede tekrarlanmaz.</summary>
public sealed record UpdateProductRequest(
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    bool IsActive);

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

        // Yazma yolları korumalı (anonim GET'ler korunur). Mutasyon → outbox → Discovery indeksler.
        group.MapPost("/products", async (
            CreateProductCommand command,
            CreateProductHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/catalog/products/{result.Value}", new { id = result.Value })
                : result.ToHttpResult();
        }).RequireAuthorization();

        group.MapPut("/products/{id:guid}", async (
            Guid id,
            UpdateProductRequest request,
            UpdateProductHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateProductCommand(
                id, request.Name, request.Description, request.Price, request.Currency, request.IsActive);
            var result = await handler.HandleAsync(command, cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult();
        }).RequireAuthorization();
    }
}
