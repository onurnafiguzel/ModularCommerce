extern alias McpAssembly;

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using McpAssembly::ModularCommerce.Mcp.Auth;
using McpAssembly::ModularCommerce.Mcp.Clients;
using ModularCommerce.Shared.Infrastructure.Auth;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace ModularCommerce.Mcp.IntegrationTests;

/// <summary>
/// Gerçek ModularCommerce.Host'u (tüm 9 modül) WebApplicationFactory ile in-memory ayağa kaldırır
/// — pgvector'lı bir Postgres testcontainer'a karşı (Discovery'nin vector extension'ı gerektirdiği
/// için, TestKit'in PostgresFixture'ındaki düz postgres imajı burada YETERSİZ). Redis de GERÇEK bir
/// testcontainer — kasıtlı erişilemez bir adres (ör. localhost:1) kullanmak İŞE YARAMIYOR: Redis
/// cache decorator'ları (RedisCartCache vb.) AbortOnConnectFail=false sayesinde çökmüyor ama her
/// işlemde StackExchange.Redis'in kendi connect/operation timeout'unu (saniyeler) bekleyip
/// Postgres'e düşüyor — bu, 100-paralel bir testte TÜM istekleri yapay olarak saniyelerce
/// yavaşlatıp Polly'nin KENDİ timeout'una (üretim: 3s toplam) takılmasına yol açtı (gözlemlendi,
/// debug edildi). RabbitMq ise BİLEREK erişilemez bırakılıyor: MassTransit bağlantı hatasında
/// host'u düşürmüyor (bu repo'da doğrulanmış davranış) ve checkout akışı ona senkron bağımlı değil
/// — Redis'teki gibi "sessizce saniyelerce bekleyen" bir cache-okuma yolu burada yok.
///
/// ASPNETCORE_ENVIRONMENT=Development, MigrateAndSeedHostedService'in (dolayısıyla Catalog'un
/// otomatik ürün seed'inin) çalışması için şart.
///
/// ApiClient, gerçek ağ yerine Factory.Server.CreateHandler() ile TESTSERVER'A DOĞRUDAN bağlanır;
/// Mcp'nin KENDİ X-Api-Key auth katmanı burada devrede DEĞİL (o zaten Adım 2'de ayrıca test
/// edildi) — bunun yerine sabit bir TestCustomerId için gerçek IServiceJwtIssuer ile basılmış bir
/// JWT, FixedCustomerAuthHandler ile her isteğe eklenir; böylece gerçek Host'un JWT doğrulaması
/// (Jwt:Mcp issuer/key) ve ownership kontrolleri (user.GetUserId()) uçtan uca çalışır. Resilience
/// pipeline ÜRETİM ayarlarıyla (AddModularCommerceApiClient, değiştirilmeden) kuruluyor — Redis
/// artık gerçek olduğu için üretim bütçesini gevşetmeye gerek kalmadı.
/// </summary>
public sealed class McpHostFixture : IAsyncLifetime
{
    public const string McpIssuer = "modularcommerce-mcp";
    public const string McpSigningKey = "test-mcp-signing-key-for-integration-tests-32-chars!!";
    public static readonly Guid TestCustomerId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        .WithImage("pgvector/pgvector:pg17")
        .Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public ModularCommerceApiClient ApiClient { get; private set; } = null!;

    /// <summary>Testlerin yarış-sonrası DB'yi (payments/reservations/outbox) ham SQL ile
    /// doğrulaması için — Host'un KENDİ DbContext'leri yerine (modül iç katmanına referans
    /// gerektirir, Mcp.IntegrationTests bunu bilerek yapmaz) doğrudan Npgsql kullanılır.</summary>
    public string PostgresConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            // ConfigureAppConfiguration DEĞİL: minimal hosting (top-level statement Program.cs)
            // + WebApplicationFactory kombinasyonunda güvenilir biçimde çalışan, topluluk tarafından
            // önerilen yol UseSetting — host configuration'a doğrudan, yüksek öncelikle yazar.
            builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("ConnectionStrings:RabbitMq", "amqp://guest:guest@localhost:1/");
            builder.UseSetting("Jwt:Mcp:Issuer", McpIssuer);
            builder.UseSetting("Jwt:Mcp:SigningKey", McpSigningKey);
        });

        // İlk HTTP isteğini beklemeden Host'u gerçekten başlatır — migrate+seed bu noktada biter.
        _ = Factory.Server;

        var jwtIssuer = new McpServiceJwtIssuer(Options.Create(new JwtOptions
        {
            Audience = "ModularCommerce",
            Mcp = new JwtIssuerOptions { Issuer = McpIssuer, SigningKey = McpSigningKey },
        }));

        var services = new ServiceCollection();
        services.AddSingleton<IServiceJwtIssuer>(jwtIssuer);
        services.AddTransient<FixedCustomerAuthHandler>();
        services
            .AddModularCommerceApiClient("http://localhost")
            .ConfigurePrimaryHttpMessageHandler(() => Factory.Server.CreateHandler())
            .AddHttpMessageHandler<FixedCustomerAuthHandler>();

        ApiClient = services.BuildServiceProvider().GetRequiredService<ModularCommerceApiClient>();
    }

    /// <summary>
    /// AYNI (zaten migrate+seed edilmiş) Postgres/Redis testcontainer'larına karşı, ama
    /// ASPNETCORE_ENVIRONMENT=Production ile ikinci bir Host instance'ı kurar. Production'da
    /// <see cref="ModularCommerce.Shared.Infrastructure.Persistence.MigrateAndSeedHostedService{TContext}"/>
    /// RUN_MIGRATIONS=true olmadan migrate/seed ÇALIŞTIRMIYOR (bkz. kaynağı) — burada sorun değil,
    /// şema zaten Development factory'si tarafından oluşturuldu. Tek amaç: dev-only endpoint'lerin
    /// (ör. Inventory'nin PUT /dev/stock'u) gerçek Production ortam adında da 404 verdiğini kanıtlamak.
    /// </summary>
    public WebApplicationFactory<Program> CreateProductionFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("ConnectionStrings:RabbitMq", "amqp://guest:guest@localhost:1/");
            builder.UseSetting("Jwt:Mcp:Issuer", McpIssuer);
            builder.UseSetting("Jwt:Mcp:SigningKey", McpSigningKey);
        });

    public async Task DisposeAsync()
    {
        Factory.Dispose();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private sealed class FixedCustomerAuthHandler(IServiceJwtIssuer issuer) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", issuer.IssueToken(TestCustomerId));
            return base.SendAsync(request, cancellationToken);
        }
    }
}
