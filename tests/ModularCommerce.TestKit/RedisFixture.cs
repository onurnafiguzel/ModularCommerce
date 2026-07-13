using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace ModularCommerce.TestKit;

/// <summary>
/// Testcontainers Redis fixture'ı (redis:7-alpine, docker-compose ile aynı imaj): koleksiyon tek
/// container paylaşır. Modül test projeleri doğrudan kullanır ya da ince bir alt sınıfla kapatır.
/// Ön koşul: Docker Desktop.
/// </summary>
public class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public string ConnectionString => _container.GetConnectionString();

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
