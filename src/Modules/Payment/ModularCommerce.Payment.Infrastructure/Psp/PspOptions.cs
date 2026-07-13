using System.ComponentModel.DataAnnotations;

namespace ModularCommerce.Payment.Infrastructure.Psp;

/// <summary>
/// Sahte PSP simülasyon ayarları (FR-6.3): oranlar 0..1 aralığındadır ve 0 default'uyla
/// normal koşu deterministiktir. K6 resiliency koşuları bu oranları env ile yükseltir.
/// Doğrulama deklaratif (`[Range]`) — AddValidatedOptions ile başlangıçta fail-fast.
/// </summary>
public sealed record PspOptions
{
    public const string SectionName = "Payment:Psp";

    /// <summary>PSP'nin normal yanıt gecikmesi (ms).</summary>
    [Range(0, int.MaxValue)]
    public int LatencyMs { get; init; } = 50;

    /// <summary>İş reddi oranı (declined) — terminal, retry edilmez.</summary>
    [Range(0.0, 1.0)]
    public double DeclineRate { get; init; }

    /// <summary>Geçici altyapı hatası oranı — PspTransientException, retry tetikler.</summary>
    [Range(0.0, 1.0)]
    public double FailureRate { get; init; }

    /// <summary>Deneme-başı timeout'u aşan takılma oranı.</summary>
    [Range(0.0, 1.0)]
    public double TimeoutRate { get; init; }

    /// <summary>Pending satır bu süreden eskiyse devralınabilir (crash kurtarma).</summary>
    [Range(1, int.MaxValue)]
    public int StalePendingSeconds { get; init; } = 30;
}
