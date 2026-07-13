using ModularCommerce.Catalog.Contracts;

namespace ModularCommerce.Catalog.Infrastructure.Caching;

/// <summary>
/// IProductReader (checkout sıcak yolu — ürün snapshot batch'i) read-through cache decorator'ı.
/// Her ürün ayrı anahtarda cache'lenir → kısmi isabet: yalnız cache'te OLMAYAN id'ler DB'ye gider.
/// Not: snapshot IsActive/Price taşır; ürünler runtime'da değişmediğinden bayat-satış riski yok,
/// TTL penceresi de sınırlar. Mutasyon endpoint'i gelince event-tabanlı invalidation eklenmeli.
/// </summary>
public sealed class CachingProductReader(IProductReader inner, IProductCache cache) : IProductReader
{
    public async Task<IReadOnlyList<ProductSnapshotDto>> GetByIdsAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        var hits = new List<ProductSnapshotDto>(productIds.Count);
        var misses = new List<Guid>();

        foreach (var id in productIds.Distinct())
        {
            var cached = await cache.GetAsync<ProductSnapshotDto>(CatalogCacheKeys.Snapshot(id), cancellationToken);
            if (cached is not null)
            {
                hits.Add(cached);
            }
            else
            {
                misses.Add(id);
            }
        }

        if (misses.Count > 0)
        {
            var fetched = await inner.GetByIdsAsync(misses, cancellationToken);
            foreach (var dto in fetched)
            {
                await cache.SetAsync(CatalogCacheKeys.Snapshot(dto.ProductId), dto, cancellationToken);
                hits.Add(dto);
            }
        }

        return hits;
    }
}
