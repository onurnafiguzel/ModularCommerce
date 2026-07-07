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
/// IStockReservationService.Release'in gerçek Postgres'e karşı davranışı:
/// sayaç düşer, idempotent, xmin yarışında sınırlı retry kazanır.
/// (Checkout telafi yolu — Hafta 6'nın Inventory tarafı.)
/// </summary>
[Collection("Postgres")]
public sealed class ReleaseReservationTests(PostgresContainerFixture fixture)
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

    [Fact(DisplayName = "Reserve → Release: Reserved düşer, rezervasyon Released olur")]
    public async Task Release_AfterReserve_RestoresAvailability()
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
            var release = await CreateService(context).ReleaseAsync(reservationId, CancellationToken.None);
            release.IsSuccess.Should().BeTrue();
        }

        await using (var verify = fixture.CreateContext())
        {
            var stockItem = await verify.StockItems.SingleAsync(s => s.ProductId == productId);
            var reservation = await verify.Reservations.SingleAsync(r => r.Id == reservationId);

            stockItem.Reserved.Should().Be(0);
            reservation.Status.Should().Be(ReservationStatus.Released);
        }
    }

    [Fact(DisplayName = "Çift release no-op başarıdır; sayaç ikinci kez düşmez")]
    public async Task Release_Twice_IsIdempotent()
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
            (await service.ReleaseAsync(reservationId, CancellationToken.None)).IsSuccess.Should().BeTrue();
            (await service.ReleaseAsync(reservationId, CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        await using (var verify = fixture.CreateContext())
        {
            (await verify.StockItems.SingleAsync(s => s.ProductId == productId))
                .Reserved.Should().Be(0, "ikinci release sayacı tekrar düşürmemeli");
        }
    }

    [Fact(DisplayName = "Bilinmeyen rezervasyon → NotFound")]
    public async Task Release_UnknownReservation_ReturnsNotFound()
    {
        await using var context = fixture.CreateContext();

        var result = await CreateService(context).ReleaseAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Inventory.Reservation.NotFound");
    }

    [Fact(DisplayName = "Aynı sıcak satırda 10 paralel release: retry hepsini geçirir, Reserved 0'a iner")]
    public async Task ParallelReleases_OnSameStockItem_AllSucceedViaRetry()
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

        // Her release kendi context'iyle (ayrık istekler gibi) aynı stok satırını
        // günceller → xmin çakışmaları kaçınılmaz; sınırlı retry hepsini geçirmeli.
        var results = await Task.WhenAll(reservationIds.Select(async id =>
        {
            await using var context = fixture.CreateContext();
            return await CreateService(context).ReleaseAsync(id, CancellationToken.None);
        }));

        results.Should().OnlyContain(r => r.IsSuccess, "release telafidir, nihayetinde başarmalıdır");

        await using (var verify = fixture.CreateContext())
        {
            (await verify.StockItems.SingleAsync(s => s.ProductId == productId)).Reserved.Should().Be(0);
            (await verify.Reservations.CountAsync(
                r => reservationIds.Contains(r.Id) && r.Status == ReservationStatus.Released))
                .Should().Be(10);
        }
    }
}
