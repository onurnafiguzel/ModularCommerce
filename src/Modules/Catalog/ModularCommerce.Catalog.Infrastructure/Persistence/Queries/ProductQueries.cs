using Microsoft.EntityFrameworkCore;
using ModularCommerce.Catalog.Application.Abstractions;
using ModularCommerce.Catalog.Application.Common;
using ModularCommerce.Catalog.Application.Products.Common;
using ModularCommerce.Catalog.Application.Products.GetProducts;

namespace ModularCommerce.Catalog.Infrastructure.Persistence.Queries;

public sealed class ProductQueries(CatalogDbContext context) : IProductQueries
{
    public async Task<PagedResponse<ProductSummaryResponse>> GetProductsAsync(
        GetProductsQuery query,
        CancellationToken cancellationToken)
    {
        var products = context.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            products = products.Where(p => EF.Functions.ILike(p.Name, pattern));
        }

        if (query.MinPrice.HasValue)
        {
            products = products.Where(p => p.Price.Amount >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            products = products.Where(p => p.Price.Amount <= query.MaxPrice.Value);
        }

        var totalCount = await products.CountAsync(cancellationToken);

        var items = await products
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id) // eşit adlarda deterministik sayfalama
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductSummaryResponse(
                p.Id,
                p.Name,
                p.Sku,
                p.Price.Amount,
                p.Price.Currency,
                p.StockQuantity))
            .ToListAsync(cancellationToken);

        return new PagedResponse<ProductSummaryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
