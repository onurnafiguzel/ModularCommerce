using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace ModularCommerce.Shared.Infrastructure.Observability;
public sealed class PostgresHealthCheck(IConfiguration configuration) : IHealthCheck
{
    private readonly string _connectionString = configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("ConnectionStrings:Database bulunamadı");

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("Postgres erişilebilir");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres erişilemiyor", ex);
        }
    }
}
