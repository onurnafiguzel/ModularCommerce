namespace ModularCommerce.Catalog.Infrastructure.Caching;

/// <summary>
/// Katalog okuma-modeli için read-through cache soyutlaması. Cache bir OPTİMİZASYONDUR:
/// implementasyon graceful degrade eder — Redis düşse bile okuma DB'ye düşer, ASLA kırılmaz
/// (Cart'tan farklı olarak burada Redis kaynak değil, hızlandırıcıdır).
/// </summary>
public interface IProductCache
{
    /// <summary>Cache'te varsa değeri, yoksa (veya cache ulaşılamazsa) null döner.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class;

    /// <summary>Değeri TTL ile yazar; cache ulaşılamazsa sessizce geçer (best-effort).</summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class;

    /// <summary>Invalidation hook'u: girdi silinir (mutasyon endpoint'i geldiğinde kullanılacak).</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Cache anahtarları tek yerde — çakışmayı ve dağınık string'leri önler.</summary>
public static class CatalogCacheKeys
{
    public static string Product(Guid id) => $"catalog:product:{id}";

    public static string Snapshot(Guid id) => $"catalog:snapshot:{id}";
}
