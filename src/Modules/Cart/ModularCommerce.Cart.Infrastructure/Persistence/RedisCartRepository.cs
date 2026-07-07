using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Shared.Kernel;
using StackExchange.Redis;

namespace ModularCommerce.Cart.Infrastructure.Persistence;

public sealed class RedisCartRepository(
    IConnectionMultiplexer redis,
    IConfiguration configuration) : ICartRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = JsonSerializerOptions.Web;

    private readonly TimeSpan _ttl = TimeSpan.FromDays(
        configuration.GetValue("Cart:TtlDays", 7));

    private static string KeyFor(Guid customerId) => $"cart:{customerId}";

    public async Task<Result<Domain.Carts.Cart?>> GetAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await redis.GetDatabase().StringGetAsync(KeyFor(customerId));

            return Result.Success(value.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<CartDocument>(value.ToString(), SerializerOptions)!.ToCart());
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            return Result.Failure<Domain.Carts.Cart?>(CartErrors.StorageUnavailable);
        }
    }

    public async Task<Result> SaveAsync(Domain.Carts.Cart cart, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(CartDocument.FromCart(cart), SerializerOptions);

        try
        {
            // SET + expiry tek komut: TTL yazmada kayar (FR-4.2).
            await redis.GetDatabase().StringSetAsync(KeyFor(cart.CustomerId), payload, _ttl);
            return Result.Success();
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            return Result.Failure(CartErrors.StorageUnavailable);
        }
    }

    public async Task<Result> RemoveAsync(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(KeyFor(customerId));
            return Result.Success();
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            return Result.Failure(CartErrors.StorageUnavailable);
        }
    }
}
