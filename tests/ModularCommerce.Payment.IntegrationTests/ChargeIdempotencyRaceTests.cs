using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ModularCommerce.Payment.Contracts;
using ModularCommerce.Payment.Domain.Payments;
using ModularCommerce.Payment.Infrastructure.Psp;
using ModularCommerce.Payment.IntegrationTests.Fixtures;
using ModularCommerce.Shared.Kernel;
using Xunit;

namespace ModularCommerce.Payment.IntegrationTests;

/// <summary>
/// Kanıt yükümlülüğü §10.2: aynı idempotency key ile 100 paralel ödeme → TEK charge.
/// Nihai hakem payments tablosunun unique index'idir; kaybedenler InFlight/replay
/// yollarından kazananın sonucuna yakınsar (FR-6.2). PSP çağrı sayacı, "tek charge"ın
/// veritabanı satırından bağımsız ikinci kanıtıdır.
/// </summary>
[Collection("PaymentPostgres")]
public sealed class ChargeIdempotencyRaceTests(PostgresContainerFixture fixture)
{
    private static readonly PspOptions FastDeterministicPsp = new() { LatencyMs = 25 };

    /// <summary>Retryable InFlight'ta istemci sözleşmesini uygular: aynı key ile terminale kadar dener.</summary>
    private async Task<Result<PaymentResultDto>> ChargeUntilTerminalAsync(
        CountingPspClient psp, ChargeRequest request)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var context = fixture.CreateContext();
            var service = TestPaymentServiceFactory.Create(context, psp, FastDeterministicPsp);
            var result = await service.ChargeAsync(request, CancellationToken.None);

            if (result.IsSuccess || result.Error.Code != "Payment.InFlight")
            {
                return result;
            }

            await Task.Delay(Random.Shared.Next(5, 16));
        }

        throw new InvalidOperationException("Ödeme 200 denemede terminale ulaşmadı.");
    }

    [Fact(DisplayName = "Aynı key ile 100 PARALEL charge: DB'de TAM 1 Completed, PSP tam 1 kez arandı, hepsi aynı PaymentId (§10.2)")]
    public async Task HundredParallelCharges_ProduceSingleCharge()
    {
        var customerId = Guid.NewGuid();
        var key = $"yaris-{Guid.NewGuid():N}";
        var psp = TestPaymentServiceFactory.CreatePspClient(FastDeterministicPsp);
        var request = new ChargeRequest(customerId, Guid.NewGuid(), key, 250m, "TRY");

        // Start-gate: 100 çağrı aynı anda bırakılır (repo yarış testi deseni).
        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, 100).Select(async _ =>
        {
            await gate.Task;
            return await ChargeUntilTerminalAsync(psp, request);
        }).ToList();
        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        // 100 istemcinin hepsi başarıya yakınsar ve AYNI ödemeyi görür.
        results.Should().OnlyContain(r => r.IsSuccess);
        results.Select(r => r.Value.PaymentId).Distinct().Should().HaveCount(1);

        // PSP tam 1 kez arandı: double-charge fiziksel olarak imkansız.
        psp.Calls.Should().Be(1, "kazanan dışında kimse PSP'ye ulaşmamalı");

        await using var verify = fixture.CreateContext();
        var rows = await verify.Payments
            .Where(p => p.CustomerId == customerId && p.IdempotencyKey == key)
            .ToListAsync();
        rows.Should().ContainSingle().Which.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact(DisplayName = "Farklı müşteriler aynı key'i kullanabilir (kapsam müşteri-başına)")]
    public async Task SameKeyDifferentCustomers_BothCharge()
    {
        var key = $"ortak-{Guid.NewGuid():N}";
        var psp = TestPaymentServiceFactory.CreatePspClient(FastDeterministicPsp);

        var first = await ChargeUntilTerminalAsync(
            psp, new ChargeRequest(Guid.NewGuid(), Guid.NewGuid(), key, 100m, "TRY"));
        var second = await ChargeUntilTerminalAsync(
            psp, new ChargeRequest(Guid.NewGuid(), Guid.NewGuid(), key, 200m, "TRY"));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.PaymentId.Should().NotBe(second.Value.PaymentId);
        psp.Calls.Should().Be(2);
    }
}
