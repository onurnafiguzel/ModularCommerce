using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace ModularCommerce.Inventory.IntegrationTests.Fixtures;

/// <summary>
/// Redis test container'ı (redis:7-alpine, docker-compose ile aynı imaj).
/// Multiplexer'ı kendisi yönetir; koleksiyonlar arası paylaşılmaz.
/// </summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Connection = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        Connection.Dispose();
        await _container.DisposeAsync();
    }
}

/// <summary>
/// Postgres + Redis birlikte gereken testler için kompozit koleksiyon —
/// mevcut Postgres fixture'ı aynen yeniden kullanılır (bootstrap kopyalanmaz).
/// </summary>
[CollectionDefinition("PostgresRedis")]
public sealed class PostgresRedisCollection :
    ICollectionFixture<PostgresContainerFixture>,
    ICollectionFixture<RedisContainerFixture>;
