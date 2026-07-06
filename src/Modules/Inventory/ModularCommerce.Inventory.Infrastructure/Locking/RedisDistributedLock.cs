using ModularCommerce.Inventory.Application.Abstractions;
using StackExchange.Redis;

namespace ModularCommerce.Inventory.Infrastructure.Locking;

public sealed class RedisDistributedLock(IConnectionMultiplexer redis) : IDistributedLock
{
    private const string ReleaseScript =
        """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    public async Task<ILockHandle?> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        TimeSpan waitBudget,
        CancellationToken cancellationToken)
    {
        var database = redis.GetDatabase();
        var token = Guid.NewGuid().ToString("N");
        var deadline = DateTime.UtcNow.Add(waitBudget);
        var started = DateTime.UtcNow;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await database.StringSetAsync(key, token, ttl, When.NotExists))
            {
                return new RedisLockHandle(database, key, token, DateTime.UtcNow - started);
            }

            if (DateTime.UtcNow >= deadline)
            {
                return null; // bütçe doldu — çağıran 409 "tekrar deneyin" üretir
            }

            await Task.Delay(Random.Shared.Next(5, 16), cancellationToken);
        }
    }

    private sealed class RedisLockHandle(
        IDatabase database,
        string key,
        string token,
        TimeSpan waitedFor) : ILockHandle
    {
        public TimeSpan WaitedFor => waitedFor;

        public async ValueTask DisposeAsync()
            => await database.ScriptEvaluateAsync(
                ReleaseScript,
                [new RedisKey(key)],
                [new RedisValue(token)]);
    }
}
