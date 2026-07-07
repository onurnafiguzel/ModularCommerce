using FluentValidation;
using ModularCommerce.Cart.Application.Carts.Common;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Cart.Application.Carts.AddItem;

public sealed class AddItemHandler(
    ICartRepository carts,
    IValidator<AddItemCommand> validator)
{
    public async Task<Result<CartResponse>> HandleAsync(
        AddItemCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CartResponse>(Error.Validation(
                "Cart.AddItem.InvalidCommand",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        var getResult = await carts.GetAsync(command.CustomerId, cancellationToken);
        if (getResult.IsFailure)
        {
            return Result.Failure<CartResponse>(getResult.Error);
        }

        var cart = getResult.Value;
        if (cart is null)
        {
            var cartResult = Domain.Carts.Cart.Create(command.CustomerId);
            if (cartResult.IsFailure)
            {
                return Result.Failure<CartResponse>(cartResult.Error);
            }

            cart = cartResult.Value;
        }

        var addResult = cart.AddItem(command.ProductId, command.Quantity);
        if (addResult.IsFailure)
        {
            return Result.Failure<CartResponse>(addResult.Error);
        }

        var saveResult = await carts.SaveAsync(cart, cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result.Failure<CartResponse>(saveResult.Error);
        }

        return Result.Success(CartResponse.FromCart(cart));
    }
}
