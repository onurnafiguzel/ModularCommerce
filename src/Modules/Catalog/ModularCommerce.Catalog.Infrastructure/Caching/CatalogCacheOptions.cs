using System.ComponentModel.DataAnnotations;

namespace ModularCommerce.Catalog.Infrastructure.Caching;

/// <summary>
/// Katalog okuma-modeli cache ayarları. Ürünler çalışma zamanında değişmediğinden (yalnız seed)
/// read-through + TTL güvenlidir; invalidation bugün gerekmez (mutasyon endpoint'i gelince
/// event-tabanlı invalidation eklenir — <see cref="IProductCache.RemoveAsync"/> hook'u hazır).
/// </summary>
public sealed class CatalogCacheOptions
{
    public const string SectionName = "Catalog:Cache";

    /// <summary>Cache girdisi ömrü (sn). Bayat okuma penceresini sınırlar.</summary>
    [Range(1, int.MaxValue)]
    public int TtlSeconds { get; set; } = 60;

    /// <summary>Cache katmanını aç/kapa (yük testinde cache'li/cache'siz A/B için).</summary>
    public bool Enabled { get; set; } = true;
}
