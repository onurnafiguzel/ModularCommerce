using ModularCommerce.Cart.Application.Carts.Common;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Cart.Application.Carts.GetCart;

public sealed class GetCartHandler(ICartRepository carts)
{
    public async Task<Result<CartResponse>> HandleAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var getResult = await carts.GetAsync(customerId, cancellationToken);
        if (getResult.IsFailure)
        {
            return Result.Failure<CartResponse>(getResult.Error);
        }

        return Result.Success(getResult.Value is null
            ? CartResponse.Empty(customerId)
            : CartResponse.FromCart(getResult.Value));
    }
}
