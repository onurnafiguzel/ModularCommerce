using FluentAssertions;
using ModularCommerce.Cart.Application.Carts.RemoveItem;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Cart.UnitTests.Application;

using Cart = ModularCommerce.Cart.Domain.Carts.Cart;

public class RemoveItemHandlerTests
{
    private readonly ICartRepository _carts = Substitute.For<ICartRepository>();
    private readonly RemoveItemHandler _handler;

    public RemoveItemHandlerTests()
    {
        _carts.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<Cart?>(null));
        _carts.SaveAsync(Arg.Any<Cart>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _carts.RemoveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _handler = new RemoveItemHandler(_carts);
    }

    [Fact(DisplayName = "Son satır silinince anahtar tamamen kaldırılır (yokluk = boşluk)")]
    public async Task Handle_RemovingLastLine_RemovesKey()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cart = Cart.Create(customerId).Value;
        cart.AddItem(productId, 2);
        _carts.GetAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Cart?>(cart));

        var result = await _handler.HandleAsync(customerId, productId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        await _carts.Received(1).RemoveAsync(customerId, Arg.Any<CancellationToken>());
        await _carts.DidNotReceive().SaveAsync(Arg.Any<Cart>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Başka satır kaldıysa sepet kaydedilir")]
    public async Task Handle_WithRemainingLines_Saves()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cart = Cart.Create(customerId).Value;
        cart.AddItem(productId, 2);
        cart.AddItem(Guid.NewGuid(), 1);
        _carts.GetAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<Cart?>(cart));

        var result = await _handler.HandleAsync(customerId, productId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        await _carts.Received(1).SaveAsync(cart, Arg.Any<CancellationToken>());
        await _carts.DidNotReceive().RemoveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sepet yokken silme → NotFound")]
    public async Task Handle_WhenCartMissing_ReturnsNotFound()
    {
        var result = await _handler.HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
