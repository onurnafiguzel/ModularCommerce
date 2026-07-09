using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ModularCommerce.Inventory.Contracts;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Inventory.Infrastructure.BackgroundJobs;
using ModularCommerce.Inventory.Infrastructure.ContractAdapters;
using ModularCommerce.Inventory.Infrastructure.Persistence;
using ModularCommerce.Inventory.Infrastructure.Persistence.Strategies;
using ModularCommerce.Inventory.IntegrationTests.Fixtures;
using ModularCommerce.Ordering.Contracts;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Inventory.IntegrationTests;

/// <summary>
/// TTL süpürücüsünün dallanma çekirdeği (SweepBatchAsync): aday sorgusu GERÇEK Postgres'e karşı;
/// sınıflandırma ve stok servisi substitute (dallanmayı izole eder). Kanıt: Paid-bağlı rezervasyon
/// COMMIT edilir (oversell=0), yetim EXPIRE edilir, sınıflandırma hatası batch'e DOKUNMAZ.
/// </summary>
[Collection("Postgres")]
public sealed class ReservationTtlSweeperTests(PostgresContainerFixture fixture)
{
    private static ReservationTtlSweeper CreateSweeper()
        => new(Substitute.For<IServiceProvider>(), NullLogger<ReservationTtlSweeper>.Instance);

    /// <summary>Süresi GEÇMİŞ Active bir rezervasyon üretir (crash penceresini simüle eder).</summary>
    private async Task<Guid> SeedExpiredActiveReservationAsync(int onHand = 10, int quantity = 3)
    {
        var productId = Guid.NewGuid();
        Guid reservationId;

        await using (var context = fixture.CreateContext())
        {
            var item = StockItem.Create(productId, onHand).Value;
            var reservation = item.Reserve(quantity).Value;
            reservationId = reservation.Id;
            context.StockItems.Add(item);
            context.Reservations.Add(reservation); // ayrı aggregate — açıkça eklenmeli
            await context.SaveChangesAsync();

            // Crash penceresi: rezervasyon Active kaldı ve TTL'i geçmişte.
            await context.Database.ExecuteSqlAsync(
                $"""UPDATE inventory.reservations SET "ExpiresAtUtc" = now() - interval '1 minute' WHERE "Id" = {reservationId}""");
        }

        return reservationId;
    }

