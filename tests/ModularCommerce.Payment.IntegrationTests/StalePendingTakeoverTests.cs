using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ModularCommerce.Payment.Contracts;
using ModularCommerce.Payment.Domain.Payments;
using ModularCommerce.Payment.Infrastructure.Psp;
using ModularCommerce.Payment.IntegrationTests.Fixtures;
using Xunit;
using PaymentAggregate = ModularCommerce.Payment.Domain.Payments.Payment;

namespace ModularCommerce.Payment.IntegrationTests;

/// <summary>
/// P3 crash penceresi: Pending satır bırakıp ölen bir process, o key'i sonsuza dek
/// kilitleyemez — StalePendingSeconds geçince ilk gelen istek satırı devralır ve
/// ödemeyi tamamlar. Taze Pending ise devralınamaz (InFlight).
/// </summary>
[Collection("PaymentPostgres")]
public sealed class StalePendingTakeoverTests(PostgresContainerFixture fixture)
{
    private static readonly PspOptions Options = new() { LatencyMs = 0, StalePendingSeconds = 30 };

    private async Task<Guid> SeedPendingAsync(Guid customerId, string key, bool stale)
    {
        await using var context = fixture.CreateContext();
        var payment = PaymentAggregate.Create(
            Guid.NewGuid(), customerId, 100m, "TRY", key, Domain.Payments.PaymentMethod.Card).Value;
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        if (stale)
        {
            // Crash'i simüle et: satır Pending, doğum anı bayatlık eşiğinin gerisinde.
            await context.Database.ExecuteSqlAsync(
                $"""
                 UPDATE payment.payments
                 SET "CreatedAtUtc" = now() - interval '10 minutes'
                 WHERE "Id" = {payment.Id}
                 """);
        }

        return payment.Id;
    }

    [Fact(DisplayName = "Bayat Pending (crash izi): yeni istek satırı devralır ve ödemeyi tamamlar — key kilitli kalmaz")]
    public async Task StalePending_IsTakenOverAndCompleted()
    {
        var customerId = Guid.NewGuid();
        var key = $"bayat-{Guid.NewGuid():N}";
        var paymentId = await SeedPendingAsync(customerId, key, stale: true);
        var psp = TestPaymentServiceFactory.CreatePspClient(Options);

        await using (var context = fixture.CreateContext())
        {
            var result = await TestPaymentServiceFactory.Create(context, psp, Options).ChargeAsync(
                new ChargeRequest(customerId, Guid.NewGuid(), key, 100m, "TRY"),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.PaymentId.Should().Be(paymentId, "yeni satır değil, MEVCUT satır devralınmalı");
        }

        psp.Calls.Should().Be(1);

        await using (var verify = fixture.CreateContext())
        {
            var row = await verify.Payments.SingleAsync(p => p.Id == paymentId);
            row.Status.Should().Be(PaymentStatus.Completed);
            row.ClaimedAtUtc.Should().NotBeNull("devralma izi kalmalı");
        }
    }

    [Fact(DisplayName = "Taze Pending devralınamaz: InFlight döner, PSP aranmaz (sahibi hâlâ çalışıyor)")]
    public async Task FreshPending_ReturnsInFlight()
    {
        var customerId = Guid.NewGuid();
        var key = $"taze-{Guid.NewGuid():N}";
        await SeedPendingAsync(customerId, key, stale: false);
        var psp = TestPaymentServiceFactory.CreatePspClient(Options);

        await using var context = fixture.CreateContext();
        var result = await TestPaymentServiceFactory.Create(context, psp, Options).ChargeAsync(
            new ChargeRequest(customerId, Guid.NewGuid(), key, 100m, "TRY"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payment.InFlight");
        psp.Calls.Should().Be(0);
    }
}
