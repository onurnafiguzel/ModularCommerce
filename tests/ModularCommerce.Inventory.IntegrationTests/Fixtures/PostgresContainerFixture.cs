using Microsoft.EntityFrameworkCore;
using ModularCommerce.Inventory.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ModularCommerce.Inventory.IntegrationTests.Fixtures;

/// <summary>
/// Repo'nun ilk Testcontainers fixture'ı: tüm "Postgres" koleksiyonu tek container paylaşır
/// (test başına container = dakikalar; paylaşım + test başına benzersiz ProductId = saniyeler).
/// İkinci modül integration testi geldiğinde paylaşılan bir test kütüphanesine terfi edecek.
/// Ön koşul: Docker Desktop çalışıyor olmalı.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine") // docker-compose ile aynı imaj
        .Build();

    private DbContextOptions<InventoryDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(
                _container.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema))
            .Options;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Her çağrıda taze context — paralel rezervasyon denemeleri gerçek istekler gibi ayrık scope'larda koşar.</summary>
    public InventoryDbContext CreateContext() => new(_options);
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>;
