using FluentAssertions;
using ModularCommerce.Inventory.Domain.Stock;
using Xunit;

namespace ModularCommerce.Inventory.UnitTests.Domain;

/// <summary>
/// Return (kapsamlı Cancel iadesi): commit edilmiş (kalıcı düşmüş) stok, sipariş iptalinde
/// OnHand'e geri açılır. Commit OnHand ve Reserved'ı birlikte düşürmüştü; Return yalnız
/// OnHand'i geri artırır → Available yükselir.
/// </summary>
public class StockItemReturnTests
{
    private static (StockItem Item, Reservation Reservation) CommittedReservation(
        int onHand = 10, int quantity = 3)
    {
        var item = StockItem.Create(Guid.NewGuid(), onHand).Value;
        var reservation = item.Reserve(quantity).Value;
        item.Commit(reservation);
        return (item, reservation);
    }

    [Fact(DisplayName = "Return commit edilmiş stoğu geri açar: OnHand artar, durum Returned, StockReturned raise")]
    public void Return_CommittedReservation_RestoresOnHand()
    {
        var (item, reservation) = CommittedReservation(onHand: 10, quantity: 3);
        // Commit sonrası: OnHand=7, Reserved=0, Available=7
        item.OnHand.Should().Be(7);

        var result = item.Return(reservation);

        result.IsSuccess.Should().BeTrue();
        item.OnHand.Should().Be(10, "iade OnHand'i geri getirir");
        item.Reserved.Should().Be(0);
        item.Available.Should().Be(10);
        reservation.Status.Should().Be(ReservationStatus.Returned);
        item.DomainEvents.Should().ContainSingle(e => e is StockReturned)
            .Which.As<StockReturned>().ReservationId.Should().Be(reservation.Id);
    }

    [Fact(DisplayName = "Return idempotenttir: ikinci çağrı no-op başarı, OnHand bir daha artmaz")]
    public void Return_AlreadyReturned_IsNoOpSuccess()
    {
        var (item, reservation) = CommittedReservation(onHand: 10, quantity: 3);
        item.Return(reservation);

        var second = item.Return(reservation);

        second.IsSuccess.Should().BeTrue();
        item.OnHand.Should().Be(10, "ikinci iade OnHand'i tekrar artırmamalı");
    }

    [Fact(DisplayName = "Commit edilmemiş (Active) rezervasyon iade edilemez → Conflict")]
    public void Return_ActiveReservation_ReturnsNotReturnable()
    {
        var item = StockItem.Create(Guid.NewGuid(), 10).Value;
        var reservation = item.Reserve(3).Value; // Active, commit edilmedi

        var result = item.Return(reservation);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.ReservationNotReturnable);
        item.OnHand.Should().Be(10, "commit edilmemiş iade OnHand'e dokunmamalı");
    }

    [Fact(DisplayName = "Başka stok kaydının rezervasyonu iade edilemez")]
    public void Return_ReservationOfAnotherStockItem_ReturnsMismatch()
    {
        var (_, foreignReservation) = CommittedReservation();
        var otherItem = StockItem.Create(Guid.NewGuid(), 5).Value;

        var result = otherItem.Return(foreignReservation);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.ReservationMismatch);
    }

    [Fact(DisplayName = "Return sonrası stok yeniden satılabilir (Available gerçekten yükseldi)")]
    public void Return_ThenReserve_UsesRestoredStock()
    {
        var item = StockItem.Create(Guid.NewGuid(), 3).Value;
        var reservation = item.Reserve(3).Value;
        item.Commit(reservation);
        item.Available.Should().Be(0);

        item.Return(reservation);

        item.Reserve(3).IsSuccess.Should().BeTrue("iade edilen stok yeniden rezerve edilebilir");
    }
}
