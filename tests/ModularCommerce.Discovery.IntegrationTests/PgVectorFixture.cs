using Microsoft.EntityFrameworkCore;
using ModularCommerce.Discovery.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace ModularCommerce.Discovery.IntegrationTests;

/// <summary>
/// pgvector destekli Postgres container'ı. Üretimdeki sırayı taklit eder: extension'ı data source'tan
/// ÖNCE ayrı bir bağlantıyla kur (NpgsqlDataSource tip kataloğunu ilk bağlantıda önbelleğe alır),
/// sonra UseVector'lı data source'u kur ve migration'ı uygula (şema + vektör kolonu + HNSW index).
/// </summary>
public sealed class PgVectorFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg17")
        .Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var connectionString = _container.GetConnectionString();

        await using (var provisioning = new NpgsqlConnection(connectionString))
        {
            await provisioning.OpenAsync();
            await using var command = provisioning.CreateCommand();
            command.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseVector();
        DataSource = builder.Build();

        var options = new DbContextOptionsBuilder<DiscoveryDbContext>()
            .UseNpgsql(DataSource, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", DiscoveryDbContext.Schema))
            .Options;
        await using var context = new DiscoveryDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _container.DisposeAsync();
    }
}
