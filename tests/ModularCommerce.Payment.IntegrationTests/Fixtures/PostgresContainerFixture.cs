using Microsoft.EntityFrameworkCore;
using ModularCommerce.Payment.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ModularCommerce.Payment.IntegrationTests.Fixtures;

/// <summary>
/// Repo fixture deseninin proje-lokal kopyası: koleksiyon tek container paylaşır,
/// her test benzersiz müşteri/key ile çalışır. Ön koşul: Docker Desktop.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine") // docker-compose ile aynı imaj
        .Build();

    private DbContextOptions<PaymentDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(
                _container.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PaymentDbContext.Schema))
            .Options;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Her çağrıda taze context — paralel charge denemeleri ayrık scope'larda koşar.</summary>
    public PaymentDbContext CreateContext() => new(_options);
}

[CollectionDefinition("PaymentPostgres")]
public sealed class PaymentPostgresCollection : ICollectionFixture<PostgresContainerFixture>;
