using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ModularCommerce.Catalog.Infrastructure.Caching;

/// <summary>
/// Redis destekli read-through cache (Cart deseni: ham IConnectionMultiplexer + System.Text.Json).
/// Kritik: her Redis işlemi graceful degrade eder — bağlantı/timeout hatasında okuma cache-miss
/// gibi davranır (DB'ye düşülür), yazma sessizce atlanır. Cache ASLA okuma yolunu kırmaz.
/// </summary>
public sealed class RedisProductCache(
    IConnectionMultiplexer redis,
    IOptions<CatalogCacheOptions> options,
    ILogger<RedisProductCache> logger) : IProductCache
{
    private static readonly JsonSerializerOptions SerializerOptions = JsonSerializerOptions.Web;

    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(options.Value.TtlSeconds);

    // Kapalıyken cache saf passthrough: Get hep miss (Redis'e gitmez), Set/Remove no-op.
    // Bayrak burada yaşar ki decorator'lar KOŞULSUZ sarılabilsin (CatalogModule dallanmasız kalır).
    private readonly bool _enabled = options.Value.Enabled;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        if (!_enabled)
        {
            return null;
        }

        try
        {
            var value = await redis.GetDatabase().StringGetAsync(key);
            return value.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<T>(value.ToString(), SerializerOptions);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            // Graceful degrade: cache ulaşılamıyor → miss gibi davran, çağıran DB'ye düşer.
            logger.LogWarning(ex, "Katalog cache okunamadı ({Key}); DB'ye düşülüyor.", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class
    {
        if (!_enabled)
        {
            return;
        }

        try
        {
            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            await redis.GetDatabase().StringSetAsync(key, payload, _ttl);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            // Best-effort: yazma başarısızsa okuma yolu etkilenmez (sonraki istek yine DB'den okur).
            logger.LogWarning(ex, "Katalog cache yazılamadı ({Key}); atlanıyor.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        if (!_enabled)
        {
            return;
        }

        try
        {
            await redis.GetDatabase().KeyDeleteAsync(key);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            logger.LogWarning(ex, "Katalog cache girdisi silinemedi ({Key}); atlanıyor.", key);
        }
    }
}
