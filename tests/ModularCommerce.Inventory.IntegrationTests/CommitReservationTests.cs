using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ModularCommerce.Inventory.Domain.Stock;
using ModularCommerce.Inventory.Infrastructure.ContractAdapters;
using ModularCommerce.Inventory.Infrastructure.Persistence;
using ModularCommerce.Inventory.Infrastructure.Persistence.Strategies;
using ModularCommerce.Inventory.IntegrationTests.Fixtures;
using Xunit;

namespace ModularCommerce.Inventory.IntegrationTests;

/// <summary>
/// IStockReservationService.Commit'in gerçek Postgres'e karşı davranışı (FR-3.3):
/// ödeme başarısı sonrası rezervasyon kalıcı düşüşe çevrilir — OnHand ve Reserved
/// birlikte azalır, idempotent, xmin yarışında sınırlı retry kazanır.
/// (Checkout ödeme zinciri — Hafta 7'nin Inventory tarafı.)
/// </summary>
[Collection("Postgres")]
public sealed class CommitReservationTests(PostgresContainerFixture fixture)
{
    private StockReservationService CreateService(InventoryDbContext context)
        => new(
            context,
            new OptimisticConcurrencyReservationStrategy(context),
            NullLogger<StockReservationService>.Instance);

    private async Task<Guid> SeedStockAsync(int onHand)
    {
        await using var context = fixture.CreateContext();
        var productId = Guid.NewGuid();
        context.StockItems.Add(StockItem.Create(productId, onHand).Value);
        await context.SaveChangesAsync();
        return productId;
    }

    [Fact(DisplayName = "Reserve → Commit: OnHand ve Reserved birlikte düşer, rezervasyon Committed olur")]
    public async Task Commit_AfterReserve_PermanentlyDecrementsStock()
    {
        var productId = await SeedStockAsync(10);

        Guid reservationId;
        await using (var context = fixture.CreateContext())
        {
            var reserve = await CreateService(context).ReserveAsync(productId, 4, CancellationToken.None);
            reserve.IsSuccess.Should().BeTrue();
            reservationId = reserve.Value.ReservationId;
        }

        await using (var context = fixture.CreateContext())
        {
            var commit = await CreateService(context).CommitAsync(reservationId, CancellationToken.None);
            commit.IsSuccess.Should().BeTrue();
        }

        await using (var verify = fixture.CreateContext())
        {
            var stockItem = await verify.StockItems.SingleAsync(s => s.ProductId == productId);
            var reservation = await verify.Reservations.SingleAsync(r => r.Id == reservationId);

            stockItem.OnHand.Should().Be(6);
            stockItem.Reserved.Should().Be(0);
            reservation.Status.Should().Be(ReservationStatus.Committed);
        }
    }

    [Fact(DisplayName = "Çift commit no-op başarıdır; sayaçlar ikinci kez düşmez")]
    public async Task Commit_Twice_IsIdempotent()
    {
        var productId = await SeedStockAsync(10);

        Guid reservationId;
        await using (var context = fixture.CreateContext())
        {
            reservationId = (await CreateService(context)
                .ReserveAsync(productId, 3, CancellationToken.None)).Value.ReservationId;
        }

        await using (var context = fixture.CreateContext())
        {
            var service = CreateService(context);
            (await service.CommitAsync(reservationId, CancellationToken.None)).IsSuccess.Should().BeTrue();
            (await service.CommitAsync(reservationId, CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        await using (var verify = fixture.CreateContext())
        {
            var stockItem = await verify.StockItems.SingleAsync(s => s.ProductId == productId);
            stockItem.OnHand.Should().Be(7, "ikinci commit sayaçları tekrar düşürmemeli");
            stockItem.Reserved.Should().Be(0);
        }
    }

    [Fact(DisplayName = "Release edilmiş rezervasyon commit edilemez → Conflict, sayaçlar değişmez")]
    public async Task Commit_ReleasedReservation_ReturnsConflict()
    {
        var productId = await SeedStockAsync(10);

        Guid reservationId;
        await using (var context = fixture.CreateContext())
        {
            var service = CreateService(context);
            reservationId = (await service
                .ReserveAsync(productId, 3, CancellationToken.None)).Value.ReservationId;
            (await service.ReleaseAsync(reservationId, CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        await using (var context = fixture.CreateContext())
        {
            var commit = await CreateService(context).CommitAsync(reservationId, CancellationToken.None);

            commit.IsFailure.Should().BeTrue();
            commit.Error.Code.Should().Be("Inventory.Reservation.NotCommittable");
        }

        await using (var verify = fixture.CreateContext())
        {
            var stockItem = await verify.StockItems.SingleAsync(s => s.ProductId == productId);
            stockItem.OnHand.Should().Be(10);
            stockItem.Reserved.Should().Be(0);
        }
    }

    [Fact(DisplayName = "Aynı sıcak satırda 10 paralel commit: retry hepsini geçirir, sayaçlar tutarlı")]
    public async Task ParallelCommits_OnSameStockItem_AllSucceedViaRetry()
    {
        var productId = await SeedStockAsync(50);

        var reservationIds = new List<Guid>();
        await using (var context = fixture.CreateContext())
        {
            var service = CreateService(context);
            for (var i = 0; i < 10; i++)
            {
                reservationIds.Add((await service
                    .ReserveAsync(productId, 2, CancellationToken.None)).Value.ReservationId);
            }
        }

        // Her commit kendi context'iyle (ayrık istekler gibi) aynı stok satırını
        // günceller → xmin çakışmaları kaçınılmaz; sınırlı retry hepsini geçirmeli
        // (para alınmışken commit nihayetinde başarmalıdır).
        var results = await Task.WhenAll(reservationIds.Select(async id =>
        {
            await using var context = fixture.CreateContext();
            return await CreateService(context).CommitAsync(id, CancellationToken.None);
        }));

        results.Should().OnlyContain(r => r.IsSuccess, "commit kesinleştirmedir, nihayetinde başarmalıdır");

        await using (var verify = fixture.CreateContext())
        {
            var stockItem = await verify.StockItems.SingleAsync(s => s.ProductId == productId);
            stockItem.OnHand.Should().Be(30, "10 × 2 adet kalıcı düştü");
            stockItem.Reserved.Should().Be(0);
            (await verify.Reservations.CountAsync(
                r => reservationIds.Contains(r.Id) && r.Status == ReservationStatus.Committed))
                .Should().Be(10);
        }
    }
}
