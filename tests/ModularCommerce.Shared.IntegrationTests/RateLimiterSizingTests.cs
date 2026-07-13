using System.Threading.RateLimiting;
using FluentAssertions;
using Xunit;

namespace ModularCommerce.Shared.IntegrationTests;

/// <summary>
/// Hız sınırlama boyutlandırma doğrulaması (web host'suz, sliding-window primitifiyle):
/// checkout policy 100-paralel burst'ü kabul etmeli (§10.2 kırılmaz), auth policy limiti
/// aşınca reddetmeli (429 yolu).
/// </summary>
public sealed class RateLimiterSizingTests
{
    [Fact(DisplayName = "Checkout policy: 100 paralel burst tamamı kabul (§10.2 korunur)")]
    public void Checkout_BurstOf100_AllAdmitted()
    {
        using var limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromSeconds(10),
            SegmentsPerWindow = 4,
            QueueLimit = 120,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });

        var acquired = 0;
        for (var i = 0; i < 100; i++)
        {
            if (limiter.AttemptAcquire(1).IsAcquired)
            {
                acquired++;
            }
        }

        acquired.Should().Be(100, "PermitLimit(120) ≥ 100 → 100-paralel burst reddedilmemeli");
    }

    [Fact(DisplayName = "Auth policy: PermitLimit kadar kabul, sonrası 429")]
    public void Auth_ExceedingPermit_IsRejected()
    {
        using var limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromSeconds(60),
            SegmentsPerWindow = 4,
            QueueLimit = 0,
        });

        for (var i = 0; i < 5; i++)
        {
            limiter.AttemptAcquire(1).IsAcquired.Should().BeTrue($"{i + 1}. istek limit içinde");
        }

        limiter.AttemptAcquire(1).IsAcquired.Should().BeFalse("limit aşıldı → reddedilmeli (429)");
    }
}
