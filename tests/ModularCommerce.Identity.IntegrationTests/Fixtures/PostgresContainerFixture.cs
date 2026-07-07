using Microsoft.EntityFrameworkCore;
using ModularCommerce.Identity.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ModularCommerce.Identity.IntegrationTests.Fixtures;

/// <summary>
/// Inventory'deki fixture deseninin proje-lokal kopyası (repo kuralı):
/// koleksiyon tek container paylaşır, her test benzersiz e-postayla çalışır.
/// Üçüncü kopyada paylaşılan test kütüphanesine terfi değerlendirilir.
/// Ön koşul: Docker Desktop çalışıyor olmalı.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine") // docker-compose ile aynı imaj
        .Build();

    private DbContextOptions<IdentityDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(
                _container.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
            .Options;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Her çağrıda taze context — her handler adımı gerçek istek gibi ayrık scope'ta koşar.</summary>
    public IdentityDbContext CreateContext() => new(_options);
}

[CollectionDefinition("IdentityPostgres")]
public sealed class IdentityPostgresCollection : ICollectionFixture<PostgresContainerFixture>;
