using FluentAssertions;
using ModularCommerce.Payment.Domain.Payments;
using Xunit;

namespace ModularCommerce.Payment.UnitTests.Domain;

/// <summary>
/// retryable-409 istemci sözleşmesinin domain seviyesindeki kanıtı (FR-6.2): geçici hatalar
/// (InFlight/PspUnavailable) AYNI key ile tekrar denenmeli → Retryable=true; terminal hatalar
/// (Declined/Timeout) YENİ key gerektirir → Retryable=false. HTTP katmanı bunu gövdeye yansıtır.
/// </summary>
public sealed class PaymentErrorContractTests
{
    [Fact(DisplayName = "Geçici ödeme hataları retryable işaretlidir (aynı key ile tekrar)")]
    public void TransientErrors_AreRetryable()
    {
        PaymentErrors.InFlight.Retryable.Should().BeTrue();
        PaymentErrors.PspUnavailable.Retryable.Should().BeTrue();
    }

    [Fact(DisplayName = "Terminal ödeme hataları retryable DEĞİLDİR (yeni key gerekir)")]
    public void TerminalErrors_AreNotRetryable()
    {
        PaymentErrors.Declined(null).Retryable.Should().BeFalse();
        PaymentErrors.Timeout.Retryable.Should().BeFalse();
    }
}
