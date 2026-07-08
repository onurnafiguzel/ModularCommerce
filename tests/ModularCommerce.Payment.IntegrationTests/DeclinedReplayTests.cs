using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ModularCommerce.Payment.Contracts;
using ModularCommerce.Payment.Domain.Payments;
using ModularCommerce.Payment.Infrastructure.Psp;
using ModularCommerce.Payment.IntegrationTests.Fixtures;
using Xunit;

namespace ModularCommerce.Payment.IntegrationTests;

/// <summary>
/// FR-6.2'nin tam metni: aynı key ile ikinci istek İLK SONUCUN kopyasını döner —
/// başarısız (declined) sonuç DAHİL. Yeni deneme yeni key ister.
/// </summary>
[Collection("PaymentPostgres")]
public sealed class DeclinedReplayTests(PostgresContainerFixture fixture)
{
    private static readonly PspOptions AlwaysDecline = new() { LatencyMs = 0, DeclineRate = 1 };

    [Fact(DisplayName = "Declined replay: aynı key PSP'ye GİTMEDEN Failed kopyasını döner; yeni key yeni deneme yapar (FR-6.2)")]
    public async Task DeclinedPayment_IsReplayedWithoutNewPspCall()
    {
        var customerId = Guid.NewGuid();
        var key = $"red-{Guid.NewGuid():N}";
        var psp = TestPaymentServiceFactory.CreatePspClient(AlwaysDecline);
        var request = new ChargeRequest(customerId, Guid.NewGuid(), key, 100m, "TRY");

        // İlk çağrı: PSP declined → satır Failed finalize edilir.
        await using (var context = fixture.CreateContext())
        {
            var result = await TestPaymentServiceFactory.Create(context, psp, AlwaysDecline)
                .ChargeAsync(request, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Payment.Declined");
        }
        psp.Calls.Should().Be(1);

        // Aynı key ikinci çağrı: kopyayı döner, PSP sayacı ARTMAZ.
        await using (var context = fixture.CreateContext())
        {
            var replay = await TestPaymentServiceFactory.Create(context, psp, AlwaysDecline)
                .ChargeAsync(request, CancellationToken.None);

            replay.IsFailure.Should().BeTrue();
            replay.Error.Code.Should().Be("Payment.Declined");
        }
        psp.Calls.Should().Be(1, "replay PSP'ye gitmemeli (FR-6.2)");

        // Yeni key: yeni deneme (PSP tekrar aranır).
        await using (var newAttempt = fixture.CreateContext())
        {
            await TestPaymentServiceFactory.Create(newAttempt, psp, AlwaysDecline)
                .ChargeAsync(request with { IdempotencyKey = $"red2-{Guid.NewGuid():N}" }, CancellationToken.None);
        }
        psp.Calls.Should().Be(2);

        // Audit: Failed satır declined denemesiyle birlikte kalıcı (NFR-6.4).
        await using (var verify = fixture.CreateContext())
        {
            var row = await verify.Payments
                .Include(p => p.Attempts)
                .SingleAsync(p => p.CustomerId == customerId && p.IdempotencyKey == key);

            row.Status.Should().Be(PaymentStatus.Failed);
            row.FailureCode.Should().Be("insufficient_funds");
            row.Attempts.Should().ContainSingle()
                .Which.Outcome.Should().Be(PaymentAttemptOutcome.Declined);
        }
    }

    [Fact(DisplayName = "Replay güvenliği: Completed ödemenin key'i FARKLI tutarla gelirse AmountMismatch (sepet değişmiş)")]
    public async Task CompletedReplay_WithDifferentAmount_ReturnsAmountMismatch()
    {
        var customerId = Guid.NewGuid();
        var key = $"tutar-{Guid.NewGuid():N}";
        var options = new PspOptions { LatencyMs = 0 };
        var psp = TestPaymentServiceFactory.CreatePspClient(options);

        await using (var context = fixture.CreateContext())
        {
            (await TestPaymentServiceFactory.Create(context, psp, options).ChargeAsync(
                new ChargeRequest(customerId, Guid.NewGuid(), key, 100m, "TRY"),
                CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        await using (var context = fixture.CreateContext())
        {
            var mismatch = await TestPaymentServiceFactory.Create(context, psp, options).ChargeAsync(
                new ChargeRequest(customerId, Guid.NewGuid(), key, 175m, "TRY"),
                CancellationToken.None);

            mismatch.IsFailure.Should().BeTrue();
            mismatch.Error.Code.Should().Be("Payment.AmountMismatch");
        }

        psp.Calls.Should().Be(1, "mismatch yolunda PSP aranmaz");
    }
}
