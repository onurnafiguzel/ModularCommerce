using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace ModularCommerce.Ordering.IntegrationTests.Fixtures;

/// <summary>
/// Gerçek ModularCommerce.Host'u (tüm modüller) WebApplicationFactory ile in-memory ayağa
/// kaldırır — pgvector'lı bir Postgres testcontainer'a karşı (Discovery'nin vector extension'ı
/// gerektirdiği için düz postgres imajı yetersiz) ve GERÇEK bir Redis testcontainer (kasıtlı
/// erişilemez bir adres StackExchange.Redis'in kendi connect/operation timeout'unu beklettiği
/// için 100-paralel bir testte yapay gecikme yaratır — bkz. Mcp.IntegrationTests/McpHostFixture'daki
/// AYNI not). RabbitMq bilerek erişilemez bırakılır (MassTransit host'u düşürmez, checkout ona
/// senkron bağımlı değil).
///
/// MCP bu branch'te YOK — token, Host'un KENDİ (tek) Jwt issuer/key'i ile doğrudan basılır
/// (Identity'nin JwtTokenService'inin aynısı, test içinde elle tekrarlanır — cross-module
/// test bağımlılığı eklememek için).
/// </summary>
public sealed class HostStackFixture : IAsyncLifetime
{
    public const string JwtIssuer = "ModularCommerce.IntegrationTest";
    public const string JwtAudience = "ModularCommerce.IntegrationTest";
    public const string JwtSigningKey = "integration-test-signing-key-for-checkout-race-32ch!";
    public static readonly Guid TestCustomerId = Guid.Parse("00000000-0000-0000-0000-0000000000bb");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        .WithImage("pgvector/pgvector:pg17")
        .Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public string PostgresConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("ConnectionStrings:RabbitMq", "amqp://guest:guest@localhost:1/");
            builder.UseSetting("Jwt:Issuer", JwtIssuer);
            builder.UseSetting("Jwt:Audience", JwtAudience);
            builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
        });

        // İlk HTTP isteğini beklemeden Host'u gerçekten başlatır — migrate+seed bu noktada biter.
        _ = Factory.Server;

        Client = Factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", MintToken(TestCustomerId));
    }

    public static string MintToken(Guid customerId)
    {
        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            Expires = DateTime.UtcNow.AddMinutes(15),
            Claims = new Dictionary<string, object> { ["sub"] = customerId.ToString() },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
                SecurityAlgorithms.HmacSha256),
        });
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }
}

[CollectionDefinition(nameof(HostStackCollection))]
public sealed class HostStackCollection : ICollectionFixture<HostStackFixture>;
