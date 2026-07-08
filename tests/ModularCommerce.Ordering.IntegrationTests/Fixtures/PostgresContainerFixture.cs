using Microsoft.EntityFrameworkCore;
using ModularCommerce.Ordering.Infrastructure.Outbox;
using ModularCommerce.Ordering.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ModularCommerce.Ordering.IntegrationTests.Fixtures;

/// <summary>
/// Repo fixture deseninin proje-lokal kopyası: koleksiyon tek container paylaşır,
/// her test benzersiz müşteri/key ile çalışır. Ön koşul: Docker Desktop.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine") // docker-compose ile aynı imaj
        .Build();

    private DbContextOptions<OrderingDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _options = new DbContextOptionsBuilder<OrderingDbContext>()
            .UseNpgsql(
                _container.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", OrderingDbContext.Schema))
            .Options;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Her çağrıda taze context — paralel checkout denemeleri ayrık scope'larda koşar.</summary>
    public OrderingDbContext CreateContext() => new(_options);

    /// <summary>
    /// Outbox interceptor'ı bağlı context — üretimdeki OrderingModule kaydını taklit eder.
    /// SaveChanges anında domain event'ler outbox satırına çevrilir (atomiklik testleri için).
    /// </summary>
    public OrderingDbContext CreateContextWithOutbox()
    {
        var options = new DbContextOptionsBuilder<OrderingDbContext>()
            .UseNpgsql(
                _container.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", OrderingDbContext.Schema))
            .AddInterceptors(new DomainEventToOutboxInterceptor(new OrderingIntegrationEventRegistry()))
            .Options;
        return new OrderingDbContext(options);
    }
}

[CollectionDefinition("OrderingPostgres")]
public sealed class OrderingPostgresCollection : ICollectionFixture<PostgresContainerFixture>;
