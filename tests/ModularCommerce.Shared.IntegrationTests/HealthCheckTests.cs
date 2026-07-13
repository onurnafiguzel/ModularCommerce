using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModularCommerce.Shared.Infrastructure.Observability;
using ModularCommerce.Shared.IntegrationTests.Fixtures;
using StackExchange.Redis;
using Xunit;

namespace ModularCommerce.Shared.IntegrationTests;

/// <summary>
/// El yapımı readiness probları gerçek bağımlılığa karşı: ayakta → Healthy, erişilemez → Unhealthy.
/// (Unhealthy yolu, /health/ready'nin 503 döndürüp LB routing'i kesmesinin temelidir.)
/// </summary>
[Collection("Infra")]
public sealed class HealthCheckTests(InfraContainersFixture fixture)
{
    private static IConfiguration ConfigWithDatabase(string connectionString)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = connectionString,
            })
            .Build();

    [Fact(DisplayName = "Postgres ayakta → Healthy")]
    public async Task Postgres_WhenUp_Healthy()
    {
        var check = new PostgresHealthCheck(ConfigWithDatabase(fixture.PostgresConnectionString));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact(DisplayName = "Postgres erişilemez → Unhealthy")]
    public async Task Postgres_WhenDown_Unhealthy()
    {
        // Kapalı port + kısa timeout → hızlı başarısızlık.
        var check = new PostgresHealthCheck(ConfigWithDatabase(
            "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1;Command Timeout=1"));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact(DisplayName = "Redis ayakta → Healthy")]
    public async Task Redis_WhenUp_Healthy()
    {
        using var multiplexer = await ConnectionMultiplexer.ConnectAsync(fixture.RedisConnectionString);
        var check = new RedisHealthCheck(multiplexer);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact(DisplayName = "Redis erişilemez → Unhealthy")]
    public async Task Redis_WhenDown_Unhealthy()
    {
        var options = ConfigurationOptions.Parse("127.0.0.1:1");
        options.AbortOnConnectFail = false;   // uygulamadaki gibi: bağlanamasa da exception atmaz
        options.ConnectTimeout = 500;
        using var multiplexer = await ConnectionMultiplexer.ConnectAsync(options);
        var check = new RedisHealthCheck(multiplexer);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }
}
