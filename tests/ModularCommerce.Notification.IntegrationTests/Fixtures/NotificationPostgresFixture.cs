using Microsoft.EntityFrameworkCore;
using ModularCommerce.Notification.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace ModularCommerce.Notification.IntegrationTests.Fixtures;

/// <summary>
/// Koleksiyon tek Postgres container'ı paylaşır; her test benzersiz OrderId ile çalışır.
/// Ön koşul: Docker Desktop. İdempotency hakemi (processed_messages bileşik PK) yalnız gerçek
/// Postgres'te doğrulanır — bu yüzden processor testleri InMemory değil container kullanır.
/// </summary>
public sealed class NotificationPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    private DbContextOptions<NotificationDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(
                _container.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationDbContext.Schema))
            .Options;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public NotificationDbContext CreateContext() => new(_options);
}

[CollectionDefinition("NotificationPostgres")]
public sealed class NotificationPostgresCollection : ICollectionFixture<NotificationPostgresFixture>;
