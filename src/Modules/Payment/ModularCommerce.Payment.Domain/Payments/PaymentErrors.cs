using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Payment.Domain.Payments;

public static class PaymentErrors
{
    /// <summary>Terminal iş reddi: aynı key ile tekrar aynı sonuç döner; yeni deneme = YENİ key.</summary>
    public static Error Declined(string? code) => Error.Conflict(
        "Payment.Declined",
        $"Ödeme reddedildi{(code is null ? "" : $" ({code})")}. Yeni bir deneme için yeni bir Idempotency-Key kullanın.");

    /// <summary>Retryable: aynı key ile devam eden bir ödeme var — istemci aynı key ile tekrar dener.</summary>
    public static readonly Error InFlight = Error.Conflict(
        "Payment.InFlight",
        "Bu istek için ödeme zaten işleniyor, lütfen tekrar deneyin.");

    /// <summary>Retryable: circuit breaker açık — PSP korumaya alındı.</summary>
    public static readonly Error PspUnavailable = Error.Conflict(
        "Payment.PspUnavailable",
        "Ödeme sağlayıcısına şu anda ulaşılamıyor, lütfen tekrar deneyin.");

    /// <summary>Terminal: pipeline (retry'lar dahil) zaman bütçesini tüketti.</summary>
    public static readonly Error Timeout = Error.Conflict(
        "Payment.Timeout",
        "Ödeme zaman aşımına uğradı. Yeni bir deneme için yeni bir Idempotency-Key kullanın.");

    /// <summary>Replay güvenliği: aynı key'in ödemesi farklı tutara aitti (sepet değişmiş).</summary>
    public static readonly Error AmountMismatch = Error.Conflict(
        "Payment.AmountMismatch",
        "Bu Idempotency-Key farklı bir tutar için kullanılmış. Yeni bir key ile tekrar deneyin.");

    public static readonly Error UnsupportedMethod = Error.Validation(
        "Payment.UnsupportedMethod",
        "Bu ödeme yöntemi henüz desteklenmiyor.");

    public static readonly Error InvalidRequest = Error.Validation(
        "Payment.InvalidRequest",
        "Ödeme isteği geçersiz.");

    /// <summary>Terminal satır değiştirilemez (NFR-6.4) — programlama hatası koruması.</summary>
    public static readonly Error AlreadyFinalized = Error.Conflict(
        "Payment.AlreadyFinalized",
        "Ödeme zaten sonuçlanmış; terminal durum değiştirilemez.");
}
