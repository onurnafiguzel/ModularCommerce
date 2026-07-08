using System.Diagnostics;
using ModularCommerce.Payment.Application.Abstractions;
using ModularCommerce.Payment.Domain.Payments;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;

namespace ModularCommerce.Payment.Infrastructure.Psp;

/// <summary>
/// Kart stratejisi (FR-6.5'in ilk implementasyonu): resiliency pipeline'ını (NFR-6.1)
/// PSP çağrısının etrafında koşturur ve her denemeyi — retry'lar dahil — kaydeder.
/// Declined bir İŞ sonucudur, pipeline'dan geçmeden aynen döner (retry edilmez).
/// </summary>
public sealed class CardPaymentStrategy(
    ResiliencePipelineProvider<string> pipelineProvider,
    IPspClient pspClient) : IPaymentMethodStrategy
{
    public const string PipelineName = "payment-psp";

    public PaymentMethod Method => PaymentMethod.Card;

    public async Task<PspChargeOutcome> ExecuteAsync(
        PspChargeRequest request,
        CancellationToken cancellationToken)
    {
        var attempts = new List<PspAttemptLog>();
        var pipeline = pipelineProvider.GetPipeline(PipelineName);

        try
        {
            var result = await pipeline.ExecuteAsync(
                async token => await ChargeOnceAsync(request, attempts, token),
                cancellationToken);

            return result.Approved
                ? PspChargeOutcome.Success(attempts, result.TransactionId!)
                : PspChargeOutcome.Failure(
                    attempts,
                    result.DeclineCode ?? "declined",
                    PaymentErrors.Declined(result.DeclineCode));
        }
        catch (BrokenCircuitException)
        {
            // Breaker açık: PSP'ye hiç gidilmedi — charge belirsizliği YOK, key yeniden
            // kullanılabilir (adapter Pending satırı serbest bırakır, istemci retry eder).
            return PspChargeOutcome.Failure(attempts, "circuit_open", PaymentErrors.PspUnavailable);
        }
        catch (TimeoutRejectedException)
        {
            // Toplam bütçe (3 sn) tükendi ya da deneme timeout'ları retry'ı da tüketti.
            return PspChargeOutcome.Failure(attempts, "timeout", PaymentErrors.Timeout);
        }
        catch (PspTransientException)
        {
            // Retry'lar tükendi, PSP hâlâ hata veriyor: sahte PSP'de transient hata
            // charge üretmez — key yeniden kullanılabilir, istemci retry eder.
            return PspChargeOutcome.Failure(attempts, "psp_unavailable", PaymentErrors.PspUnavailable);
        }
    }

    private async Task<PspResult> ChargeOnceAsync(
        PspChargeRequest request,
        List<PspAttemptLog> attempts,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await pspClient.ChargeAsync(request, cancellationToken);
            stopwatch.Stop();

            attempts.Add(new PspAttemptLog(
                result.Approved ? PaymentAttemptOutcome.Success : PaymentAttemptOutcome.Declined,
                result.TransactionId,
                result.DeclineCode,
                stopwatch.ElapsedMilliseconds));

            return result;
        }
        catch (PspTransientException ex)
        {
            stopwatch.Stop();
            attempts.Add(new PspAttemptLog(
                PaymentAttemptOutcome.TransientError, null, ex.Message, stopwatch.ElapsedMilliseconds));
            throw;
        }
        catch (OperationCanceledException)
        {
            // Deneme-başı timeout'un iptali: Polly bunu TimeoutRejectedException'a çevirir.
            stopwatch.Stop();
            attempts.Add(new PspAttemptLog(
                PaymentAttemptOutcome.Timeout, null, "attempt_timeout", stopwatch.ElapsedMilliseconds));
            throw;
        }
    }
}
