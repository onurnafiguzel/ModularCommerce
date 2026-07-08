using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModularCommerce.Payment.Application.Abstractions;
using ModularCommerce.Payment.Contracts;
using ModularCommerce.Payment.Domain.Payments;
using ModularCommerce.Payment.Infrastructure.Persistence;
using ModularCommerce.Payment.Infrastructure.Persistence.Configurations;
using ModularCommerce.Payment.Infrastructure.Psp;
using ModularCommerce.Shared.Kernel;
using Npgsql;
using PaymentAggregate = ModularCommerce.Payment.Domain.Payments.Payment;

namespace ModularCommerce.Payment.Infrastructure.ContractAdapters;

/// <summary>
/// IPaymentService implementasyonu. Double-charge'ın nihai hakemi payments tablosunun
/// (customer_id, idempotency_key) unique index'idir: Pending satırı ÖNCE insert edilir,
/// PSP çağrısı ancak insert kazanılırsa yapılır. Kaybeden, mevcut satırın durumuna göre
/// terminal replay / InFlight / bayat-Pending takeover yollarından birine girer (FR-6.2).
/// </summary>
public sealed class PaymentService(
    PaymentDbContext context,
    IEnumerable<IPaymentMethodStrategy> strategies,
    PspOptions pspOptions,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<Result<PaymentResultDto>> ChargeAsync(
        ChargeRequest request,
        CancellationToken cancellationToken)
    {
        var strategy = strategies.FirstOrDefault(
            s => (int)s.Method == (int)request.Method);
        if (strategy is null)
        {
            return Result.Failure<PaymentResultDto>(PaymentErrors.UnsupportedMethod);
        }

        var paymentResult = PaymentAggregate.Create(
            request.OrderId,
            request.CustomerId,
            request.Amount,
            request.Currency,
            request.IdempotencyKey,
            (Domain.Payments.PaymentMethod)(int)request.Method);
        if (paymentResult.IsFailure)
        {
            return Result.Failure<PaymentResultDto>(paymentResult.Error);
        }

        var payment = paymentResult.Value;

        // 1. hakem: Pending satırını insert etmeyi dene — kazanan PSP'yi arar.
        context.Payments.Add(payment);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: PaymentConfiguration.IdempotencyIndexName,
            })
        {
            // Yarışı kaybettik ya da key daha önce kullanılmış: mevcut satır konuşur.
            return await HandleExistingPaymentAsync(request, strategy, cancellationToken);
        }

        return await ExecuteChargeAsync(payment, strategy, request, cancellationToken);
    }

    /// <summary>PSP koşusu + finalize; satırın sahibi olan yol (insert kazananı veya takeover).</summary>
    private async Task<Result<PaymentResultDto>> ExecuteChargeAsync(
        PaymentAggregate payment,
        IPaymentMethodStrategy strategy,
        ChargeRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await strategy.ExecuteAsync(
            new PspChargeRequest(payment.Id, payment.Amount, payment.Currency, payment.IdempotencyKey),
            cancellationToken);

        if (outcome.Error is not null && outcome.Error == PaymentErrors.PspUnavailable)
        {
            // PSP'ye ulaşılamadı (breaker açık / transient tükendi): sahte PSP'de charge
            // belirsizliği yoktur — Pending satırı SİLİNİR ki aynı key retryable kalsın
            // (Failed finalize edilseydi replay istemciyi terminale kilitlerdi).
            context.Payments.Remove(payment);
            await context.SaveChangesAsync(CancellationToken.None);
            return Result.Failure<PaymentResultDto>(outcome.Error);
        }

        foreach (var attempt in outcome.Attempts)
        {
            payment.RecordAttempt(attempt.Outcome, attempt.TransactionId, attempt.ErrorCode, attempt.LatencyMs);
        }

        if (outcome.Succeeded)
        {
            payment.MarkCompleted(outcome.TransactionId!);
        }
        else
        {
            payment.MarkFailed(outcome.FailureCode!);
        }

        // Finalize iptal edilmez: PSP sonucu alındıysa kalıcı iz düşülmek ZORUNDA (NFR-6.4).
        await context.SaveChangesAsync(CancellationToken.None);

        if (outcome.Succeeded)
        {
            return Result.Success(ToDto(payment));
        }

        logger.LogWarning(
            "Ödeme başarısız finalize edildi: {PaymentId} ({FailureCode})",
            payment.Id, payment.FailureCode);

        return Result.Failure<PaymentResultDto>(outcome.Error!);
    }

    /// <summary>Kaybeden yol: terminal replay (FR-6.2) / taze Pending → InFlight / bayat Pending → takeover.</summary>
    private async Task<Result<PaymentResultDto>> HandleExistingPaymentAsync(
        ChargeRequest request,
        IPaymentMethodStrategy strategy,
        CancellationToken cancellationToken)
    {
        // Insert denemesinin tracker kalıntısı ikinci SaveChanges'e taşınmasın.
        context.ChangeTracker.Clear();

        var existing = await context.Payments
            .Include(p => p.Attempts)
            .FirstOrDefaultAsync(
                p => p.CustomerId == request.CustomerId && p.IdempotencyKey == request.IdempotencyKey,
                cancellationToken);

        if (existing is null)
        {
            // 23505 aldık ama satır görünmüyor: kazananın transaction'ı henüz commit
            // olmamış olabilir — istemci sözleşmesi devrede (aynı key ile tekrar dene).
            return Result.Failure<PaymentResultDto>(PaymentErrors.InFlight);
        }

        switch (existing.Status)
        {
            case PaymentStatus.Completed:
                // Replay güvenliği: aynı key farklı tutara yapıştırılamaz (sepet değişmiş olabilir).
                if (existing.Amount != request.Amount || existing.Currency != request.Currency)
                {
                    return Result.Failure<PaymentResultDto>(PaymentErrors.AmountMismatch);
                }

                return Result.Success(ToDto(existing));

            case PaymentStatus.Failed:
                // Declined/timeout kopyası aynen döner (FR-6.2); yeni deneme = yeni key.
                return Result.Failure<PaymentResultDto>(
                    existing.FailureCode == "timeout"
                        ? PaymentErrors.Timeout
                        : PaymentErrors.Declined(existing.FailureCode));

            default:
                return await HandlePendingPaymentAsync(existing, strategy, request, cancellationToken);
        }
    }

    private async Task<Result<PaymentResultDto>> HandlePendingPaymentAsync(
        PaymentAggregate existing,
        IPaymentMethodStrategy strategy,
        ChargeRequest request,
        CancellationToken cancellationToken)
    {
        var isStale = existing.LastActivityAtUtc
            < DateTime.UtcNow.AddSeconds(-pspOptions.StalePendingSeconds);

        if (!isStale)
        {
            // Taze Pending: sahibi hâlâ çalışıyor — retryable 409, istemci aynı key ile döner.
            return Result.Failure<PaymentResultDto>(PaymentErrors.InFlight);
        }

        // Bayat Pending (sahibi crash etmiş olmalı): satırı devral. Yarışın hakemi xmin —
        // iki devralıcıdan yalnız biri Reclaim'i persist edebilir.
        var reclaim = existing.Reclaim();
        if (reclaim.IsFailure)
        {
            return Result.Failure<PaymentResultDto>(reclaim.Error);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning(
                "Bayat-Pending devralma yarışı kaybedildi: {PaymentId}", existing.Id);
            return Result.Failure<PaymentResultDto>(PaymentErrors.InFlight);
        }

        logger.LogWarning(
            "Bayat Pending ödeme devralındı: {PaymentId} (son etkinlik {LastActivityAtUtc})",
            existing.Id, existing.LastActivityAtUtc);

        return await ExecuteChargeAsync(existing, strategy, request, cancellationToken);
    }

    private static PaymentResultDto ToDto(PaymentAggregate payment)
        => new(
            payment.Id,
            payment.Amount,
            payment.Currency,
            payment.PspTransactionId,
            payment.CompletedAtUtc);
}
