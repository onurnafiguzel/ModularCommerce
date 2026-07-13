using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularCommerce.Payment.Api.Endpoints;
using ModularCommerce.Payment.Application.Abstractions;
using ModularCommerce.Payment.Contracts;
using ModularCommerce.Payment.Infrastructure.ContractAdapters;
using ModularCommerce.Payment.Infrastructure.Persistence;
using ModularCommerce.Payment.Infrastructure.Psp;
using ModularCommerce.Shared.Infrastructure.Modules;
using ModularCommerce.Shared.Infrastructure.Persistence;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace ModularCommerce.Payment.Api;

public sealed class PaymentModule : IModule
{
    public string Name => "Payment";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PaymentDbContext>(configuration, PaymentDbContext.Schema);

        var pspOptions = configuration.GetSection(PspOptions.SectionName).Get<PspOptions>()
            ?? new PspOptions();
        ValidateOptions(pspOptions);
        services.AddSingleton(pspOptions);

        services.AddSingleton<IPspClient, FakePspClient>();
        services.AddSingleton<IPaymentMethodStrategy, CardPaymentStrategy>();
        services.AddScoped<IPaymentService, PaymentService>();

        // NFR-6.1 resiliency pipeline'ı (dıştan içe): toplam timeout → retry → circuit
        // breaker → bulkhead → deneme-başı timeout. Declined İŞ sonucudur, exception
        // değildir — bu pipeline yalnız transient hata ve timeout'ları görür.
        services.AddResiliencePipeline(CardPaymentStrategy.PipelineName, builder =>
        {
            var transientErrors = new PredicateBuilder()
                .Handle<PspTransientException>()
                .Handle<TimeoutRejectedException>();

            builder
                // NFR-6.2: timeout üst sınırı 3 sn — retry'lar dahil toplam bütçe.
                .AddTimeout(TimeSpan.FromSeconds(3))
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = transientErrors,
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(100),
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    ShouldHandle = transientErrors,
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(10),
                    MinimumThroughput = 8,
                    BreakDuration = TimeSpan.FromSeconds(5),
                })
                // Bulkhead: PSP'ye aynı anda en fazla 20 istek, 20 kuyruk.
                .AddConcurrencyLimiter(permitLimit: 20, queueLimit: 20)
                .AddTimeout(TimeSpan.FromSeconds(1));
        });

        // Polly telemetrisini ILogger'a bağlar — breaker open/close logları buradan
        // gelir (kanıt yükümlülüğü §10.3).
        services.AddResilienceEnricher();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/payment");
        var isDevelopment = endpoints.ServiceProvider
            .GetRequiredService<IHostEnvironment>()
            .IsDevelopment();

        group.MapPaymentDevEndpoints(isDevelopment);
    }

    private static void ValidateOptions(PspOptions options)
    {
        if (options.DeclineRate is < 0 or > 1
            || options.FailureRate is < 0 or > 1
            || options.TimeoutRate is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Payment:Psp oranları (DeclineRate/FailureRate/TimeoutRate) 0 ile 1 arasında olmalıdır.");
        }

        if (options.LatencyMs < 0 || options.StalePendingSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Payment:Psp:LatencyMs negatif olamaz; StalePendingSeconds pozitif olmalıdır.");
        }
    }
}
