using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModularCommerce.Cart.Infrastructure.Persistence;
using StackExchange.Redis;

namespace ModularCommerce.Cart.Infrastructure.Caching;

public sealed class RedisCartCache(
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    ILogger<RedisCartCache> logger) : ICartCache
{
    private static readonly JsonSerializerOptions SerializerOptions = JsonSerializerOptions.Web;

    private readonly TimeSpan _ttl = TimeSpan.FromDays(configuration.GetValue("Cart:TtlDays", 7));

    private static string KeyFor(Guid customerId) => $"cart:{customerId}";

    public async Task<Domain.Carts.Cart?> GetAsync(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            var value = await redis.GetDatabase().StringGetAsync(KeyFor(customerId));
            return value.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<CartDocument>(value.ToString(), SerializerOptions)!.ToCart();
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            // Graceful degrade: cache ulaşılamıyor → miss, çağıran Postgres'e düşer.
            logger.LogWarning(ex, "Sepet cache okunamadı ({CustomerId}); Postgres'e düşülüyor.", customerId);
            return null;
        }
    }

    public async Task SetAsync(Domain.Carts.Cart cart, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(CartDocument.FromCart(cart), SerializerOptions);
            // SET + expiry tek komut: TTL yazmada kayar.
            await redis.GetDatabase().StringSetAsync(KeyFor(cart.CustomerId), payload, _ttl);
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            logger.LogWarning(ex, "Sepet cache yazılamadı ({CustomerId}); atlanıyor.", cart.CustomerId);
        }
    }

    public async Task RemoveAsync(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(KeyFor(customerId));
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            logger.LogWarning(ex, "Sepet cache girdisi silinemedi ({CustomerId}); atlanıyor.", customerId);
        }
    }
}
