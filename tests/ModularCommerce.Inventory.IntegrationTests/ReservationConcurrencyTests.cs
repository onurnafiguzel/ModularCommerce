using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ModularCommerce.Inventory.Application.Abstractions;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Inventory.Infrastructure.Persistence;
using ModularCommerce.Inventory.Infrastructure.Persistence.Strategies;
using ModularCommerce.Inventory.IntegrationTests.Fixtures;
using ModularCommerce.Shared.Kernel;
using Xunit;
using Xunit.Abstractions;

namespace ModularCommerce.Inventory.IntegrationTests;

/// <summary>
/// HAFTANIN ANAHTAR TESTLERİ (NFR-3.1): aynı anda bırakılan paralel rezervasyon denemeleri.
/// - OptimisticConcurrency: 10 stokta TAM 10 başarı — sıfır oversell, deterministik assert.
/// - Naive: check-then-act yarışı → Reserved > OnHand (oversell) gözlemi.
/// Sonuç sayıları test çıktısına yazılır; Hafta 3 karşılaştırma tablosu buradan doldurulur.
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public class ReservationConcurrencyTests(
    PostgresContainerFixture fixture, 
    ITestOutputHelper output)
{
    private const int OnHand = 10;
    private const int Attackers = 100;

    private async Task<Guid> SeedStockAsync(int onHand)
    {
        await using var context = fixture.CreateContext();
        var item = StockItem.Create(Guid.NewGuid(), onHand).Value;
        item.ClearDomainEvents();
        context.StockItems.Add(item);
        await context.SaveChangesAsync();
        return item.ProductId;
    }

    private async Task<(int OnHand, int Reserved, int ReservationRows)> ReadStateAsync(Guid productId)
    {
        await using var context = fixture.CreateContext();
        var item = await context.StockItems.AsNoTracking().SingleAsync(s => s.ProductId == productId);
        var rows = await context.Reservations.CountAsync(r => r.ProductId == productId);
        return (item.OnHand, item.Reserved, rows);
    }

    [Fact(DisplayName = "Optimistic concurrency: 100 paralel istekte TAM 10 rezervasyon — sıfır oversell")]
    public async Task Optimistic_strategy_should_allow_exactly_available_stock()
    {
        var productId = await SeedStockAsync(OnHand);
        var startGate = new TaskCompletionSource();
        var successes = 0;
        var conflicts = 0;

        var workers = Enumerable.Range(0, Attackers).Select(async _ =>
        {
            await startGate.Task;

            // İstemci davranışı: ConcurrencyConflict retryable, InsufficientStock terminal.
            // Retry-until-terminal, yarışı assert'ten çıkarır — sonuç deterministiktir.
            while (true)
            {
                await using var context = fixture.CreateContext();
                var strategy = new OptimisticConcurrencyReservationStrategy(context);
                var result = await strategy.ReserveAsync(productId, 1, CancellationToken.None);

                if (result.IsSuccess)
                {
                    Interlocked.Increment(ref successes);
                    return;
                }

                if (result.Error == InventoryErrors.ConcurrencyConflict)
                {
                    Interlocked.Increment(ref conflicts);
                    continue; // "tekrar deneyin"
                }

                result.Error.Should().Be(InventoryErrors.InsufficientStock);
                return;
            }
        }).ToArray();

        startGate.SetResult(); // hepsi aynı anda başlar
        await Task.WhenAll(workers);

        var state = await ReadStateAsync(productId);
        output.WriteLine(
            $"[Optimistic] deneme={Attackers} başarı={successes} çakışma-retry={conflicts} " +
            $"OnHand={state.OnHand} Reserved={state.Reserved} rezervasyonSatırı={state.ReservationRows}");

        successes.Should().Be(OnHand, "NFR-3.1: 10 stokta tam 10 rezervasyon, ne eksik ne fazla");
        state.Reserved.Should().Be(OnHand, "oversell = 0");
        state.ReservationRows.Should().Be(OnHand);
    }

    [Fact(DisplayName = "Naive strateji: check-then-act yarışı oversell üretir (Reserved > OnHand)")]
    public async Task Naive_strategy_should_oversell_under_contention()
    {
        // Yarış doğası gereği nondeterministiktir; start-gate + 100 paralel deneme ile pratikte
        // her turda tetiklenir. Garanti için 3 tura kadar tekrarlanır (her tur taze ürün).
        // Bu test ayrıca naive raw SQL'in kolon adlarını gerçek şemaya karşı doğrular.
        for (var round = 1; round <= 3; round++)
        {
            var productId = await SeedStockAsync(OnHand);
            var startGate = new TaskCompletionSource();
            var successes = 0;

            var workers = Enumerable.Range(0, Attackers).Select(async _ =>
            {
                await startGate.Task;

                await using var context = fixture.CreateContext();
                var strategy = new NaiveReservationStrategy(context);
                var result = await strategy.ReserveAsync(productId, 1, CancellationToken.None);
                if (result.IsSuccess)
                {
                    Interlocked.Increment(ref successes);
                }
            }).ToArray();

            startGate.SetResult();
            await Task.WhenAll(workers);

            var state = await ReadStateAsync(productId);
            output.WriteLine(
                $"[Naive tur {round}] deneme={Attackers} başarı={successes} " +
                $"OnHand={state.OnHand} Reserved={state.Reserved} oversell={state.Reserved - state.OnHand}");

            if (state.Reserved > state.OnHand)
            {
                successes.Should().BeGreaterThan(OnHand, "naive yol Available kontrolünü bayat veriyle yapar");
                return; // oversell kanıtlandı
            }
        }

        Assert.Fail("3 turda oversell gözlenmedi — naive yol bir şekilde korunuyor olabilir (D3 garantilerini kontrol et)");
    }
}
