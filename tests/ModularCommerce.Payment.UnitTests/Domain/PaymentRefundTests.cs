using FluentAssertions;
using ModularCommerce.Payment.Domain.Payments;
using Xunit;
using PaymentAggregate = ModularCommerce.Payment.Domain.Payments.Payment;

namespace ModularCommerce.Payment.UnitTests.Domain;

/// <summary>
/// Refund (W9 kapsamlı Cancel): tamamlanmış ödeme iade edilir. Yalnız Completed → Refunded;
/// idempotent (aynı sipariş iki kez iptal → çift iade yok); iade denemesi de audit satırı bırakır.
/// </summary>
public class PaymentRefundTests
{
    private static PaymentAggregate CompletedPayment()
    {
        var payment = PaymentAggregate.Create(
            Guid.NewGuid(), Guid.NewGuid(), 100m, "TRY", "anahtar-1", PaymentMethod.Card).Value;
        payment.RecordAttempt(PaymentAttemptOutcome.Success, "psp-tx-1", null, 40);
        payment.MarkCompleted("psp-tx-1");
        return payment;
    }

    [Fact(DisplayName = "Refund: Completed → Refunded, iade audit satırı eklenir, PaymentRefunded raise")]
    public void Refund_FromCompleted_Succeeds()
    {
        var payment = CompletedPayment();
        var attemptsBefore = payment.Attempts.Count;

        var result = payment.Refund("refund-tx-9");

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundTransactionId.Should().Be("refund-tx-9");
        payment.RefundedAtUtc.Should().NotBeNull();
        payment.Attempts.Should().HaveCount(attemptsBefore + 1, "iade denemesi audit'e eklenir (NFR-6.4)");
        payment.Attempts[^1].ErrorCode.Should().Be("refund");
        payment.DomainEvents.Should().ContainSingle(e => e is PaymentRefunded);
    }

    [Fact(DisplayName = "Refund idempotenttir: ikinci iade no-op başarı, ikinci audit satırı eklenmez")]
    public void Refund_Twice_IsIdempotent()
    {
        var payment = CompletedPayment();
        payment.Refund("refund-tx-1");
        var attemptsAfterFirst = payment.Attempts.Count;

        var second = payment.Refund("refund-tx-2");

        second.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundTransactionId.Should().Be("refund-tx-1", "ilk iade kimliği korunur");
        payment.Attempts.Should().HaveCount(attemptsAfterFirst, "ikinci iade audit satırı eklememeli");
    }

    [Fact(DisplayName = "Pending ödeme iade edilemez → NotRefundable")]
    public void Refund_FromPending_ReturnsNotRefundable()
    {
        var pending = PaymentAggregate.Create(
            Guid.NewGuid(), Guid.NewGuid(), 100m, "TRY", "anahtar-1", PaymentMethod.Card).Value;

        var result = pending.Refund("refund-tx");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.NotRefundable);
        pending.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact(DisplayName = "Başarısız (Failed) ödeme iade edilemez → NotRefundable")]
    public void Refund_FromFailed_ReturnsNotRefundable()
    {
        var failed = PaymentAggregate.Create(
            Guid.NewGuid(), Guid.NewGuid(), 100m, "TRY", "anahtar-1", PaymentMethod.Card).Value;
        failed.MarkFailed("declined");

        var result = failed.Refund("refund-tx");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.NotRefundable);
    }
}
