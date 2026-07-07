using FluentAssertions;
using ModularCommerce.Cart.Application.Carts.UpdateItemQuantity;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Cart.UnitTests.Application;

using Cart = ModularCommerce.Cart.Domain.Carts.Cart;

public class UpdateItemQuantityHandlerTests
{
    private readonly ICartRepository _carts = Substitute.For<ICartRepository>();
    private readonly UpdateItemQuantityHandler _handler;

    public UpdateItemQuantityHandlerTests()
    {
        _carts.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Cart?>(null));
        _carts.SaveAsync(Arg.Any<Cart>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _handler = new UpdateItemQuantityHandler(_carts, new UpdateItemQuantityCommandValidator());
    }

    [Fact(DisplayName = "Adet güncellenir ve kaydedilir")]
    public async Task Handle_WithExistingLine_UpdatesAndSaves()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cart = Cart.Create(customerId).Value;
        cart.AddItem(productId, 2);
        _carts.GetAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Cart?>(cart));

        var result = await _handler.HandleAsync(
            new UpdateItemQuantityCommand(customerId, productId, 5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.Quantity == 5);
        await _carts.Received(1).SaveAsync(cart, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sepet hiç yoksa satır da yoktur → NotFound")]
    public async Task Handle_WhenCartMissing_ReturnsNotFound()
    {
        var result = await _handler.HandleAsync(
            new UpdateItemQuantityCommand(Guid.NewGuid(), Guid.NewGuid(), 5), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact(DisplayName = "Sıfır adet istek-şekli doğrulamasına takılır (silme DELETE'in işi)")]
    public async Task Handle_WithZeroQuantity_ReturnsValidationError()
    {
        var result = await _handler.HandleAsync(
            new UpdateItemQuantityCommand(Guid.NewGuid(), Guid.NewGuid(), 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        await _carts.DidNotReceive().SaveAsync(Arg.Any<Cart>(), Arg.Any<CancellationToken>());
    }
}
