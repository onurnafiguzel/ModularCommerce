using FluentAssertions;
using ModularCommerce.Payment.Domain.Payments;
using Xunit;
// "Payment" modül namespace'inin segmentiyle çakışır — aggregate'e alias ile erişilir.
using PaymentAggregate = ModularCommerce.Payment.Domain.Payments.Payment;

namespace ModularCommerce.Payment.UnitTests.Domain;

/// <summary>
/// Payment aggregate kuralları: Pending doğar, tam bir kez terminale geçer, terminal
/// satır değiştirilemez (NFR-6.4 — idempotency replay'inin güvenilirliği buna yaslanır).
/// </summary>
public class PaymentTests
{
    private static PaymentAggregate NewPayment(decimal amount = 100m)
        => PaymentAggregate.Create(
            Guid.NewGuid(), Guid.NewGuid(), amount, "TRY", "anahtar-1", PaymentMethod.Card).Value;

    [Fact(DisplayName = "Create: Pending doğar, terminal alanlar boş")]
    public void Create_ValidRequest_StartsPending()
    {
        var payment = NewPayment();

        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.PspTransactionId.Should().BeNull();
        payment.FailureCode.Should().BeNull();
        payment.CompletedAtUtc.Should().BeNull();
        payment.Attempts.Should().BeEmpty();
        payment.Amount.Currency.Should().Be("TRY");
    }

    [Theory(DisplayName = "Create: geçersiz istek reddedilir")]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_NonPositiveAmount_Fails(decimal amount)
    {
        var result = PaymentAggregate.Create(
            Guid.NewGuid(), Guid.NewGuid(), amount, "TRY", "anahtar-1", PaymentMethod.Card);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.InvalidRequest);
    }

    [Fact(DisplayName = "Create: boş veya 64 karakteri aşan idempotency key reddedilir")]
    public void Create_InvalidIdempotencyKey_Fails()
    {
        PaymentAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, "TRY", "", PaymentMethod.Card)
            .IsFailure.Should().BeTrue();
        PaymentAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, "TRY", new string('k', 65), PaymentMethod.Card)
            .IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "MarkCompleted: Completed olur, PaymentCompleted raise edilir")]
    public void MarkCompleted_FromPending_Succeeds()
    {
        var payment = NewPayment();

        var result = payment.MarkCompleted("psp-tx-42");

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.PspTransactionId.Should().Be("psp-tx-42");
        payment.CompletedAtUtc.Should().NotBeNull();
        payment.DomainEvents.Should().ContainSingle(e => e is PaymentCompleted);
    }

    [Fact(DisplayName = "MarkFailed: Failed olur, PaymentFailed raise edilir")]
    public void MarkFailed_FromPending_Succeeds()
    {
        var payment = NewPayment();

        var result = payment.MarkFailed("insufficient_funds");

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureCode.Should().Be("insufficient_funds");
        payment.DomainEvents.Should().ContainSingle(e => e is PaymentFailed);
    }

    [Fact(DisplayName = "Terminal satır değiştirilemez: ikinci finalize AlreadyFinalized döner")]
    public void Finalize_Twice_IsRejected()
    {
        var completed = NewPayment();
        completed.MarkCompleted("psp-tx-1");

        completed.MarkCompleted("psp-tx-2").Error.Should().Be(PaymentErrors.AlreadyFinalized);
        completed.MarkFailed("late_decline").Error.Should().Be(PaymentErrors.AlreadyFinalized);
        completed.PspTransactionId.Should().Be("psp-tx-1", "terminal alanlar değişmemeli");

        var failed = NewPayment();
        failed.MarkFailed("declined");

        failed.MarkCompleted("psp-tx-3").Error.Should().Be(PaymentErrors.AlreadyFinalized);
        failed.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact(DisplayName = "RecordAttempt: append-only, deneme numarası artar")]
    public void RecordAttempt_AppendsWithIncrementingNumber()
    {
        var payment = NewPayment();

        payment.RecordAttempt(PaymentAttemptOutcome.TransientError, null, "psp_5xx", 120).IsSuccess.Should().BeTrue();
        payment.RecordAttempt(PaymentAttemptOutcome.Success, "psp-tx-9", null, 80).IsSuccess.Should().BeTrue();

        payment.Attempts.Should().HaveCount(2);
        payment.Attempts[0].AttemptNumber.Should().Be(1);
        payment.Attempts[0].Outcome.Should().Be(PaymentAttemptOutcome.TransientError);
        payment.Attempts[1].AttemptNumber.Should().Be(2);
        payment.Attempts[1].PspTransactionId.Should().Be("psp-tx-9");
    }

    [Fact(DisplayName = "Terminal satıra deneme eklenemez (immutable audit)")]
    public void RecordAttempt_AfterFinalize_IsRejected()
    {
        var payment = NewPayment();
        payment.MarkFailed("declined");

        var result = payment.RecordAttempt(PaymentAttemptOutcome.Success, "psp-tx", null, 50);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.AlreadyFinalized);
        payment.Attempts.Should().BeEmpty();
    }
}
