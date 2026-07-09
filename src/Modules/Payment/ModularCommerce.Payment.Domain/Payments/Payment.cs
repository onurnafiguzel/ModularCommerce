using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Payment.Domain.Payments;

/// <summary>
/// Ödeme aggregate'i. (CustomerId, IdempotencyKey) ikilisi double-charge'ın nihai
/// hakemidir (DB unique index — FR-6.2); satır Pending doğar, tam bir kez terminale
/// geçer ve terminal satır asla mutasyona uğramaz. Her PSP çağrı denemesi append-only
/// PaymentAttempt kaydıdır (NFR-6.4).
/// </summary>
public sealed class Payment : Entity
{
    public const int IdempotencyKeyMaxLength = 64; // Ordering'in key sözleşmesiyle aynı

    private readonly List<PaymentAttempt> _attempts = [];

    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public PaymentMethod Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? PspTransactionId { get; private set; }
    public string? FailureCode { get; private set; }
    public string? RefundTransactionId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? RefundedAtUtc { get; private set; }

    /// <summary>Bayat-Pending devralmasının (takeover) zaman damgası; tazelik ölçümü bunu da sayar.</summary>
    public DateTime? ClaimedAtUtc { get; private set; }

    public IReadOnlyList<PaymentAttempt> Attempts => _attempts.AsReadOnly();

    /// <summary>EF Core materialization için; uygulama kodu asla çağırmaz.</summary>
    private Payment()
    {
    }

    private Payment(
        Guid orderId,
        Guid customerId,
        decimal amount,
        string currency,
        string idempotencyKey,
        PaymentMethod method)
    {
        OrderId = orderId;
        CustomerId = customerId;
        Amount = amount;
        Currency = currency;
        IdempotencyKey = idempotencyKey;
        Method = method;
        Status = PaymentStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Result<Payment> Create(
        Guid orderId,
        Guid customerId,
        decimal amount,
        string currency,
        string idempotencyKey,
        PaymentMethod method)
    {
        if (orderId == Guid.Empty
            || customerId == Guid.Empty
            || amount <= 0
            || currency.Length != 3)
        {
            return Result.Failure<Payment>(PaymentErrors.InvalidRequest);
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > IdempotencyKeyMaxLength)
        {
            return Result.Failure<Payment>(PaymentErrors.InvalidRequest);
        }

        return Result.Success(new Payment(
            orderId, customerId, amount, currency.ToUpperInvariant(), idempotencyKey, method));
    }

    /// <summary>Pending'in son etkinlik anı: takeover yapılmadıysa doğum anıdır.</summary>
    public DateTime LastActivityAtUtc => ClaimedAtUtc ?? CreatedAtUtc;

    /// <summary>
    /// Bayat Pending satırını devralır (crash'te yarım kalan ödemenin kilidini açar).
    /// Devralan, satırı tazeler — eşzamanlı rakipler taze Pending görüp InFlight döner;
    /// yarışın hakemi xmin concurrency token'ıdır (kalıcılık katmanında).
    /// </summary>
    public Result Reclaim()
    {
        if (Status != PaymentStatus.Pending)
        {
            return Result.Failure(PaymentErrors.AlreadyFinalized);
        }

        ClaimedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>Append-only deneme kaydı (NFR-6.4); yalnız Pending'de eklenebilir.</summary>
    public Result RecordAttempt(
        PaymentAttemptOutcome outcome,
        string? pspTransactionId,
        string? errorCode,
        long latencyMs)
    {
        if (Status != PaymentStatus.Pending)
        {
            return Result.Failure(PaymentErrors.AlreadyFinalized);
        }

        _attempts.Add(new PaymentAttempt(
            _attempts.Count + 1, outcome, pspTransactionId, errorCode, latencyMs));

        return Result.Success();
    }

    public Result MarkCompleted(string pspTransactionId)
    {
        if (Status != PaymentStatus.Pending)
        {
            return Result.Failure(PaymentErrors.AlreadyFinalized);
        }

        Status = PaymentStatus.Completed;
        PspTransactionId = pspTransactionId;
        CompletedAtUtc = DateTime.UtcNow;

        Raise(new PaymentCompleted(Id, OrderId, CustomerId, Amount, Currency, CompletedAtUtc.Value));

        return Result.Success();
    }

    public Result MarkFailed(string failureCode)
    {
        if (Status != PaymentStatus.Pending)
        {
            return Result.Failure(PaymentErrors.AlreadyFinalized);
        }

        Status = PaymentStatus.Failed;
        FailureCode = failureCode;
        CompletedAtUtc = DateTime.UtcNow;

        Raise(new PaymentFailed(Id, OrderId, CustomerId, failureCode, CompletedAtUtc.Value));

        return Result.Success();
    }

    /// <summary>
    /// Tamamlanmış ödemeyi iade eder (W9 kapsamlı Cancel). İdempotenttir: zaten iade edilmişse
    /// no-op başarı (aynı sipariş iki kez iptal edilirse çift iade olmaz). İade denemesi de
    /// değiştirilemez audit satırı bırakır (NFR-6.4). Yalnız Completed → Refunded (terminal
    /// immutability'yi bozmadan tek yönlü geçiş).
    /// </summary>
    public Result Refund(string refundTransactionId)
    {
        if (Status == PaymentStatus.Refunded)
        {
            return Result.Success();
        }

        if (Status != PaymentStatus.Completed)
        {
            return Result.Failure(PaymentErrors.NotRefundable);
        }

        Status = PaymentStatus.Refunded;
        RefundTransactionId = refundTransactionId;
        RefundedAtUtc = DateTime.UtcNow;

        // İade audit'i (RecordAttempt yalnız Pending'de eklerdi; iade Completed'dan yapılır).
        _attempts.Add(new PaymentAttempt(
            _attempts.Count + 1, PaymentAttemptOutcome.Success, refundTransactionId, "refund", 0));

        Raise(new PaymentRefunded(Id, OrderId, CustomerId, Amount, RefundedAtUtc.Value));

        return Result.Success();
    }
}
