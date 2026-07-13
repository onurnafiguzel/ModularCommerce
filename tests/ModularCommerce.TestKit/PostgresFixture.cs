using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace ModularCommerce.TestKit;

/// <summary>
/// Testcontainers Postgres fixture temeli: koleksiyon TEK container paylaşır (test-başına-container
/// dakikalar → paylaşım + test-başına benzersiz veri saniyeler). Modül test projeleri bunu tek
/// satırlık bir alt sınıfla kapatır (yalnız <see cref="Schema"/> verir) — 6 modülde kopyalanan
/// bootstrap tek yere iner. Ön koşul: Docker Desktop.
/// </summary>
public abstract class PostgresFixture<TContext> : IAsyncLifetime
    where TContext : DbContext
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine") // docker-compose ile aynı imaj
        .Build();

    /// <summary>Modülün şeması (ör. <c>InventoryDbContext.Schema</c>) — alt sınıf verir.</summary>
    protected abstract string Schema { get; }

    public string ConnectionString => _container.GetConnectionString();

    private DbContextOptions<TContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _options = BuildOptions();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Her çağrıda taze context — paralel istekler gerçek gibi ayrık scope'larda koşar.</summary>
    public TContext CreateContext() => Instantiate(_options);

    /// <summary>
    /// İnterceptor gibi ek yapılandırma gereken alt sınıflar için (ör. Ordering outbox interceptor'ı):
    /// aynı container'a yeni bir seçenek seti kurar.
    /// </summary>
    protected DbContextOptions<TContext> BuildOptions(
        Action<DbContextOptionsBuilder<TContext>>? configure = null)
    {
        var builder = new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(
                _container.GetConnectionString(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Schema));

        configure?.Invoke(builder);
        return builder.Options;
    }

    /// <summary>DbContext ctor'u (DbContextOptions&lt;TContext&gt;) üzerinden örnekler.</summary>
    protected static TContext Instantiate(DbContextOptions<TContext> options)
        => (TContext)Activator.CreateInstance(typeof(TContext), options)!;
}
