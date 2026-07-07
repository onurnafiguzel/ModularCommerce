using FluentValidation;
using ModularCommerce.Cart.Application.Carts.Common;
using ModularCommerce.Cart.Domain.Carts;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Cart.Application.Carts.UpdateItemQuantity;

public sealed class UpdateItemQuantityHandler(
    ICartRepository carts,
    IValidator<UpdateItemQuantityCommand> validator)
{
    public async Task<Result<CartResponse>> HandleAsync(
        UpdateItemQuantityCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CartResponse>(Error.Validation(
                "Cart.UpdateItemQuantity.InvalidCommand",
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
            return Result.Failure<CartResponse>(CartErrors.ItemNotFound(command.ProductId));
        }

        var changeResult = cart.ChangeQuantity(command.ProductId, command.Quantity);
        if (changeResult.IsFailure)
        {
            return Result.Failure<CartResponse>(changeResult.Error);
        }

        var saveResult = await carts.SaveAsync(cart, cancellationToken);
        if (saveResult.IsFailure)
        {
            return Result.Failure<CartResponse>(saveResult.Error);
        }

        return Result.Success(CartResponse.FromCart(cart));
    }
}
