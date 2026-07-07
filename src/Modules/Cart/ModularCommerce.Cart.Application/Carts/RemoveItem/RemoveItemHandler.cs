using ModularCommerce.Cart.Application.Carts.Common;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Cart.Application.Carts.RemoveItem;

public sealed class RemoveItemHandler(ICartRepository carts)
{
    public async Task<Result<CartResponse>> HandleAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var getResult = await carts.GetAsync(customerId, cancellationToken);
        if (getResult.IsFailure)
        {
            return Result.Failure<CartResponse>(getResult.Error);
        }

        var cart = getResult.Value;
        if (cart is null)
        {
            return Result.Failure<CartResponse>(CartErrors.ItemNotFound(productId));
        }

        var removeResult = cart.RemoveItem(productId);
        if (removeResult.IsFailure)
        {
            return Result.Failure<CartResponse>(removeResult.Error);
        }

        var persistResult = cart.IsEmpty
            ? await carts.RemoveAsync(customerId, cancellationToken)
            : await carts.SaveAsync(cart, cancellationToken);

        if (persistResult.IsFailure)
        {
            return Result.Failure<CartResponse>(persistResult.Error);
        }

        return Result.Success(CartResponse.FromCart(cart));
    }
}
