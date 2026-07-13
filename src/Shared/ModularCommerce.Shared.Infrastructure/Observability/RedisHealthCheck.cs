using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ModularCommerce.Shared.Infrastructure.Observability;
public sealed class RedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy("Redis erişilebilir");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis erişilemiyor", ex);
        }
    }
}
