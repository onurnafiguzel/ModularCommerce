using ModularCommerce.Catalog.Application.Abstractions;
using ModularCommerce.Catalog.Application.Common;
using ModularCommerce.Catalog.Application.Products.Common;
using ModularCommerce.Catalog.Application.Products.GetProducts;

namespace ModularCommerce.Catalog.Infrastructure.Caching;

/// <summary>
/// IProductQueries read-through cache decorator'ı (OCP: DB implementasyonu değişmez).
/// Yalnız id-anahtarlı DETAY okuması cache'lenir (sınırlı, yüksek tekrar). LİSTE cache'lenmez:
/// arama/fiyat/sayfa permütasyonları sınırsız, tekrar düşük → churn/bellek maliyeti değmiyor.
/// </summary>
public sealed class CachingProductQueries(IProductQueries inner, IProductCache cache) : IProductQueries
{
    public Task<PagedResponse<ProductSummaryResponse>> GetProductsAsync(
        GetProductsQuery query,
        CancellationToken cancellationToken)
        => inner.GetProductsAsync(query, cancellationToken);

    public async Task<ProductDetailResponse?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = CatalogCacheKeys.Product(id);

        var cached = await cache.GetAsync<ProductDetailResponse>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var product = await inner.GetProductByIdAsync(id, cancellationToken);
        if (product is not null)
        {
            await cache.SetAsync(key, product, cancellationToken);
        }

        return product;
    }
}