    [Fact(DisplayName = "Yetim rezervasyon (siparişe bağlı değil): sweeper EXPIRE eder → Reserved iade")]
    public async Task Sweep_OrphanReservation_ExpiresIt()
    {
        var reservationId = await SeedExpiredActiveReservationAsync();

        var reconciler = Substitute.For<IOrderReservationReconciler>();
        reconciler.ClassifyAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new ReservationClassification(reservationId, IsBoundToLiveOrder: false)]);

        var reservations = Substitute.For<IStockReservationService>();
        reservations.ExpireAsync(reservationId, Arg.Any<CancellationToken>()).Returns(Result.Success());

        await using (var context = fixture.CreateContext())
        {
            await CreateSweeper().SweepBatchAsync(context, reservations, reconciler, CancellationToken.None);
        }

        await reservations.Received(1).ExpireAsync(reservationId, Arg.Any<CancellationToken>());
        await reservations.DidNotReceive().CommitAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Paid siparişe bağlı rezervasyon (P2): sweeper ASLA expire etmez, COMMIT'e çevirir (oversell=0)")]
    public async Task Sweep_ReservationBoundToPaidOrder_CommitsNeverExpires()
    {
        var reservationId = await SeedExpiredActiveReservationAsync();

        var reconciler = Substitute.For<IOrderReservationReconciler>();
        reconciler.ClassifyAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new ReservationClassification(reservationId, IsBoundToLiveOrder: true)]);

        var reservations = Substitute.For<IStockReservationService>();
        reservations.CommitAsync(reservationId, Arg.Any<CancellationToken>()).Returns(Result.Success());

        await using (var context = fixture.CreateContext())
        {
            await CreateSweeper().SweepBatchAsync(context, reservations, reconciler, CancellationToken.None);
        }

        await reservations.Received(1).CommitAsync(reservationId, Arg.Any<CancellationToken>());
        await reservations.DidNotReceive().ExpireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sınıflandırma hatası: batch'e DOKUNULMAZ (şüphede expire yok — NFR-3.1)")]
    public async Task Sweep_WhenClassifyThrows_TouchesNothing()
    {
        await SeedExpiredActiveReservationAsync();

        var reconciler = Substitute.For<IOrderReservationReconciler>();
        reconciler.ClassifyAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<ReservationClassification>>>(_ => throw new InvalidOperationException("Ordering ulaşılamadı"));

        var reservations = Substitute.For<IStockReservationService>();

        await using var context = fixture.CreateContext();
        var act = async () => await CreateSweeper()
            .SweepBatchAsync(context, reservations, reconciler, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>("hata ExecuteAsync'te yakalanır, batch dokunulmaz");
        await reservations.DidNotReceive().ExpireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await reservations.DidNotReceive().CommitAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ---- Full-stack: GERÇEK StockReservationService ile sayaç etkileri (oversell=0 kanıtı) ----

    private static StockReservationService RealReservationService(InventoryDbContext context)
        => new(context, new OptimisticConcurrencyReservationStrategy(context),
            NullLogger<StockReservationService>.Instance);

    [Fact(DisplayName = "Full-stack yetim: sweeper gerçekten EXPIRE eder → Reserved iade, Available geri (FR-3.3)")]
    public async Task Sweep_Orphan_ActuallyExpiresAndRestoresStock()
    {
        var reservationId = await SeedExpiredActiveReservationAsync(onHand: 10, quantity: 3);
        var productId = await ProductIdOfAsync(reservationId);

        var reconciler = Substitute.For<IOrderReservationReconciler>();
        reconciler.ClassifyAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new ReservationClassification(reservationId, IsBoundToLiveOrder: false)]);

        await using (var context = fixture.CreateContext())
        {
            await CreateSweeper().SweepBatchAsync(context, RealReservationService(context), reconciler, CancellationToken.None);
        }

        await using (var verify = fixture.CreateContext())
        {
            var stock = await verify.StockItems.SingleAsync(s => s.ProductId == productId);
            stock.Reserved.Should().Be(0, "yetim rezervasyon expire edildi, ayrılmış stok iade edildi");
            stock.Available.Should().Be(10);
            (await verify.Reservations.SingleAsync(r => r.Id == reservationId)).Status
                .Should().Be(ReservationStatus.Expired);
        }
    }

    [Fact(DisplayName = "Full-stack P2: Paid-bağlı rezervasyon COMMIT'e çevrilir — Available ARTMAZ (oversell=0 KESİN)")]
    public async Task Sweep_BoundToPaidOrder_CommitsAndDoesNotRestoreAvailability()
    {
        var reservationId = await SeedExpiredActiveReservationAsync(onHand: 10, quantity: 3);
        var productId = await ProductIdOfAsync(reservationId);

        var reconciler = Substitute.For<IOrderReservationReconciler>();
        reconciler.ClassifyAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new ReservationClassification(reservationId, IsBoundToLiveOrder: true)]);

        await using (var context = fixture.CreateContext())
        {
            await CreateSweeper().SweepBatchAsync(context, RealReservationService(context), reconciler, CancellationToken.None);
        }

        await using (var verify = fixture.CreateContext())
        {
            var stock = await verify.StockItems.SingleAsync(s => s.ProductId == productId);
            // Commit: OnHand ve Reserved birlikte düştü → Available DEĞİŞMEDİ (satılmış stok geri açılmadı).
            stock.OnHand.Should().Be(7);
            stock.Reserved.Should().Be(0);
            stock.Available.Should().Be(7, "P2 reconcile-commit satılmış stoğu geri AÇMAZ → oversell=0");
            (await verify.Reservations.SingleAsync(r => r.Id == reservationId)).Status
                .Should().Be(ReservationStatus.Committed);
        }
    }

    private async Task<Guid> ProductIdOfAsync(Guid reservationId)
    {
        await using var context = fixture.CreateContext();
        return (await context.Reservations.SingleAsync(r => r.Id == reservationId)).ProductId;
    }
}
