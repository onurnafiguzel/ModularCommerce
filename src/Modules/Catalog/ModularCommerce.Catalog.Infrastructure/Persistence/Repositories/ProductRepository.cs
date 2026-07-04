using Microsoft.EntityFrameworkCore;
using ModularCommerce.Catalog.Domain.Products;

namespace ModularCommerce.Catalog.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(CatalogDbContext context) : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void AddRange(IEnumerable<Product> products)
        => context.Products.AddRange(products);

    public Task<bool> AnyAsync(CancellationToken cancellationToken)
        => context.Products.AnyAsync(cancellationToken);
}
