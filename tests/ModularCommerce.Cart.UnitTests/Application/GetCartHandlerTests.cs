using FluentAssertions;
using ModularCommerce.Cart.Application.Carts.GetCart;
using ModularCommerce.Cart.Domain.Carts;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Cart.UnitTests.Application;

using Cart = ModularCommerce.Cart.Domain.Carts.Cart;

public class GetCartHandlerTests
{
    private readonly ICartRepository _carts = Substitute.For<ICartRepository>();
    private readonly GetCartHandler _handler;

    public GetCartHandlerTests()
    {
        _carts.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ModularCommerce.Shared.Kernel.Result.Success<Cart?>(null));
        _handler = new GetCartHandler(_carts);
    }

    [Fact(DisplayName = "Olmayan sepet 404 değil BOŞ sepettir")]
    public async Task Handle_WhenCartMissing_ReturnsEmptyCart()
    {
        var customerId = Guid.NewGuid();

        var result = await _handler.HandleAsync(customerId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerId.Should().Be(customerId);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "Mevcut sepet satırlarıyla döner")]
    public async Task Handle_WithExistingCart_ReturnsItems()
    {
        var customerId = Guid.NewGuid();
        var cart = Cart.Create(customerId).Value;
        cart.AddItem(Guid.NewGuid(), 2);
        _carts.GetAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(ModularCommerce.Shared.Kernel.Result.Success<Cart?>(cart));

        var result = await _handler.HandleAsync(customerId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.Quantity == 2);
    }
}
