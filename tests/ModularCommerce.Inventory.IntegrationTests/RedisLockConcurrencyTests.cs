using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Inventory.Infrastructure.Locking;
using ModularCommerce.Inventory.Infrastructure.Persistence.Strategies;
using ModularCommerce.Inventory.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace ModularCommerce.Inventory.IntegrationTests;

/// <summary>
/// HAFTA 4 ANAHTAR TESTİ: Redis distributed lock stratejisi aynı 100-paralel deneyde
/// tam 10 başarı + sıfır oversell + SIFIR versiyon çakışması üretmeli — kilidin satış
/// noktası, çakışmayı beklemeye çevirmesidir. Sayılar karşılaştırma tablosuna gider.
/// </summary>
[Collection("PostgresRedis")]
[Trait("Category", "Integration")]
public class RedisLockConcurrencyTests(
    PostgresContainerFixture postgres,
    RedisContainerFixture redis,
    ITestOutputHelper output)
{
    private const int OnHand = 10;
    private const int Attackers = 100;

    private static IConfiguration LockConfig(int waitBudgetMs = 2_000) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Inventory:RedisLock:TtlSeconds"] = "5",
                // Test bütçesi üretim varsayılanından (100 ms) bilinçli geniş: 100 görev
                // TEK ürünün kilidinde sıralanır; amaç doğruluk assert'i, bekleme ölçümü değil.
                ["Inventory:RedisLock:WaitBudgetMs"] = waitBudgetMs.ToString(),
            })
            .Build();

    [Fact(DisplayName = "Redis lock: 100 paralel istekte TAM 10 rezervasyon, sıfır çakışma")]
    public async Task RedisLock_strategy_should_allow_exactly_available_stock_without_conflicts()
    {
        Guid productId;
        await using (var seedContext = postgres.CreateContext())
        {
            var item = StockItem.Create(Guid.NewGuid(), OnHand).Value;
            item.ClearDomainEvents();
            seedContext.StockItems.Add(item);
            await seedContext.SaveChangesAsync();
            productId = item.ProductId;
        }

        var config = LockConfig();
        var startGate = new TaskCompletionSource();
        var successes = 0;
        var lockTimeouts = 0;
        var concurrencyConflicts = 0;
        var soldOut = 0;

        var started = DateTime.UtcNow;
        var workers = Enumerable.Range(0, Attackers).Select(async _ =>
        {
            await startGate.Task;

            await using var context = postgres.CreateContext();
            var strategy = new RedisLockReservationStrategy(
                context,
                new RedisDistributedLock(redis.Connection),
                config,
                NullLogger<RedisLockReservationStrategy>.Instance);

            var result = await strategy.ReserveAsync(productId, 1, CancellationToken.None);

            if (result.IsSuccess)
            {
                Interlocked.Increment(ref successes);
            }
            else if (result.Error == InventoryErrors.LockTimeout)
            {
                Interlocked.Increment(ref lockTimeouts);
            }
            else if (result.Error == InventoryErrors.ConcurrencyConflict)
            {
                Interlocked.Increment(ref concurrencyConflicts);
            }
            else if (result.Error == InventoryErrors.InsufficientStock)
            {
                Interlocked.Increment(ref soldOut);
            }
        }).ToArray();

        startGate.SetResult();
        await Task.WhenAll(workers);
        var elapsed = DateTime.UtcNow - started;

        int reserved, reservationRows;
        await using (var readContext = postgres.CreateContext())
        {
            reserved = (await readContext.StockItems.AsNoTracking()
                .SingleAsync(s => s.ProductId == productId)).Reserved;
            reservationRows = await readContext.Reservations.CountAsync(r => r.ProductId == productId);
        }

        output.WriteLine(
            $"[RedisLock] deneme={Attackers} başarı={successes} kilit-timeout={lockTimeouts} " +
            $"çakışma={concurrencyConflicts} tükendi={soldOut} Reserved={reserved} " +
            $"rezervasyonSatırı={reservationRows} toplamSüre={elapsed.TotalMilliseconds:F0}ms");

        successes.Should().Be(OnHand, "NFR-3.1: 10 stokta tam 10 rezervasyon");
        reserved.Should().Be(OnHand, "oversell = 0");
        reservationRows.Should().Be(OnHand);
        concurrencyConflicts.Should().Be(0, "kilit altında tek yazar var — versiyon çakışması oluşmamalı");
        (successes + lockTimeouts + concurrencyConflicts + soldOut).Should().Be(Attackers);
    }

    [Fact(DisplayName = "Kilit bekleme bütçesi dolunca LockTimeout döner (aşırı dar bütçe)")]
    public async Task RedisLock_should_time_out_when_wait_budget_is_tiny()
    {
        var redisLock = new RedisDistributedLock(redis.Connection);
        var key = $"inventory:lock:test:{Guid.NewGuid():N}";

        // Kilidi işgal et, sonra dar bütçeyle ikinci edinmeyi dene.
        await using var holder = await redisLock.TryAcquireAsync(
            key, TimeSpan.FromSeconds(5), TimeSpan.Zero, CancellationToken.None);
        holder.Should().NotBeNull("boş anahtar ilk denemede edinilmeli");

        var second = await redisLock.TryAcquireAsync(
            key, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(30), CancellationToken.None);

        second.Should().BeNull("kilit doluyken dar bütçe içinde edinme başarısız olmalı");
    }
}
