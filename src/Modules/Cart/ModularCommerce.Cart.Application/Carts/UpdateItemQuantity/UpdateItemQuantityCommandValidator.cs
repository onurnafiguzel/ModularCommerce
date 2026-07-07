using FluentValidation;

namespace ModularCommerce.Cart.Application.Carts.UpdateItemQuantity;

public sealed class UpdateItemQuantityCommandValidator : AbstractValidator<UpdateItemQuantityCommand>
{
    public UpdateItemQuantityCommandValidator()
    {
        RuleFor(c => c.ProductId)
            .NotEmpty()
            .WithMessage("Ürün kimliği boş olamaz.");

        RuleFor(c => c.Quantity)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Adet 1 veya daha büyük olmalıdır; satır silmek için DELETE kullanın.");
    }
}
