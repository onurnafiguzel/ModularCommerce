using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace ModularCommerce.Shared.IntegrationTests.Fixtures;

/// <summary>
/// Health check probları gerçek bağımlılığa karşı doğrulanır: koleksiyon tek Postgres + tek Redis
/// container paylaşır. Ön koşul: Docker Desktop.
/// </summary>
public sealed class InfraContainersFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();
    public string RedisConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync()
        => await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }
}

[CollectionDefinition("Infra")]
public sealed class InfraCollection : ICollectionFixture<InfraContainersFixture>;
