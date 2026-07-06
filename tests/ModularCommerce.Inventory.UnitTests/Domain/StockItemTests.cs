using FluentAssertions;
using ModularCommerce.Inventory.Domain.Stock;
using Xunit;

namespace ModularCommerce.Inventory.UnitTests.Domain;

/// <summary>
/// StockItem aggregate'inin invariant testleri — iş kurallarının asıl test yüzeyi.
/// Not: bu testler eşzamanlılık DEĞİL, tekil davranış doğrular; eşzamanlılık kanıtı
/// integration testlerindedir (Naive vs OptimisticConcurrency).
/// </summary>
public class StockItemTests
{
    private static StockItem CreateItem(int onHand)
        => StockItem.Create(Guid.NewGuid(), onHand).Value;

    [Fact(DisplayName = "Geçerli değerlerle stok kaydı oluşturulur; Reserved 0 başlar")]
    public void Create_WithValidValues_ReturnsStockItem()
    {
        var productId = Guid.NewGuid();
        var result = StockItem.Create(productId, 10);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProductId.Should().Be(productId);
        result.Value.OnHand.Should().Be(10);
        result.Value.Reserved.Should().Be(0);
        result.Value.Available.Should().Be(10);
    }

    [Fact(DisplayName = "Boş ürün kimliği reddedilir")]
    public void Create_WithEmptyProductId_ReturnsFailure()
    {
        var result = StockItem.Create(Guid.Empty, 10);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.InvalidProductId);
    }

    [Fact(DisplayName = "Negatif stok adedi reddedilir")]
    public void Create_WithNegativeOnHand_ReturnsFailure()
    {
        var result = StockItem.Create(Guid.NewGuid(), -1);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.InvalidOnHand);
    }

    [Fact(DisplayName = "Rezervasyon kullanılabilir stoğu düşürür, OnHand değişmez (FR-3.2)")]
    public void Reserve_ReducesAvailableButNotOnHand()
    {
        var item = CreateItem(10);

        var result = item.Reserve(3);

        result.IsSuccess.Should().BeTrue();
        item.OnHand.Should().Be(10);
        item.Reserved.Should().Be(3);
        item.Available.Should().Be(7);
    }

    [Theory(DisplayName = "Sıfır veya negatif adet reddedilir")]
    [InlineData(0)]
    [InlineData(-5)]
    public void Reserve_WithNonPositiveQuantity_ReturnsFailure(int quantity)
    {
        var item = CreateItem(10);

        var result = item.Reserve(quantity);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.InvalidQuantity);
    }

    [Fact(DisplayName = "Kullanılabilir stoktan fazlası reddedilir — asla iyimser onay yok (NFR-3.4)")]
    public void Reserve_MoreThanAvailable_ReturnsInsufficientStock()
    {
        var item = CreateItem(10);
        item.Reserve(8);

        var result = item.Reserve(3);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.InsufficientStock);
        item.Reserved.Should().Be(8, "başarısız rezervasyon durumu değiştirmemeli");
    }

    [Fact(DisplayName = "Başarılı rezervasyon StockReserved event'ini üretir")]
    public void Reserve_RaisesStockReserved()
    {
        var item = CreateItem(10);

        var reservation = item.Reserve(4).Value;

        item.DomainEvents.OfType<StockReserved>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                ProductId = item.ProductId,
                ReservationId = reservation.Id,
                Quantity = 4,
            });
    }

    [Fact(DisplayName = "Stok tam 0'a inince ProductSoldOut yayınlanır, öncesinde yayınlanmaz (FR-3.4)")]
    public void Reserve_RaisesProductSoldOut_ExactlyWhenAvailableHitsZero()
    {
        var item = CreateItem(10);

        item.Reserve(9);
        item.DomainEvents.OfType<ProductSoldOut>().Should().BeEmpty("stok henüz tükenmedi");

        item.Reserve(1);
        item.DomainEvents.OfType<ProductSoldOut>().Should().ContainSingle()
            .Which.ProductId.Should().Be(item.ProductId);
    }

    [Fact(DisplayName = "Rezervasyon Active durumda ve TTL ≈ 5 dakika ile doğar (FR-3.2)")]
    public void Reserve_CreatesActiveReservationWithTtl()
    {
        var item = CreateItem(10);
        var before = DateTime.UtcNow;

        var reservation = item.Reserve(2).Value;

        reservation.Status.Should().Be(ReservationStatus.Active);
        reservation.ProductId.Should().Be(item.ProductId);
        reservation.StockItemId.Should().Be(item.Id);
        reservation.Quantity.Should().Be(2);
        reservation.ExpiresAtUtc.Should().BeCloseTo(
            before.Add(Reservation.DefaultTtl), TimeSpan.FromSeconds(5));
    }
}
