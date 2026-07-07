using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace ModularCommerce.Cart.IntegrationTests.Fixtures;

/// <summary>
/// Inventory'deki Redis fixture'ının proje-lokal kopyası (repo kuralı):
/// koleksiyon tek container paylaşır, her test benzersiz CustomerId kullanır.
/// Ön koşul: Docker Desktop çalışıyor olmalı.
/// </summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine") // docker-compose ile aynı imaj
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

[CollectionDefinition("CartRedis")]
public sealed class CartRedisCollection : ICollectionFixture<RedisContainerFixture>;
