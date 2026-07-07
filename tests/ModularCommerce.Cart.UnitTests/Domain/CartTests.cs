using FluentAssertions;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Shared.Kernel;
using Xunit;

namespace ModularCommerce.Cart.UnitTests.Domain;

using Cart = ModularCommerce.Cart.Domain.Carts.Cart;

public class CartTests
{
    private static Cart CreateCart() => Cart.Create(Guid.NewGuid()).Value;

    [Fact(DisplayName = "Geçerli müşteriyle boş sepet oluşur")]
    public void Create_WithValidCustomer_ReturnsEmptyCart()
    {
        var customerId = Guid.NewGuid();

        var result = Cart.Create(customerId);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerId.Should().Be(customerId);
        result.Value.IsEmpty.Should().BeTrue();
    }

    [Fact(DisplayName = "Boş müşteri kimliği reddedilir")]
    public void Create_WithEmptyCustomerId_ReturnsFailure()
    {
        var result = Cart.Create(Guid.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CartErrors.InvalidCustomerId);
    }

    [Fact(DisplayName = "Ürün eklenir (FR-4.1)")]
    public void AddItem_NewProduct_AddsLine()
    {
        var cart = CreateCart();
        var productId = Guid.NewGuid();

        cart.AddItem(productId, 3).IsSuccess.Should().BeTrue();

        cart.Items.Should().ContainSingle(i => i.ProductId == productId && i.Quantity == 3);
    }

    [Fact(DisplayName = "Aynı ürün ikinci kez eklenince satır birleşir, eklenme zamanı korunur")]
    public void AddItem_ExistingProduct_MergesQuantity()
    {
        var cart = CreateCart();
        var productId = Guid.NewGuid();

        cart.AddItem(productId, 3);
        var addedAt = cart.Items[0].AddedAtUtc;
        cart.AddItem(productId, 4).IsSuccess.Should().BeTrue();

        cart.Items.Should().ContainSingle();
        cart.Items[0].Quantity.Should().Be(7);
        cart.Items[0].AddedAtUtc.Should().Be(addedAt);
    }

    [Fact(DisplayName = "Birleşen toplam satır tavanını aşamaz (flash-sale stok gaspı önlemi)")]
    public void AddItem_MergeExceedingLimit_ReturnsFailure()
    {
        var cart = CreateCart();
        var productId = Guid.NewGuid();
        cart.AddItem(productId, Cart.MaxQuantityPerLine - 1);

        var result = cart.AddItem(productId, 2);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CartErrors.QuantityLimitExceeded);
        cart.Items[0].Quantity.Should().Be(Cart.MaxQuantityPerLine - 1, "başarısız işlem sepeti değiştirmez");
    }

    [Fact(DisplayName = "Tek seferde tavan üstü adet de reddedilir")]
    public void AddItem_ExceedingLimitAtOnce_ReturnsFailure()
    {
        var result = CreateCart().AddItem(Guid.NewGuid(), Cart.MaxQuantityPerLine + 1);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CartErrors.QuantityLimitExceeded);
    }

    [Theory(DisplayName = "Sıfır/negatif adet reddedilir")]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_WithInvalidQuantity_ReturnsFailure(int quantity)
    {
        var result = CreateCart().AddItem(Guid.NewGuid(), quantity);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CartErrors.InvalidQuantity);
    }

    [Fact(DisplayName = "Boş ürün kimliği reddedilir")]
    public void AddItem_WithEmptyProductId_ReturnsFailure()
    {
        var result = CreateCart().AddItem(Guid.Empty, 1);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CartErrors.InvalidProductId);
    }

    [Fact(DisplayName = "Satır sayısı tavanı aşılamaz")]
    public void AddItem_ExceedingLineLimit_ReturnsFailure()
    {
        var cart = CreateCart();
        for (var i = 0; i < Cart.MaxLines; i++)
        {
            cart.AddItem(Guid.NewGuid(), 1).IsSuccess.Should().BeTrue();
        }

        var result = cart.AddItem(Guid.NewGuid(), 1);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CartErrors.LineLimitExceeded);
    }

    [Fact(DisplayName = "Adet güncellenir (FR-4.1)")]
    public void ChangeQuantity_ExistingProduct_UpdatesQuantity()
    {
        var cart = CreateCart();
        var productId = Guid.NewGuid();
        cart.AddItem(productId, 2);

        cart.ChangeQuantity(productId, 5).IsSuccess.Should().BeTrue();

        cart.Items[0].Quantity.Should().Be(5);
    }

    [Fact(DisplayName = "Olmayan ürünün adedi değiştirilemez → NotFound")]
    public void ChangeQuantity_UnknownProduct_ReturnsNotFound()
    {
        var result = CreateCart().ChangeQuantity(Guid.NewGuid(), 5);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact(DisplayName = "Adet sıfıra çekilemez: silme açık DELETE ile yapılır")]
    public void ChangeQuantity_ToZero_ReturnsFailure()
    {
        var cart = CreateCart();
        var productId = Guid.NewGuid();
        cart.AddItem(productId, 2);

        var result = cart.ChangeQuantity(productId, 0);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CartErrors.InvalidQuantity);
    }

    [Fact(DisplayName = "Ürün çıkarılır; son ürün çıkınca sepet boşalır (FR-4.1)")]
    public void RemoveItem_LastProduct_EmptiesCart()
    {
        var cart = CreateCart();
        var productId = Guid.NewGuid();
        cart.AddItem(productId, 2);

        cart.RemoveItem(productId).IsSuccess.Should().BeTrue();

        cart.IsEmpty.Should().BeTrue();
    }

    [Fact(DisplayName = "Olmayan ürün çıkarılamaz → NotFound")]
    public void RemoveItem_UnknownProduct_ReturnsNotFound()
    {
        var result = CreateCart().RemoveItem(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact(DisplayName = "Rehydrate depodaki satırları doğrulamadan geri yükler")]
    public void Rehydrate_RestoresItems()
    {
        var customerId = Guid.NewGuid();
        var items = new[] { new CartItem(Guid.NewGuid(), 3, DateTime.UtcNow) };

        var cart = Cart.Rehydrate(customerId, items);

        cart.CustomerId.Should().Be(customerId);
        cart.Items.Should().BeEquivalentTo(items);
    }
}
