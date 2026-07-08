namespace ModularCommerce.Payment.Domain.Payments;

/// <summary>
/// Ödeme yaşam döngüsü: Pending doğar, tam bir kez terminale (Completed/Failed) geçer.
/// Terminal satır asla mutasyona uğramaz — idempotency replay'inin kaynağıdır (FR-6.2).
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
}
