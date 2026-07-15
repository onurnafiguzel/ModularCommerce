using Microsoft.EntityFrameworkCore;
using ModularCommerce.Catalog.Domain.Products;
using ModularCommerce.Catalog.Infrastructure.Caching;

namespace ModularCommerce.Catalog.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(CatalogDbContext context, IProductCache cache) : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void AddRange(IEnumerable<Product> products)
        => context.Products.AddRange(products);

    public Task<bool> AnyAsync(CancellationToken cancellationToken)
        => context.Products.AnyAsync(cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken); // interceptor → ProductCreated outbox
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken); // takip edilen ürün; interceptor → ProductUpdated outbox

        // Fiyat/ad/aktiflik değişti → okuma cache'i bayatladı; invalidation hook'u burada devreye girer
        // (Application EF/Redis görmez). Ürünler artık runtime-immutable değil.
        await cache.RemoveAsync(CatalogCacheKeys.Product(product.Id), cancellationToken);
        await cache.RemoveAsync(CatalogCacheKeys.Snapshot(product.Id), cancellationToken);
    }
}
