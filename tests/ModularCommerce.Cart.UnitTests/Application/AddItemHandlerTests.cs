using FluentAssertions;
using ModularCommerce.Cart.Application.Carts.AddItem;
using ModularCommerce.Cart.Application.Carts.Common;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Cart.UnitTests.Application;

using Cart = ModularCommerce.Cart.Domain.Carts.Cart;

public class AddItemHandlerTests
{
    private readonly ICartRepository _carts = Substitute.For<ICartRepository>();
    private readonly AddItemHandler _handler;

    public AddItemHandlerTests()
    {
        _carts.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Cart?>(null));
        _carts.SaveAsync(Arg.Any<Cart>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _handler = new AddItemHandler(_carts, new AddItemCommandValidator());
    }

    [Fact(DisplayName = "İlk ekleme: sepet yoksa oluşturulur, kaydedilir, uyarılı yanıt döner (FR-4.1/4.4)")]
    public async Task Handle_WhenCartMissing_CreatesAndSaves()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var result = await _handler.HandleAsync(
            new AddItemCommand(customerId, productId, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerId.Should().Be(customerId);
        result.Value.Items.Should().ContainSingle(i => i.ProductId == productId && i.Quantity == 2);
        result.Value.Warning.Should().Be(CartResponse.ReservationWarning);
        await _carts.Received(1).SaveAsync(
            Arg.Is<Cart>(c => c.CustomerId == customerId), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Mevcut sepete ekleme satırı birleştirir")]
    public async Task Handle_WithExistingCart_MergesLine()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cart = Cart.Create(customerId).Value;
        cart.AddItem(productId, 3);
        _carts.GetAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Cart?>(cart));

        var result = await _handler.HandleAsync(
            new AddItemCommand(customerId, productId, 4), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.Quantity == 7);
    }

    [Fact(DisplayName = "Domain kuralı ihlalinde sepet KAYDEDİLMEZ")]
    public async Task Handle_WhenDomainRejects_DoesNotSave()
    {
        var customerId = Guid.NewGuid();

        var result = await _handler.HandleAsync(
            new AddItemCommand(customerId, Guid.NewGuid(), Cart.MaxQuantityPerLine + 1),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CartErrors.QuantityLimitExceeded);
        await _carts.DidNotReceive().SaveAsync(Arg.Any<Cart>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sıfır adet istek-şekli doğrulamasına takılır")]
    public async Task Handle_WithZeroQuantity_ReturnsValidationError()
    {
        var result = await _handler.HandleAsync(
            new AddItemCommand(Guid.NewGuid(), Guid.NewGuid(), 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }
}
