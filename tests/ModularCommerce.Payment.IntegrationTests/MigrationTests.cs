using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ModularCommerce.Payment.Domain.Payments;
using ModularCommerce.Payment.IntegrationTests.Fixtures;
using Xunit;
using PaymentAggregate = ModularCommerce.Payment.Domain.Payments.Payment;

namespace ModularCommerce.Payment.IntegrationTests;

[Collection("PaymentPostgres")]
public sealed class MigrationTests(PostgresContainerFixture fixture)
{
    [Fact(DisplayName = "Migration uygulanır: payment şemasında ödeme round-trip'i (attempts dahil) çalışır")]
    public async Task Migration_CreatesPaymentSchema_AndPaymentRoundTrips()
    {
        Guid paymentId;

        await using (var context = fixture.CreateContext())
        {
            (await context.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();

            var payment = PaymentAggregate.Create(
                Guid.NewGuid(), Guid.NewGuid(), 149.90m, "TRY", "migrasyon-kaniti", PaymentMethod.Card).Value;
            payment.RecordAttempt(PaymentAttemptOutcome.TransientError, null, "psp_5xx", 120);
            payment.RecordAttempt(PaymentAttemptOutcome.Success, "psp-tx-1", null, 45);
            payment.MarkCompleted("psp-tx-1");

            context.Payments.Add(payment);
            await context.SaveChangesAsync();
            paymentId = payment.Id;
        }

        await using (var verify = fixture.CreateContext())
        {
            var loaded = await verify.Payments
                .Include(p => p.Attempts)
                .SingleAsync(p => p.Id == paymentId);

            loaded.Status.Should().Be(PaymentStatus.Completed);
            loaded.PspTransactionId.Should().Be("psp-tx-1");
            loaded.Amount.Should().Be(149.90m);
            loaded.Attempts.Should().HaveCount(2, "retry hikayesi satır satır saklanır (NFR-6.4)");
            loaded.Attempts.Select(a => a.AttemptNumber).Should().BeEquivalentTo([1, 2]);
        }
    }
}
