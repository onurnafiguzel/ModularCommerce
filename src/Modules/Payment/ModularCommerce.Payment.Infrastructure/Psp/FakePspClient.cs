using ModularCommerce.Payment.Application.Abstractions;

namespace ModularCommerce.Payment.Infrastructure.Psp;

/// <summary>
/// Sahte PSP (FR-6.1/6.3): success / declined / transient hata / timeout senaryolarını
/// config-driven oranlarla simüle eder. Oranlar 0 iken tamamen deterministiktir;
/// testler kendi PspOptions örneğiyle deterministik davranış kurar.
/// </summary>
public sealed class FakePspClient(PspOptions options) : IPspClient
{
    /// <summary>Deneme-başı timeout'un (1 sn) güvenle üzerinde — Polly iptal eder.</summary>
    private static readonly TimeSpan HangDuration = TimeSpan.FromSeconds(10);

    public async Task<PspResult> ChargeAsync(PspChargeRequest request, CancellationToken cancellationToken)
    {
        if (Roll(options.TimeoutRate))
        {
            // Takılan PSP: yanıt hiç gelmez, deneme-başı timeout devreye girer.
            await Task.Delay(HangDuration, cancellationToken);
        }

        if (options.LatencyMs > 0)
        {
            await Task.Delay(options.LatencyMs, cancellationToken);
        }

        if (Roll(options.FailureRate))
        {
            throw new PspTransientException("psp_5xx");
        }

        if (Roll(options.DeclineRate))
        {
            return new PspResult(Approved: false, TransactionId: null, DeclineCode: "insufficient_funds");
        }

        return new PspResult(
            Approved: true,
            TransactionId: $"psp-{Guid.NewGuid():N}",
            DeclineCode: null);
    }

    private static bool Roll(double rate)
        => rate > 0 && Random.Shared.NextDouble() < rate;
}
