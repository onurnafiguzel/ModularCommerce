using FluentAssertions;
using ModularCommerce.Ordering.Domain.Orders;
using Xunit;

namespace ModularCommerce.Ordering.UnitTests.Domain;

public class OrderCreateTests
{
    private static OrderLineDraft ValidLine(decimal price = 100m, string currency = "TRY")
        => new(Guid.NewGuid(), "Ürün", price, currency, 2, Guid.NewGuid());

    [Fact(DisplayName = "Geçerli satırlarla sipariş Created doğar; toplam ve doğuş izi doğru")]
    public void Create_WithValidLines_ReturnsCreatedOrder()
    {
        var customerId = Guid.NewGuid();

        var result = Order.Create(customerId, "anahtar-1", [ValidLine(100m), ValidLine(50m)], "checkout");

        result.IsSuccess.Should().BeTrue();
        var order = result.Value;
        order.Status.Should().Be(OrderStatus.Created);
        order.CustomerId.Should().Be(customerId);
        order.TotalAmount.Should().Be(300m, "2×100 + 2×50");
        order.Currency.Should().Be("TRY");
        order.StatusHistory.Should().ContainSingle(h => h.FromStatus == null && h.ToStatus == OrderStatus.Created);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCreated);
    }

    [Fact(DisplayName = "Boş satır listesi reddedilir")]
    public void Create_WithNoLines_ReturnsFailure()
    {
        var result = Order.Create(Guid.NewGuid(), "anahtar", [], "checkout");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OrderErrors.NoLines);
    }

    [Theory(DisplayName = "Boş/aşırı uzun idempotency anahtarı reddedilir")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidKey_ReturnsFailure(string? key)
    {
        var result = Order.Create(Guid.NewGuid(), key!, [ValidLine()], "checkout");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OrderErrors.InvalidIdempotencyKey);
    }

    [Fact(DisplayName = "64 karakteri aşan anahtar reddedilir")]
    public void Create_WithTooLongKey_ReturnsFailure()
    {
        var result = Order.Create(
            Guid.NewGuid(), new string('a', Order.IdempotencyKeyMaxLength + 1), [ValidLine()], "checkout");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OrderErrors.InvalidIdempotencyKey);
    }

    [Fact(DisplayName = "Farklı para birimli satırlar reddedilir")]
    public void Create_WithMixedCurrencies_ReturnsFailure()
    {
        var result = Order.Create(
            Guid.NewGuid(), "anahtar", [ValidLine(currency: "TRY"), ValidLine(currency: "USD")], "checkout");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OrderErrors.CurrencyMismatch);
    }

    [Fact(DisplayName = "Geçersiz satır (sıfır adet / negatif fiyat / boş rezervasyon) reddedilir")]
    public void Create_WithInvalidLine_ReturnsFailure()
    {
        var zeroQty = ValidLine() with { Quantity = 0 };
        var negativePrice = ValidLine() with { UnitPrice = -1m };
        var emptyReservation = ValidLine() with { ReservationId = Guid.Empty };

        foreach (var line in new[] { zeroQty, negativePrice, emptyReservation })
        {
            var result = Order.Create(Guid.NewGuid(), "anahtar", [line], "checkout");
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be(OrderErrors.InvalidLine);
        }
    }
}
