using ModularCommerce.Payment.Domain.Payments;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Payment.Application.Abstractions;

/// <summary>
/// Ödeme yöntemi stratejisi (FR-6.5): kart/cüzdan/havale takılıp çıkarılabilir.
/// Strateji, resiliency pipeline'ını PSP çağrısının etrafında koşturur ve her denemeyi
/// (retry'lar dahil) kaydeder; kalıcılık kararı vermez — o contract adapter'ın işidir.
/// </summary>
public interface IPaymentMethodStrategy
{
    PaymentMethod Method { get; }
    Task<PspChargeOutcome> ExecuteAsync(PspChargeRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Strateji koşusunun toplam sonucu: başarıda TransactionId dolu; başarısızlıkta
/// FailureCode (audit) + Error (çağırana aynen dönecek sözleşme hatası) dolu.
/// Attempts her durumda PSP çağrı geçmişini taşır (NFR-6.4).
/// </summary>
public sealed record PspChargeOutcome(
    IReadOnlyList<PspAttemptLog> Attempts,
    string? TransactionId,
    string? FailureCode,
    Error? Error)
{
    public bool Succeeded => TransactionId is not null;

    public static PspChargeOutcome Success(IReadOnlyList<PspAttemptLog> attempts, string transactionId)
        => new(attempts, transactionId, null, null);

    public static PspChargeOutcome Failure(IReadOnlyList<PspAttemptLog> attempts, string failureCode, Error error)
        => new(attempts, null, failureCode, error);
}

/// <summary>Tek PSP çağrı denemesinin izi; adapter bunu PaymentAttempt'e çevirir.</summary>
public sealed record PspAttemptLog(
    PaymentAttemptOutcome Outcome,
    string? TransactionId,
    string? ErrorCode,
    long LatencyMs);
