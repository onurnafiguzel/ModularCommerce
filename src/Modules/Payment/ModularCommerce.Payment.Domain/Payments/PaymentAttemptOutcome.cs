namespace ModularCommerce.Payment.Domain.Payments;

/// <summary>Tek bir PSP çağrı denemesinin sonucu (Polly retry'ları dahil her deneme kaydedilir).</summary>
public enum PaymentAttemptOutcome
{
    Success = 0,
    Declined = 1,
    TransientError = 2,
    Timeout = 3,
}
