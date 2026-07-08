namespace ModularCommerce.Payment.Domain.Payments;

/// <summary>
/// Değiştirilemez ödeme denemesi kaydı (NFR-6.4): her PSP çağrısı — retry'lar dahil —
/// bir satırdır ve asla güncellenmez. Payment aggregate'ine owned olarak bağlıdır.
/// </summary>
public sealed class PaymentAttempt
{
    public int AttemptNumber { get; private set; }
    public PaymentAttemptOutcome Outcome { get; private set; }
    public string? PspTransactionId { get; private set; }
    public string? ErrorCode { get; private set; }
    public long LatencyMs { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>EF Core materialization için; uygulama kodu asla çağırmaz.</summary>
    private PaymentAttempt()
    {
    }

    internal PaymentAttempt(
        int attemptNumber,
        PaymentAttemptOutcome outcome,
        string? pspTransactionId,
        string? errorCode,
        long latencyMs)
    {
        AttemptNumber = attemptNumber;
        Outcome = outcome;
        PspTransactionId = pspTransactionId;
        ErrorCode = errorCode;
        LatencyMs = latencyMs;
        OccurredAtUtc = DateTime.UtcNow;
    }
}
