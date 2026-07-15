using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularCommerce.Discovery.Api.Endpoints;
using ModularCommerce.Discovery.Application.Abstractions;
using ModularCommerce.Discovery.Application.Indexing;
using ModularCommerce.Discovery.Application.Search;
using ModularCommerce.Discovery.Infrastructure.Embedding;
using ModularCommerce.Discovery.Infrastructure.Persistence;
using ModularCommerce.Shared.Infrastructure.Configuration;
using ModularCommerce.Shared.Infrastructure.Modules;
using ModularCommerce.Shared.Infrastructure.Persistence;
using Npgsql;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace ModularCommerce.Discovery.Api;

public sealed class DiscoveryModule : IModule
{
    public string Name => "Discovery";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<EmbeddingOptions>(configuration, EmbeddingOptions.SectionName);

        // pgvector: DbContext'in düz connection string yerine `.UseVector()` kurulmuş NpgsqlDataSource
        // kullanması gerekir (tek entegrasyon sürtünmesi). DbContext configure hook'uyla data source'a
        // yönlendirilir; repo da aynı singleton'ı kullanır.
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database bulunamadı");

        services.AddModuleDbContext<DiscoveryDbContext>(configuration, DiscoveryDbContext.Schema,
            configure: (sp, options) => options.UseNpgsql(
                sp.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", DiscoveryDbContext.Schema)));

        // Data source AddModuleDbContext'TEN SONRA kaydedilir ki (EF bir NpgsqlDataSource kaydettiyse)
        // bizimki (UseVector'lı) kazansın. KRİTİK: NpgsqlDataSource tip kataloğunu İLK bağlantıda
        // önbelleğe alır — `vector` tipi o an yoksa parametre eşlemesi kırılır. Bu yüzden data source'u
        // kurmadan ÖNCE, ayrı bir bağlantıyla extension'ı garanti ederiz (migration'daki CREATE EXTENSION
        // aynı bağlantıda ve tip-kataloğu yüklemesinden SONRA çalıştığı için tek başına yetmez).
        services.AddSingleton(_ =>
        {
            using (var provisioning = new NpgsqlConnection(connectionString))
            {
                provisioning.Open();
                using var command = provisioning.CreateCommand();
                command.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
                command.ExecuteNonQuery();
            }

            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseVector();
            return builder.Build();
        });

        services.AddScoped<IProductVectorRepository, ProductVectorRepository>();

        // Embedding sağlayıcısı config ile seçilir (provider hardcode DEĞİL). Default: Fake (sırsız).
        var provider = configuration.GetValue($"{EmbeddingOptions.SectionName}:Provider", EmbeddingProvider.Fake);
        if (provider == EmbeddingProvider.Http)
        {
            RegisterHttpEmbedding(services);
        }
        else
        {
            services.AddScoped<IEmbeddingService, FakeEmbeddingService>();
        }

        services.AddScoped<IndexProductHandler>();
        services.AddScoped<SearchProductsHandler>();
        services.AddValidatorsFromAssemblyContaining<SearchQueryValidator>(includeInternalTypes: true);
    }

    // Gerçek sağlayıcı: HttpClient + adı verilen resilience pipeline (Payment PSP deseni).
    private static void RegisterHttpEmbedding(IServiceCollection services)
    {
        services.AddResiliencePipeline(HttpEmbeddingService.PipelineName, builder =>
        {
            var transient = new PredicateBuilder()
                .Handle<EmbeddingTransientException>()
                .Handle<TimeoutRejectedException>();

            builder
                .AddTimeout(TimeSpan.FromSeconds(10))                      // toplam bütçe
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = transient,
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(200),
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    ShouldHandle = transient,
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(10),
                    MinimumThroughput = 4,
                    BreakDuration = TimeSpan.FromSeconds(5),
                })
                .AddTimeout(TimeSpan.FromSeconds(5));                      // deneme-başı
        });
        services.AddResilienceEnricher();

        services.AddHttpClient<IEmbeddingService, HttpEmbeddingService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/discovery");
        group.MapSearchEndpoints();
    }
}
