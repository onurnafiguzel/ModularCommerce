using FluentAssertions;
using ModularCommerce.Inventory.Domain.Stock;
using Xunit;

namespace ModularCommerce.Inventory.UnitTests.Domain;

/// <summary>
/// Expire (TTL süpürücüsü, FR-3.2/3.3): süresi dolmuş yetim rezervasyon sonlandırılır —
/// Reserved iade edilir, durum Expired. Release ile sayaç etkisi aynı ama iz ayrışır.
/// </summary>
public class StockItemExpireTests
{
    private static (StockItem Item, Reservation Reservation) ItemWithReservation(
        int onHand = 10, int quantity = 3)
    {
        var item = StockItem.Create(Guid.NewGuid(), onHand).Value;
        var reservation = item.Reserve(quantity).Value;
        return (item, reservation);
    }

    [Fact(DisplayName = "Expire yetim rezervasyonu sonlandırır: Reserved düşer, durum Expired, StockExpired raise")]
    public void Expire_ActiveReservation_DecrementsReservedAndMarksExpired()
    {
        var (item, reservation) = ItemWithReservation(onHand: 10, quantity: 3);

        var result = item.Expire(reservation);

        result.IsSuccess.Should().BeTrue();
        item.Reserved.Should().Be(0);
        item.Available.Should().Be(10);
        reservation.Status.Should().Be(ReservationStatus.Expired);
        item.DomainEvents.Should().ContainSingle(e => e is StockExpired)
            .Which.As<StockExpired>().ReservationId.Should().Be(reservation.Id);
    }

    [Fact(DisplayName = "Expire idempotenttir: ikinci çağrı no-op başarı, sayaç bir daha düşmez")]
    public void Expire_AlreadyExpired_IsNoOpSuccess()
    {
        var (item, reservation) = ItemWithReservation(onHand: 10, quantity: 3);
        item.Expire(reservation);

        var second = item.Expire(reservation);

        second.IsSuccess.Should().BeTrue();
        item.Reserved.Should().Be(0, "ikinci expire sayacı tekrar düşürmemeli");
    }

    [Fact(DisplayName = "Commit edilmiş rezervasyon expire edilemez → Conflict (oversell koruması)")]
    public void Expire_CommittedReservation_ReturnsNotExpirable()
    {
        var (item, reservation) = ItemWithReservation(onHand: 10, quantity: 3);
        item.Commit(reservation);

        var result = item.Expire(reservation);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.ReservationNotExpirable);
        item.OnHand.Should().Be(7, "commit edilmiş stok expire ile geri açılmamalı");
        item.Available.Should().Be(7);
    }

    [Fact(DisplayName = "Başka stok kaydının rezervasyonu expire edilemez")]
    public void Expire_ReservationOfAnotherStockItem_ReturnsMismatch()
    {
        var (_, foreignReservation) = ItemWithReservation();
        var otherItem = StockItem.Create(Guid.NewGuid(), 5).Value;

        var result = otherItem.Expire(foreignReservation);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.ReservationMismatch);
    }

    [Fact(DisplayName = "Expire sonrası aynı miktar yeniden rezerve edilebilir (stok gerçekten geri döndü)")]
    public void Expire_ThenReserve_SucceedsAgain()
    {
        var item = StockItem.Create(Guid.NewGuid(), 3).Value;
        var reservation = item.Reserve(3).Value;
        item.Available.Should().Be(0);

        item.Expire(reservation);

        item.Reserve(3).IsSuccess.Should().BeTrue();
        item.Available.Should().Be(0);
    }
}
