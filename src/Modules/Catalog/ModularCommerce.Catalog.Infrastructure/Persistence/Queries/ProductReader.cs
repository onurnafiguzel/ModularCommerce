using Microsoft.EntityFrameworkCore;
using ModularCommerce.Catalog.Contracts;

namespace ModularCommerce.Catalog.Infrastructure.Persistence.Queries;
public sealed class ProductReader(CatalogDbContext context) : IProductReader
{
    public async Task<IReadOnlyList<ProductSnapshotDto>> GetByIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
        => await context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new ProductSnapshotDto(
                p.Id,
                p.Name,
                p.Price.Amount,
                p.Price.Currency,
                p.IsActive))
            .ToListAsync(cancellationToken);
}
