namespace ModularCommerce.Payment.Application.Abstractions;

/// <summary>
/// Sahte/gerçek PSP istemcisi. İş sonuçları (onay/red) PspResult olarak DÖNER;
/// geçici altyapı hataları (ağ, 5xx) PspTransientException FIRLATIR — Polly retry
/// ayrımı bu tip üzerinden yapılır (declined'a retry uygulanmaz, FR-6.3/NFR-6.1).
/// </summary>
public interface IPspClient
{
    Task<PspResult> ChargeAsync(PspChargeRequest request, CancellationToken cancellationToken);
}

/// <summary>PSP'ye giden istek; IdempotencyKey gerçek PSP'lerde de taşınır (double-charge koruması).</summary>
public sealed record PspChargeRequest(
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string IdempotencyKey);

/// <summary>PSP iş sonucu: onaylandı ya da reddedildi (declined kodu ile).</summary>
public sealed record PspResult(bool Approved, string? TransactionId, string? DeclineCode);

/// <summary>Geçici PSP altyapı hatası — retry edilebilir (declined DEĞİL).</summary>
public sealed class PspTransientException(string message) : Exception(message);
