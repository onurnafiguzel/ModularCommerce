using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ModularCommerce.Shared.Infrastructure.Observability;
public static class HealthCheckExtensions
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddDependencyHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            // TypeActivated: ctor bağımlılıkları (IConfiguration / IConnectionMultiplexer) DI'dan
            // çözülür, ayrı kayıt gerekmez.
            .AddTypeActivatedCheck<PostgresHealthCheck>(
                "postgres", HealthStatus.Unhealthy, tags: [ReadyTag])
            .AddTypeActivatedCheck<RedisHealthCheck>(
                "redis", HealthStatus.Unhealthy, tags: [ReadyTag]);

        // "masstransit-bus" health check'i AddMassTransit tarafından otomatik kaydedilir ve
        // varsayılan olarak "ready" tag'ini taşır → readiness predicate'ine kendiliğinden dahil olur.
        return services;
    }

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Liveness: hiç prob çalıştırmaz (Predicate: _ => false) → "süreç ayakta mı".
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = HealthResponseWriter.WriteAsync,
        }).DisableRateLimiting();

        // Readiness: "ready" tag'li tüm bağımlılık probları — biri Unhealthy ise 503.
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = HealthResponseWriter.WriteAsync,
        }).DisableRateLimiting();

        return endpoints;
    }
}
