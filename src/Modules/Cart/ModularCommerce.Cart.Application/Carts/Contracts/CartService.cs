using ModularCommerce.Cart.Contracts;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Cart.Application.Carts.Contracts;

public sealed class CartService(ICartRepository carts) : ICartService
{
    public async Task<Result<IReadOnlyList<CartLineDto>>> GetItemsAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var getResult = await carts.GetAsync(customerId, cancellationToken);
        if (getResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CartLineDto>>(getResult.Error);
        }

        IReadOnlyList<CartLineDto> lines = getResult.Value is null
            ? []
            : [.. getResult.Value.Items.Select(i => new CartLineDto(i.ProductId, i.Quantity))];

        return Result.Success(lines);
    }

    public Task<Result> ClearAsync(Guid customerId, CancellationToken cancellationToken)
        => carts.RemoveAsync(customerId, cancellationToken);
}
