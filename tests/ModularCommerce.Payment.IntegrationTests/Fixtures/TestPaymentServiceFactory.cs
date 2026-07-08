using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModularCommerce.Payment.Application.Abstractions;
using ModularCommerce.Payment.Infrastructure.ContractAdapters;
using ModularCommerce.Payment.Infrastructure.Persistence;
using ModularCommerce.Payment.Infrastructure.Psp;
using Polly;
using Polly.Registry;

namespace ModularCommerce.Payment.IntegrationTests.Fixtures;

/// <summary>
/// Testler PaymentService'i gerçek Postgres + deterministik sahte PSP ile kurar
/// (FR-6.3: testler kendi PspOptions örneğini verir, oran tabanlı rastgelelik yok).
/// Pipeline pass-through'dur: resiliency davranışı K6 koşularının konusudur, buradaki
/// kanıt idempotency hakemidir.
/// </summary>
public static class TestPaymentServiceFactory
{
    private static readonly ResiliencePipelineProvider<string> PassThroughPipelines = BuildPipelines();

    public static CountingPspClient CreatePspClient(PspOptions options) => new(options);

    public static PaymentService Create(
        PaymentDbContext context,
        CountingPspClient pspClient,
        PspOptions options)
        => new(
            context,
            [new CardPaymentStrategy(PassThroughPipelines, pspClient)],
            options,
            NullLogger<PaymentService>.Instance);

    private static ResiliencePipelineProvider<string> BuildPipelines()
    {
        var services = new ServiceCollection();
        services.AddResiliencePipeline(CardPaymentStrategy.PipelineName, _ => { });
        return services.BuildServiceProvider()
            .GetRequiredService<ResiliencePipelineProvider<string>>();
    }
}

/// <summary>PSP çağrı sayacı: "tek charge" kanıtının PSP tarafı (§10.2).</summary>
public sealed class CountingPspClient(PspOptions options) : IPspClient
{
    private readonly FakePspClient _inner = new(options);
    private int _calls;

    public int Calls => _calls;

    public Task<PspResult> ChargeAsync(PspChargeRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return _inner.ChargeAsync(request, cancellationToken);
    }
}
