using FluentAssertions;
using ModularCommerce.Inventory.Domain.Stock;
using Xunit;

namespace ModularCommerce.Inventory.UnitTests.Domain;

/// <summary>
/// Commit (kalıcı düşüş, FR-3.3) kuralları: ödeme alındıktan sonra rezervasyon stoktan
/// kesin düşer. Kritik değişmez: OnHand ve Reserved BİRLİKTE azalır, Available DEĞİŞMEZ —
/// bu yüzden Commit hiçbir yarışta oversell üretemez.
/// </summary>
public class StockItemCommitTests
{
    private static (StockItem Item, Reservation Reservation) ItemWithReservation(
        int onHand = 10, int quantity = 3)
    {
        var item = StockItem.Create(Guid.NewGuid(), onHand).Value;
        var reservation = item.Reserve(quantity).Value;
        return (item, reservation);
    }

    [Fact(DisplayName = "Commit kalıcı düşüş yapar: OnHand ve Reserved birlikte azalır, Available değişmez, StockCommitted raise edilir")]
    public void Commit_ActiveReservation_DecrementsOnHandAndReserved()
    {
        var (item, reservation) = ItemWithReservation(onHand: 10, quantity: 3);
        var availableBefore = item.Available;

        var result = item.Commit(reservation);

        result.IsSuccess.Should().BeTrue();
        item.OnHand.Should().Be(7);
        item.Reserved.Should().Be(0);
        item.Available.Should().Be(availableBefore, "commit ayrılmış stoğu düşürür, satılabilir stoğu DEĞİL");
        reservation.Status.Should().Be(ReservationStatus.Committed);
        item.DomainEvents.Should().ContainSingle(e => e is StockCommitted)
            .Which.As<StockCommitted>().ReservationId.Should().Be(reservation.Id);
    }

    [Fact(DisplayName = "Commit idempotenttir: ikinci çağrı no-op başarı, sayaçlar bir daha düşmez")]
    public void Commit_AlreadyCommitted_IsNoOpSuccess()
    {
        var (item, reservation) = ItemWithReservation(onHand: 10, quantity: 3);
        item.Commit(reservation);

        var second = item.Commit(reservation);

        second.IsSuccess.Should().BeTrue();
        item.OnHand.Should().Be(7, "ikinci commit sayaçları tekrar düşürmemeli");
        item.Reserved.Should().Be(0);
    }

    [Fact(DisplayName = "Release edilmiş rezervasyon commit edilemez (Conflict)")]
    public void Commit_ReleasedReservation_ReturnsNotCommittable()
    {
        var (item, reservation) = ItemWithReservation(onHand: 10, quantity: 3);
        item.Release(reservation);

        var result = item.Commit(reservation);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.ReservationNotCommittable);
        item.OnHand.Should().Be(10, "başarısız commit sayaçlara dokunmamalı");
        item.Reserved.Should().Be(0);
    }

    [Fact(DisplayName = "Başka stok kaydının rezervasyonu commit edilemez")]
    public void Commit_ReservationOfAnotherStockItem_ReturnsMismatch()
    {
        var (_, foreignReservation) = ItemWithReservation();
        var otherItem = StockItem.Create(Guid.NewGuid(), 5).Value;

        var result = otherItem.Commit(foreignReservation);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.ReservationMismatch);
        otherItem.OnHand.Should().Be(5);
    }

    [Fact(DisplayName = "Commit sonrası release no-op DEĞİL, Conflict döner (kalıcı düşüş geri alınamaz)")]
    public void Release_AfterCommit_ReturnsNotReleasable()
    {
        var (item, reservation) = ItemWithReservation(onHand: 10, quantity: 3);
        item.Commit(reservation);

        var release = item.Release(reservation);

        release.IsFailure.Should().BeTrue();
        release.Error.Should().Be(InventoryErrors.ReservationNotReleasable);
        item.OnHand.Should().Be(7, "release, commit'lenmiş düşüşü geri getirmemeli");
    }

    [Fact(DisplayName = "Commit edilen stok yeniden rezerve edilemez (Available gerçekten azaldı)")]
    public void Commit_ThenReserveBeyondAvailable_Fails()
    {
        var item = StockItem.Create(Guid.NewGuid(), 3).Value;
        var reservation = item.Reserve(3).Value;

        item.Commit(reservation).IsSuccess.Should().BeTrue();

        var again = item.Reserve(1);
        again.IsFailure.Should().BeTrue();
        again.Error.Should().Be(InventoryErrors.InsufficientStock);
    }
}
