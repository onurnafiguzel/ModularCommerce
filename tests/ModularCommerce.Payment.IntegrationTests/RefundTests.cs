using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ModularCommerce.Payment.Contracts;
using ModularCommerce.Payment.Domain.Payments;
using ModularCommerce.Payment.Infrastructure.Psp;
using ModularCommerce.Payment.IntegrationTests.Fixtures;
using Xunit;

namespace ModularCommerce.Payment.IntegrationTests;

/// <summary>
/// RefundAsync (W9 kapsamlı Cancel): tamamlanmış ödeme gerçek Postgres'e karşı iade edilir;
/// idempotent (aynı sipariş iki kez iptal → tek iade); tamamlanmamış ödeme NotRefundable.
/// </summary>
[Collection("PaymentPostgres")]
public sealed class RefundTests(PostgresContainerFixture fixture)
{
    private static readonly PspOptions Instant = new() { LatencyMs = 0 };

    private async Task<(Guid CustomerId, string Key)> SeedCompletedPaymentAsync()
    {
        var customerId = Guid.NewGuid();
        var key = $"iade-{Guid.NewGuid():N}";
        var psp = TestPaymentServiceFactory.CreatePspClient(Instant);

        await using var context = fixture.CreateContext();
        var result = await TestPaymentServiceFactory.Create(context, psp, Instant).ChargeAsync(
            new ChargeRequest(customerId, Guid.NewGuid(), key, 250m, "TRY"), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        return (customerId, key);
    }

    [Fact(DisplayName = "Refund: Completed ödeme Refunded olur, iade audit satırı eklenir")]
    public async Task Refund_CompletedPayment_MarksRefunded()
    {
        var (customerId, key) = await SeedCompletedPaymentAsync();

        await using (var context = fixture.CreateContext())
        {
            var refund = await CreatePaymentService(context).RefundAsync(
                new RefundRequest(customerId, Guid.NewGuid(), key, 250m), CancellationToken.None);

            refund.IsSuccess.Should().BeTrue();
            refund.Value.RefundTransactionId.Should().NotBeNull();
        }

        await using (var verify = fixture.CreateContext())
        {
            var payment = await verify.Payments
                .Include(p => p.Attempts)
                .SingleAsync(p => p.CustomerId == customerId && p.IdempotencyKey == key);

            payment.Status.Should().Be(PaymentStatus.Refunded);
            payment.RefundTransactionId.Should().NotBeNull();
            payment.Attempts.Should().Contain(a => a.ErrorCode == "refund", "iade audit satırı kalıcı (NFR-6.4)");
        }
    }

    [Fact(DisplayName = "Refund idempotenttir: aynı sipariş iki kez iptal → tek iade (çift para iadesi yok)")]
    public async Task Refund_Twice_IsIdempotent()
    {
        var (customerId, key) = await SeedCompletedPaymentAsync();

        string? firstRefundTx;
        await using (var context = fixture.CreateContext())
        {
            var first = await CreatePaymentService(context).RefundAsync(
                new RefundRequest(customerId, Guid.NewGuid(), key, 250m), CancellationToken.None);
            firstRefundTx = first.Value.RefundTransactionId;
        }

        await using (var context = fixture.CreateContext())
        {
            var second = await CreatePaymentService(context).RefundAsync(
                new RefundRequest(customerId, Guid.NewGuid(), key, 250m), CancellationToken.None);

            second.IsSuccess.Should().BeTrue();
            second.Value.RefundTransactionId.Should().Be(firstRefundTx, "ikinci iptal ilk iadenin kopyasını döner");
        }

        await using (var verify = fixture.CreateContext())
        {
            var payment = await verify.Payments
                .Include(p => p.Attempts)
                .SingleAsync(p => p.CustomerId == customerId && p.IdempotencyKey == key);
            payment.Attempts.Count(a => a.ErrorCode == "refund").Should().Be(1, "tek iade audit satırı");
        }
    }

    [Fact(DisplayName = "Bilinmeyen (customer, key) → NotRefundable")]
    public async Task Refund_UnknownPayment_ReturnsNotRefundable()
    {
        await using var context = fixture.CreateContext();

        var refund = await CreatePaymentService(context).RefundAsync(
            new RefundRequest(Guid.NewGuid(), Guid.NewGuid(), "yok-boyle-key", 100m), CancellationToken.None);

        refund.IsFailure.Should().BeTrue();
        refund.Error.Code.Should().Be("Payment.NotRefundable");
    }

    private static Infrastructure.ContractAdapters.PaymentService CreatePaymentService(
        Infrastructure.Persistence.PaymentDbContext context)
        => TestPaymentServiceFactory.Create(
            context, TestPaymentServiceFactory.CreatePspClient(Instant), Instant);
}
